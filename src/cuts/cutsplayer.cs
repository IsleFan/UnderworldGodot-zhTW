using System.Diagnostics;
using System.Collections;
using Peaky.Coroutines;
using System.Collections.Generic;
using Godot;

namespace Underworld
{
    /// <summary>
    /// The main virtual machine for running the cutscenes.
    /// Executes bytecode commands from .N00 control files, plays LPF animations,
    /// handles panorama scrolling with LBACK backgrounds, palette interpolation,
    /// and sprite overlays.
    ///
    /// Architecture based on reverse engineering of ovr108 (79 functions) and
    /// DPaint LPF format analysis. See "Cutscene Engine RE Notes.md".
    /// </summary>
    public partial class cutsplayer : UWClass
    {
        static int FrameNo = 0;
        static bool FullScreen;
        static CutsLoader cuts;
        static int currentFileExt;

        // Scroll direction table (from disassembly dseg_67d6+0x1068/0x1070)
        // Index 0=Down, 1=Right, 2=Up, 3=Left
        static readonly int[] ScrollDX = { 0, 1, 0, -1 };
        static readonly int[] ScrollDY = { 1, 0, -1, 0 };

        // Viewport/panorama state
        static int vpCanvasWidth, vpCanvasHeight;
        static int vpStartX, vpStartY;
        /// <summary>
        /// splity: the row, counted up from the bottom, where the picture stops and the subtitle bar begins, or -1 for
        /// none. Set by splity (24) and by vscreen's third argument (20), kept from one cutscene to the next as in the
        /// original.
        /// </summary>
        static int splitY = -1;
        static int vpScrollDX, vpScrollDY;
        static bool vpScrollActive;
        static int vpFrameOffset;

        // LBACK panorama composite — built from LBACK*.BYT files by func 22/21.
        // The composite is wider (horizontal) or taller (vertical) than 320x200.
        // See CutsceneBitmap_ovr108_33E0 (line 446657) for LBACK loading.
        static Godot.Image vpComposite;
        static Godot.Image vpCompositeClean;  // pristine LBACK for per-frame reset
        static bool vpIsHorizontal;
        static System.Collections.Generic.List<(int pos, int extent, int fileIdx, byte[] rawPixels)> vpFileMappings = new();

        // Sprite overlay state for scroll animation (N03 cart, N06 flags).
        // DrawArtToScreen (line 444058) draws at fixed screen position.
        static CutsLoader vpSpriteLoader;
        static int vpSpriteFrame;

        // === Panorama horizontal-scroll fixes ===
        //
        // Original symptom: CS000's horizontal scroll (cart heading to bridge)
        // showed two carts — one pre-rendered in LBACK000 and a second animating
        // via N03. N03 is a cart-only sprite with no background-erase writes, so
        // it couldn't paint over LBACK's cart.
        //
        // Secondary symptom: once (a) was fixed, the cart drifted left 1,2,3,4
        // pixels every 4 frames before snapping back on the 5th frame.
        //
        // Root causes:
        //   (a) LBACK000.BYT contains the cart at rest. During scroll the DOS
        //       engine substitutes a cart-less backdrop (CS000.N04's frame 0).
        //       We couldn't locate the exact engine mechanism in the disassembly
        //       — `Cutscene_23_Unk_ovr108_1393` (start-scroll) only stores scroll
        //       state and doesn't touch the panorama buffer — but empirically
        //       N04's frame 0 is what DOSBox ends up with as the scroll backdrop.
        //       We reproduce this visually by swapping the LBACK000 region of
        //       vpComposite with N04's frame 0 at the moment the sprite first
        //       writes pixels (see vpPendingBackdropSwap).
        //
        //   (b) N03's LPF is delta-encoded: roughly every 5th frame carries a
        //       real RLE payload (recordSize > 4), the intervening 4 frames are
        //       empty deltas (recordSize ≤ 4) that keep the pixel buffer as-is.
        //       The panorama, however, pans 1 pixel per display frame. Without
        //       compensation, the sprite image stays pinned at the viewport
        //       origin while the scene drifts under it, producing the 1,2,3,4
        //       pixel drift. Between LPF keyframes we offset the sprite draw by
        //       `vpSpriteStaleOffset` pixels in the pan direction; the offset
        //       resets to 0 at each keyframe, at which point the new sprite
        //       image's internal cart position is already authored to align
        //       with the panned scene (see CutsLoader.IsKeyFrame).
        //
        // This only applies to horizontal scrolls (CS000 intro's cart, and
        // CS002's later scroll). Vertical scenes (N05/N06/N07) have dense
        // sprites with their own erase writes and align naturally.
        //
        // Verification:
        //   - DOSBox per-capture-frame pixel diff from frame 1796 (scroll start)
        //     through 2310 (fade): content changes every 5 capture frames at
        //     70 fps = 14 fps effective render rate, matching N03's authored
        //     fps header and our frameTime.
        //   - Our dump frames 1-69 compared against DOSBox frames 1851+N*5
        //     align with zero horizontal offset and <1% pixel diff (palette
        //     conversion variance).

        // Deferred backdrop swap: set true at start-scroll; swap triggers on the
        // first sprite frame that has any RLE writes (non-empty mask). N03's
        // first few frames are all-Skip so LBACK's cart remains visible during
        // that grace window — a seamless hand-off between LBACK's cart and the
        // sprite's cart.
        static bool vpPendingBackdropSwap;
        static bool vpBackdropSwapped;

        // Sprite-stale offset: pixel offset applied to sprite draw position on
        // null-delta LPF frames, to keep the sprite scene-aligned while the
        // panorama pans underneath. See the block comment above for why.
        // Resets to 0 on each LPF keyframe; increments by 1 on each null-delta
        // frame. Offset direction = opposite of scroll direction (sprite slides
        // in the same direction the scene is panning).
        static int vpSpriteStaleOffset;

        // CutsceneNo accessible to ExecuteCommand for sprite path construction
        static int currentCutsceneNo;

        /// <summary>
        /// True when a panorama viewport is active (canvas differs from 320x200).
        /// </summary>
        static bool PanoramaActive =>
            vpCanvasWidth != 0 && vpCanvasHeight != 0 &&
            (vpCanvasWidth != 320 || vpCanvasHeight != 200);

        /// <summary>
        /// Height of the scene area on the 320x200 display: show_anm draws 200 - (splity + 1) rows of the frame, the
        /// rest is the subtitle bar.
        /// </summary>
        static int SceneDisplayH =>
            splitY >= 0 ? 200 - (splitY + 1) : 200;

        /// <summary>
        /// splity (24), and vscreen's split: sets the bar; the subtitles are bottom-aligned whether or not there is one.
        /// </summary>
        static void SetSplit(int arg, TextureRect cutscontrol)
        {
            splitY = arg == 999 ? -1 : arg - 1;
            if (!FullScreen)
            {
                return;
            }
            PlaceSubtitle(cutscontrol.Size.Y / 200f);
        }

        /// <summary>
        /// cuts_draw_text: the subtitle lines are drawn bottom up from fh * lines + 1, so the last line's bottom row is 2
        /// rows above the bottom of the screen, bar or no bar. The label is bottom-aligned there (a SYS font glyph ends at
        /// the line's descent, the bottom of its line box).
        /// </summary>
        static void PlaceSubtitle(float scale)
        {
            var subtitle = uimanager.instance.CutsSubtitle;
            subtitleBottom = (200 - 2) * scale;
            if (!subtitleHooked)
            {//each new text is laid out, then the label is moved so its lines end at the bottom
                subtitleHooked = true;
                subtitle.Finished += AlignSubtitle;
            }
            AlignSubtitle();
        }

        static float subtitleBottom = 792;
        static bool subtitleHooked;

        static void AlignSubtitle()
        {
            if (!FullScreen)
            {//a windowed cutscene keeps its own place
                return;
            }
            var subtitle = uimanager.instance.CutsSubtitle;
            var height = subtitle.GetContentHeight();
            subtitle.Size = new Vector2(subtitle.Size.X, height);
            subtitle.Position = new Vector2(subtitle.Position.X, subtitleBottom - height);
        }

        // Palette interpolation state (func 19).
        // Linear interpolation matching InterpolatePaletteRange_ovr108_32CC (line 446461):
        //   result = source + (step * (target - source)) / total
        static bool palInterpActive;
        static int palInterpSpeed;     // a step every this many 8-tick units (the task's period)
        static ulong palInterpStartMsec;  // when func 19 fired: the steps follow the clock (run_timebased_tasks)
        static int palInterpTotal;     // the number of steps (the task's total)
        static int palInterpTargetIndex;
        static Palette palInterpSource;  // snapshot of palette when func 19 fires
        static Palette palInterpTarget;  // target from PALS.DAT

        // Colour cycling state (CRNG from LPF header).
        // Applied per-frame by UpdatePaletteFadeTimers_ovr108_934 (line 438583)
        // which calls RotatePaletteEntry_seg023_9 (line 98009).
        static Palette crngPalette;  // working copy of palette for cycling
        static CutsLoader.CrngEntry[] crngRanges;
        static int[] crngCounters;   // per-range accumulator (mirrors disasm pad field)

        static int StringBlock;
        static bool cancelRequested;

        // Input and frame control, after the original's CutsState (CUTS.C).
        static bool inputPending;       // a key or button arrived and no pause, wait or skip has used it yet
        static ulong lastInputMsec;     // UW1 takes a button only 0x40 ticks after the last input or file start
        static bool puntAllowed;        // punt (12): whether Escape may end the cutscene
        static bool fadedIn;            // UW1: input skips only between a fade-in and a fade-out (fade47 != -2)
        static bool fadedOut;           // a fade-out was done and no fade-in since (fade49 == -2)
        static int skipToFrame;         // skip (4) or jump (11): the frame skipping runs up to, 0 none
        static bool skipping;           // frames are played without being drawn until skipToFrame
        static int loopFrame;           // loop (7): the frame the file is replayed up to
        static int loopRepeats;         // and how many more times

        public static bool IsPlaying { get; private set; }

        public static List<CutSceneCommand> commands;


        /// <summary>
        /// Initialise colour cycling state from an LPF file's CRNG block.
        /// Called each time a new LPF animation file is loaded.
        /// </summary>
        static void InitCrngCycling(CutsLoader loader)
        {
            crngRanges = loader?.CrngRanges;
            crngCounters = crngRanges != null ? new int[crngRanges.Length] : null;
            if (loader?.EmbeddedPalette != null)
            {
                // Take a working copy of the palette for cycling
                crngPalette = new Palette();
                System.Array.Copy(loader.EmbeddedPalette.red, crngPalette.red, 256);
                System.Array.Copy(loader.EmbeddedPalette.green, crngPalette.green, 256);
                System.Array.Copy(loader.EmbeddedPalette.blue, crngPalette.blue, 256);
            }
            else
            {
                crngPalette = null;
            }
        }

        /// <summary>
        /// Returns true if any CRNG ranges are active (rate > 0 with valid indices).
        /// </summary>
        static bool HasActiveCrng()
        {
            if (crngRanges == null || crngPalette == null) return false;
            for (int i = 0; i < crngRanges.Length; i++)
            {
                if (crngRanges[i].Rate > 0 && crngRanges[i].Low >= 0 &&
                    crngRanges[i].High > crngRanges[i].Low && crngRanges[i].High < 256)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Apply one tick of CRNG colour cycling to the working palette.
        /// Mirrors UpdatePaletteFadeTimers_ovr108_934 (line 438583):
        /// for each range with rate > 0, accumulate the counter and rotate
        /// when it overflows. RotatePaletteEntry_seg023_9 (line 98009)
        /// rotates forward: save first, shift entries down, wrap to end.
        /// </summary>
        static void ApplyCrngCycling()
        {
            if (crngRanges == null || crngPalette == null || crngCounters == null) return;
            for (int i = 0; i < crngRanges.Length; i++)
            {
                if (crngRanges[i].Rate <= 0) continue;
                int low = crngRanges[i].Low;
                int high = crngRanges[i].High;
                if (low < 0 || high < 0 || high >= 256 || low >= high) continue;

                crngCounters[i] += crngRanges[i].Rate;
                // The disasm accumulates rate per tick; rotate when counter >= threshold.
                // From DOSBox frame capture analysis (frames 425-750):
                //   CRNG 0 (rate 18): 1 step every 14 DOSBox frames at ~70fps = 5.0 steps/sec
                //   CRNG 3 (rate 14): 1 step every 17 DOSBox frames at ~70fps = 4.1 steps/sec
                // At PIT timer rate 18.2 Hz, threshold = 65 gives:
                //   rate 18: 18/65 * 18.2 = 5.04/sec ✓
                //   rate 14: 14/65 * 18.2 = 3.92/sec ✓
                while (crngCounters[i] >= 65)
                {
                    crngCounters[i] -= 65;
                    // Forward rotation: save first, shift down, wrap to end
                    // Matches RotatePaletteEntry_seg023_9 (line 98009)
                    Palette.cyclePalette(crngPalette, low, high - low + 1);
                }
            }
        }

        /// <summary>
        /// Escape: ends the cutscene unless its script has said not to (punt), and in UW2 never the ending
        /// (cutscene 2, gobble_input_events).
        /// </summary>
        public static void StopCutscene()
        {
            if (!puntAllowed || (_RES == GAME_UW2 && currentCutsceneNo == 2))
            {
                return;
            }
            cancelRequested = true;
            //MusicStreamPlayer.Instance?.Stop();
            //XMIMusic.CurrentlyPlayingThemeNo = 0;
        }

        /// <summary>
        /// Any other key or a mouse button during a cutscene: ends a pause, or starts skipping inside a skip.
        /// UW1 takes a button only 0x40 ticks (a quarter second) after the last input it took (show_anm).
        /// </summary>
        public static void InputReceived(bool isKey)
        {
            if (!IsPlaying)
            {
                return;
            }
            var now = Time.GetTicksMsec();
            if (_RES != GAME_UW2 && !isKey && now - lastInputMsec <= 250)
            {
                return;
            }
            lastInputMsec = now;
            inputPending = true;
        }

        /// <summary>
        /// fadein / fadeout: count * 8 even steps of 8 ticks (1/32 s) each from black to the picture or back, so count / 4
        /// seconds in all; 0 changes at once, as does a fade while frames are being skipped. What arrives meanwhile is
        /// swallowed (gobble_input_events).
        /// </summary>
        static IEnumerator Fade(TextureRect cutscontrol, bool fadeIn, int count)
        {
            var steps = skipping ? 0 : count << 3;
            for (int step = 1; step <= steps; step++)
            {
                if (cancelRequested)
                {
                    yield break;
                }
                var t = (float)step / steps;
                var level = fadeIn ? t : 1f - t;
                cutscontrol.Modulate = new Color(level, level, level, 1f);
                yield return new WaitForSeconds(8f / 256f);
            }
            var end = fadeIn ? 1f : 0f;
            cutscontrol.Modulate = new Color(end, end, end, 1f);
            inputPending = false;
        }

        /// <summary>
        /// install_timebased_task(task_palfade, period, total): a step every period units of 8 ticks of the clock, total
        /// steps in all, the last one the target itself (palette_fade: start + step * (end - start) / total).
        /// </summary>
        static Palette InterpolatedPalette()
        {
            var units = (long)(Time.GetTicksMsec() - palInterpStartMsec) * 256 / 1000 / 8;
            int totalSteps = System.Math.Max(1, palInterpTotal);
            int actualStep = (int)System.Math.Min(units / System.Math.Max(1, palInterpSpeed), totalSteps);
            var interpPal = new Palette();
            for (int i = 0; i < 256; i++)
            {
                int sr = palInterpSource.red[i], sg = palInterpSource.green[i], sb = palInterpSource.blue[i];
                int tr = palInterpTarget.red[i], tg = palInterpTarget.green[i], tb = palInterpTarget.blue[i];
                interpPal.red[i] = (byte)System.Math.Clamp(sr + (actualStep * (tr - sr)) / totalSteps, 0, 255);
                interpPal.green[i] = (byte)System.Math.Clamp(sg + (actualStep * (tg - sg)) / totalSteps, 0, 255);
                interpPal.blue[i] = (byte)System.Math.Clamp(sb + (actualStep * (tb - sb)) / totalSteps, 0, 255);
            }
            return interpPal;
        }

        /// <summary>The control the cutscene plays in, for a palette fade to redraw while a frame is held.</summary>
        static TextureRect activeControl;

        /// <summary>
        /// The palette tasks run on the clock (run_time_critical_things), so a fade goes on while a pause or a wait
        /// holds the frame: the frame on show is drawn again with the palette of the moment.
        /// </summary>
        static void RefreshPaletteFade()
        {
            if (!palInterpActive || palInterpSource == null || palInterpTarget == null || activeControl == null
                || cuts?.RawPixelData == null || vpScrollActive)
            {
                return;
            }
            var shown = FrameNo - 1;
            if (shown < 0 || shown >= cuts.RawPixelData.Length || cuts.RawPixelData[shown] == null)
            {
                return;
            }
            activeControl.Texture = ArtLoader.Image(
                databuffer: cuts.RawPixelData[shown],
                dataOffSet: 0,
                width: 320, height: 200,
                palette: InterpolatedPalette(),
                useAlphaChannel: false,
                useSingleRedChannel: false,
                crop: false);
        }

        /// <summary>The palette the frame shows now: a fade's or palrange's when one is running, else the file's.</summary>
        static Palette CurrentScriptPalette()
        {
            var from = palInterpActive && palInterpTarget != null ? palInterpTarget : cuts.EmbeddedPalette;
            var pal = new Palette();
            System.Array.Copy(from.red, pal.red, 256);
            System.Array.Copy(from.green, pal.green, 256);
            System.Array.Copy(from.blue, pal.blue, 256);
            return pal;
        }

        /// <summary>
        /// Writes the script's colours (three 6-bit VGA words each) into pal: the first entry and the count are the
        /// words at at, the colours follow from colours on.
        /// </summary>
        static void SetScriptColours(Palette pal, List<int> args, int at, int colours)
        {
            var first = args[at];
            var count = args[at + 1];
            for (int i = 0; i < count && first + i < 256 && colours + i * 3 + 2 < args.Count; i++)
            {
                pal.red[first + i] = (byte)((args[colours + i * 3] & 0x3F) << 2);
                pal.green[first + i] = (byte)((args[colours + i * 3 + 1] & 0x3F) << 2);
                pal.blue[first + i] = (byte)((args[colours + i * 3 + 2] & 0x3F) << 2);
            }
        }

        static bool SpeechPlaying => main.instance?.DigitalAudioPlayer?.Playing == true;

        /// <summary>
        /// Starts playing an animation file: input, skips and loops belong to the file they were set in.
        /// </summary>
        static void ResetFileState()
        {
            inputPending = false;
            lastInputMsec = Time.GetTicksMsec();
            skipping = false;
            skipToFrame = 0;
            loopRepeats = 0;
        }

        /// <summary>
        /// Holds the current frame (the pause loop of show_anm, cuts_run_pause) for seconds, 999 until input.
        /// Input ends the hold unless ignoreInput; UW1 also ignores input while speech plays. With
        /// untilSpeechEnds the hold first lasts until the speech is over, ignoring input, and the seconds count
        /// from then (UW1's wait with speech).
        /// </summary>
        static IEnumerator HoldFrame(int seconds, bool untilSpeechEnds = false, bool ignoreInput = false)
        {
            inputPending = false;
            var start = Time.GetTicksMsec();
            while (!cancelRequested)
            {
                if (untilSpeechEnds)
                {
                    if (SpeechPlaying)
                    {
                        inputPending = false;
                        RefreshPaletteFade();
                        yield return new WaitForSeconds(0.05f);
                        continue;
                    }
                    untilSpeechEnds = false;
                    start = Time.GetTicksMsec();
                }
                if (seconds != 999 && Time.GetTicksMsec() - start >= (ulong)seconds * 1000)
                {
                    break;
                }
                if (inputPending)
                {
                    inputPending = false;
                    if (!ignoreInput && !(_RES != GAME_UW2 && SpeechPlaying))
                    {
                        break;
                    }
                }
                RefreshPaletteFade();
                yield return new WaitForSeconds(0.05f);
            }
        }

        /// <summary>
        /// Loads and begins a cutscene
        /// </summary>
        /// <param name="CutsceneNo">The index number of the cutscene to play</param>
        /// <param name="callBackMethod">Function to call after the cutscene has played</param>
        public static void PlayCutscene(int CutsceneNo, CallBacks.CutsceneCallBack callBackMethod, bool useSingleRedChannel = false)
        {
            cancelRequested = false;
            crngRanges = null;
            crngCounters = null;
            crngPalette = null;
            if (CutsceneNo >= 256)
            {
                FullScreen = false;
            }
            else
            {
                FullScreen = true;
            }

            if (_RES == GAME_UW2)
            {//show_cutscene stops the digital sound effects first (punt_all_digi_fx; UW1's runcutscene does not)
                main.instance?.DigitalAudioPlayer?.Stop();
            }
            StringBlock = 0xC00 + CutsceneNo;
            currentCutsceneNo = CutsceneNo;
            puntAllowed = true;
            fadedIn = false;
            fadedOut = false;
            ResetFileState();

            // runcutscene / show_cutscene start a music theme for some cutscenes: UW1 theme 4 for 1 to 3,
            // UW2 theme 1 for the ending (2).
            if (_RES != GAME_UW2 && CutsceneNo >= 1 && CutsceneNo <= 3)
            {
                XMIMusic.LoadXMI(4);
            }
            else if (_RES == GAME_UW2 && CutsceneNo == 2)
            {
                XMIMusic.LoadXMI(1);
            }

            // Read the .N00 control file
            if (Loader.ReadStreamFile(
                System.IO.Path.Combine(
                    BasePath, "CUTS", GetsCutsceneFileName(CutsceneNo, 0)
                    ), out byte[] CutsData)
                )
            {
                commands = LoadCutsceneData(CutsData);
            }

            uimanager.EnableDisable(uimanager.instance.PanelMainMenu, false);

            // Start the cutscene
            _ = Coroutine.Run(
                RunCutscene(CutsceneNo: CutsceneNo, callBackMethod: callBackMethod, useSingleRedChannel: useSingleRedChannel),
                main.instance);
        }

        /// <summary>
        /// Build the panorama composite from mapped LBACK files.
        /// Horizontal panoramas (canvas width > 320): LBACKs side by side,
        /// composite width = 320 + vpStartX.
        /// Vertical panoramas (canvas height > 200): LBACKs stacked vertically.
        /// Uses the current LPF file's embedded palette to render indexed pixels.
        /// See CutsceneBitmap_ovr108_33E0 (line 446657) for LBACK loading,
        /// DrawBitmapScreen (line 446795) for VGA buffer placement.
        /// </summary>
        static void BuildPanoramaComposite()
        {
            vpIsHorizontal = vpCanvasWidth > 320;
            var pal = cuts.EmbeddedPalette;

            // Composite width: for horizontal, use the map-file position params
            // to determine width (rightmost LBACK position + 320).
            int compW = vpIsHorizontal ? vpCanvasWidth : vpCanvasWidth;
            int compH = vpCanvasHeight;

            var img = Godot.Image.Create(compW, compH, false, Godot.Image.Format.Rgb8);

            int runningY = 0;
            foreach (var mapping in vpFileMappings)
            {
                var (pos, extent, fileIdx, rawPixels) = mapping;
                if (rawPixels == null) continue;

                if (vpIsHorizontal)
                {
                    // Horizontal: each LBACK placed at its position parameter
                    // from the map-file command. Composite grows to fit.
                    int blitX = pos;
                    int totalW = System.Math.Max(img.GetWidth(), blitX + 320);
                    if (img.GetWidth() < totalW)
                    {
                        var newImg = Godot.Image.Create(totalW, compH, false, Godot.Image.Format.Rgb8);
                        newImg.BlitRect(img, new Rect2I(0, 0, img.GetWidth(), compH), Vector2I.Zero);
                        img = newImg;
                        vpCanvasWidth = totalW;
                        compW = totalW;
                    }
                    int blitW = System.Math.Min(320, compW - blitX);
                    int blitH = System.Math.Min(200, compH);
                    for (int y = 0; y < blitH; y++)
                        for (int x = 0; x < blitW; x++)
                        {
                            byte idx = rawPixels[y * 320 + x];
                            img.SetPixel(blitX + x, y, pal.ColorAtIndex(idx, false, false));
                        }
                }
                else
                {
                    // Vertical: stack LBACKs sequentially
                    int blitH = System.Math.Min(200, compH - runningY);
                    int blitW = System.Math.Min(320, compW);
                    for (int y = 0; y < blitH; y++)
                        for (int x = 0; x < blitW; x++)
                        {
                            byte idx = rawPixels[y * 320 + x];
                            img.SetPixel(x, runningY + y, pal.ColorAtIndex(idx, false, false));
                        }
                    runningY += 200;
                }
            }

            vpComposite = img;
            vpCompositeClean = (Godot.Image)img.Duplicate();
            Debug.Print($"  Composite: {compW}x{compH}, scroll={(vpIsHorizontal ? "horizontal" : "vertical")}");
        }

        /// <summary>
        /// Extract the visible viewport region from the panorama composite at the
        /// current scroll position. Implements the scroll formula from
        /// AnimateViewportScroll_ovr108_B8E (line 439098 in uw2_asm.asm):
        ///   pos = start + (frame+1) * delta * direction_table[index]
        /// The (frame+1) comes from `inc ax` at line 439116 before the multiply.
        /// Direction tables at dseg_67d6+0x1068 (DX) and +0x1070 (DY) (line 358318).
        /// VGA hardware wraps the CRT start address; we use linear equivalents.
        /// </summary>
        static Godot.Image GetScrollFrame(int totalFrame)
        {
            if (vpComposite == null) return null;

            int frameAdj = totalFrame + 1; // assembly uses frame+1 (ovr108_B9E, line 439116)
            int displayH = SceneDisplayH;

            Godot.Image region;
            if (vpIsHorizontal)
            {
                int scrollX = vpStartX + vpScrollDX * frameAdj;
                int maxScroll = vpComposite.GetWidth() - 320;
                int scrollPos = System.Math.Clamp(scrollX, 0, maxScroll);
                region = Godot.Image.Create(320, vpComposite.GetHeight(), false, vpComposite.GetFormat());
                region.BlitRect(vpComposite,
                    new Rect2I(scrollPos, 0, 320, vpComposite.GetHeight()),
                    Vector2I.Zero);
            }
            else
            {
                // Vertical scroll (vscreen_focus, do_pan): the view's top row is h - (focus_y + 1) of the virtual
                // screen, the first LBACK's top row being row 0, and do_pan moves focus_y by (frame + 1) * step * dy,
                // so the view moves the other way. CS000: focus 359, up: 0 to the bottom. CS002: focus 199, down: 160 to 0.
                int initialY = vpComposite.GetHeight() - (vpStartY + 1);
                int scrollPos = initialY - vpScrollDY * frameAdj;
                int maxScroll = vpComposite.GetHeight() - displayH;
                scrollPos = System.Math.Clamp(scrollPos, 0, maxScroll);
                region = Godot.Image.Create(320, displayH, false, vpComposite.GetFormat());
                region.BlitRect(vpComposite,
                    new Rect2I(0, scrollPos, 320, displayH),
                    Vector2I.Zero);
            }

            return region;
        }

        /// <summary>
        /// Overlay animated sprite pixels (cart, flags) onto a scroll viewport
        /// region. Uses the sprite LPF's per-frame RLE write mask so we only
        /// paint pixels explicitly written by the sprite animation, preserving
        /// the scrolling LBACK background under RLE Skip regions.
        ///
        /// (offX, offY) shift the sprite's draw position within the region. For
        /// horizontal scroll this is the stale-offset compensation for null-delta
        /// LPF frames (see the panorama-scroll block comment at the top of the
        /// file). For the unshifted call path it defaults to (0, 0).
        ///
        /// `advance` controls whether vpSpriteFrame increments — retained for
        /// the (currently unused) sub-tick path where multiple display frames
        /// render the same sprite frame during a finer pan interpolation.
        ///
        /// Also hosts the one-shot deferred-backdrop swap: on the first sprite
        /// frame whose RLE actually writes pixels we replace the LBACK000 half
        /// of the panorama with the next LPF's frame 0 (e.g. CS000.N04 for the
        /// cart scroll), removing the at-rest cart that was baked into LBACK.
        /// </summary>
        static void ApplySpriteOverlay(Godot.Image region, bool advance = true)
        {
            ApplySpriteOverlay(region, 0, 0, advance);
        }

        static void ApplySpriteOverlay(Godot.Image region, int offX, int offY, bool advance)
        {
            if (vpSpriteLoader == null || vpSpriteLoader.WriteMasks == null) return;
            if (vpSpriteFrame >= vpSpriteLoader.ImageCache.Length) return;

            var spriteTex = vpSpriteLoader.ImageCache[vpSpriteFrame];
            var mask = vpSpriteLoader.WriteMasks[vpSpriteFrame];
            if (spriteTex == null || mask == null) { if (advance) vpSpriteFrame++; return; }

            // Trigger deferred backdrop swap on first non-empty sprite frame.
            if (vpPendingBackdropSwap && !vpBackdropSwapped)
            {
                bool hasWrites = false;
                for (int i = 0; i < mask.Length; i++) { if (mask[i] != 0) { hasWrites = true; break; } }
                if (hasWrites)
                {
                    SwapBackdropToNextFile();
                    vpBackdropSwapped = true;
                    // Rebuild this frame's region from the swapped composite.
                    var rebuilt = GetScrollFrame(vpFrameOffset + vpSpriteFrame);
                    if (rebuilt != null)
                        region.BlitRect(rebuilt,
                            new Rect2I(0, 0, rebuilt.GetWidth(), rebuilt.GetHeight()),
                            Vector2I.Zero);
                }
            }

            var spriteImg = spriteTex.GetImage();
            int rw = region.GetWidth();
            int rh = region.GetHeight();
            int sw = spriteImg.GetWidth();
            int sh = spriteImg.GetHeight();
            int h = System.Math.Min(sh, rh);
            int w = System.Math.Min(sw, rw);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int rx = x + offX;
                    int ry = y + offY;
                    if (rx < 0 || rx >= rw || ry < 0 || ry >= rh) continue;
                    if (mask[y * sw + x] != 0)
                        region.SetPixel(rx, ry, spriteImg.GetPixel(x, y));
                }

            if (advance) vpSpriteFrame++;
        }

        // --- Debug: dump composited scroll frames for pixel-perfect comparison
        // against DOSBox captures. Off by default; flip DumpScrollFrames=true to
        // enable. Frames land at user://cuts_dump/CSnnn_Nxx/frame_YYY.png
        // (~/Library/Application Support/Godot/app_userdata/Underworld/cuts_dump
        // on macOS). Use with the pixel-diff scripts in the PR description.
        public static bool DumpScrollFrames = false;

        static void DumpFrame(Godot.Image img, int cutsNo, int fileExt, int totalFrame)
        {
            if (!DumpScrollFrames || img == null) return;
            string subdir = $"CS{cutsNo:D3}_N{fileExt:D2}";
            string dirAbs = Godot.ProjectSettings.GlobalizePath($"user://cuts_dump/{subdir}");
            Godot.DirAccess.MakeDirRecursiveAbsolute(dirAbs);
            img.SavePng($"{dirAbs}/frame_{totalFrame:D3}.png");
        }

        /// <summary>
        /// Replace the LBACK000 region of the panorama composite with the next
        /// LPF file's frame 0 content. Called on the first sprite frame that
        /// actually writes pixels, to remove the at-rest cart baked into
        /// LBACK000 so the animated sprite cart is the only cart visible.
        /// Horizontal scroll only; see the block comment at the top of the file.
        /// </summary>
        static void SwapBackdropToNextFile()
        {
            if (vpComposite == null) return;
            int nextExt = currentFileExt + 1;
            var backdropPath = System.IO.Path.Combine(
                BasePath, "CUTS", GetsCutsceneFileName(currentCutsceneNo, nextExt));
            if (!System.IO.File.Exists(backdropPath)) return;
            var backdrop = new CutsLoader(GetsCutsceneFileName(currentCutsceneNo, nextExt));
            var tex = backdrop.LoadImageAt(0);
            if (tex == null) return;
            var img = tex.GetImage();
            int blitH = System.Math.Min(img.GetHeight(), vpComposite.GetHeight());
            int blitW = System.Math.Min(320, vpComposite.GetWidth());
            vpComposite.BlitRect(img, new Rect2I(0, 0, blitW, blitH), Vector2I.Zero);
            if (vpCompositeClean != null)
                vpCompositeClean.BlitRect(img, new Rect2I(0, 0, blitW, blitH), Vector2I.Zero);
            Debug.Print($"  Backdrop swap: N{nextExt:D2} frame 0 -> composite[0:{blitW}]");
        }

        /// <summary>
        /// Build a 320x200 indexed-pixel "base" for sprite LPF decoding. The
        /// sprite LPFs (N03, N06) are authored as deltas against the backdrop
        /// pixels at their draw position; if we decoded them onto a black buffer
        /// their RLE Skip regions would leave voids. Feeding LBACK pixels as the
        /// decode base means Skip regions show LBACK content, matching the DOS
        /// engine's VGA screen where Skip opcodes leave existing pixels alone.
        /// For vertical scroll (vpStartX=0): base = first LBACK raw pixels.
        /// For horizontal scroll (vpStartX=70): base = LBACK000[70:] + LBACK001[:70].
        /// </summary>
        static byte[] BuildLbackBase()
        {
            if (vpFileMappings.Count == 0) return null;
            var lbackBase = new byte[320 * 200];
            int fx = vpStartX;

            if (vpIsHorizontal)
            {
                var raws = new System.Collections.Generic.List<byte[]>();
                foreach (var m in vpFileMappings)
                    raws.Add(m.rawPixels ?? new byte[64000]);

                for (int y = 0; y < 200; y++)
                    for (int c = 0; c < 320; c++)
                    {
                        int vgaX = fx + c;
                        int lbackIdx = vgaX / 320;
                        int localX = vgaX % 320;
                        if (lbackIdx < raws.Count)
                            lbackBase[y * 320 + c] = raws[lbackIdx][y * 320 + localX];
                    }
            }
            else
            {
                // Vertical: sprite covers full width at x=0, use first LBACK
                var firstRaw = vpFileMappings[0].rawPixels;
                if (firstRaw != null)
                    System.Array.Copy(firstRaw, lbackBase,
                        System.Math.Min(firstRaw.Length, lbackBase.Length));
            }
            return lbackBase;
        }

        /// <summary>
        /// Executes a single cutscene command. Returns the number of params consumed.
        /// </summary>
        public static IEnumerator ExecuteCommand(CutSceneCommand cmd, TextureRect cutscontrol, int CutsceneNo)
        {
            if (cancelRequested)
            {
                yield return new WaitOneFrame();
            }
            string paramlist = "";
            for (int p = 0; p < cmd.NoOfParams; p++)
            {
                paramlist += $"({cmd.functionParams[p]})";
            }
            Debug.Print($"  [{cmd.frame}] {cmd.FunctionName}: {paramlist}");

            switch (cmd.functionNo)
            {
                case 0: // txt: nothing while skipping; UW2 string FFFFh clears the subtitle
                    if (skipping)
                    {
                        break;
                    }
                    if (_RES == GAME_UW2 && cmd.functionParams[1] == 0xFFFF)
                    {
                        uimanager.instance.CutsSubtitle.Text = "";
                    }
                    else
                    {
                        uimanager.instance.CutsSubtitle.Text = $"[center]{GameStrings.GetDisplayString(StringBlock, cmd.functionParams[1])}[/center]";
                    }
                    break;

                case 1: // erase: clears the subtitle
                    uimanager.instance.CutsSubtitle.Text = "";
                    break;

                case 4: // skip: input from here skips to the frame. Speech is always available here, so the pause
                        // the original adds without speech never applies.
                    if (!skipping)
                    {
                        inputPending = false;
                        skipToFrame = cmd.functionParams[0];
                    }
                    break;

                case 7: // loop: replay the file up to this frame arg[0] more times
                    loopRepeats = cmd.functionParams[0];
                    loopFrame = cmd.frame;
                    break;

                case 6: // end-cutsc
                    uimanager.EnableDisable(cutscontrol, false);
                    uimanager.EnableDisable(uimanager.instance.CutsSubtitle, false);
                    break;

                case 8: // open-file
                    {
                        var extNo = cmd.functionParams[1];
                        var filePath = System.IO.Path.Combine(
                            BasePath, "CUTS", GetsCutsceneFileName(cmd.functionParams[0], extNo));

                        if (cmd.functionParams[0] == 996)
                        {
                            if (_RES == GAME_UW2)
                            {
                                Debug.Print("opening random guardian background.");
                                //when file no is 996 then a random file is opened from index 0x1C onwards (guardian taunts.
                                var randomfile = 0x1C + Rng.r.Next(4);
                                filePath = System.IO.Path.Combine(
                                    BasePath, "CUTS", GetsCutsceneFileName(randomfile, extNo));
                            }
                            else
                            {
                                Debug.Print("use of 996 open-file in uw1. fixme.");
                            }
                        }

                        Debug.Print($"  Open {GetsCutsceneFileName(cmd.functionParams[0], extNo)}");

                        if (System.IO.File.Exists(filePath))
                        {
                            cuts = new CutsLoader(filePath);
                            InitCrngCycling(cuts);
                            currentFileExt = extNo;
                            FrameNo = 0;
                            ResetFileState();
                        }
                        break;
                    }

                case 9: // fadeout: full screen only, once until the next fade-in
                    if (FullScreen && !fadedOut)
                    {
                        yield return Fade(cutscontrol, fadeIn: false, cmd.functionParams[0]);
                        fadedIn = false;
                        fadedOut = true;
                    }
                    break;

                case 10: // fadein: full screen only, once until the next fade-out
                    if (FullScreen && !fadedIn)
                    {
                        yield return Fade(cutscontrol, fadeIn: true, cmd.functionParams[0]);
                        fadedIn = true;
                        fadedOut = false;
                    }
                    break;

                case 11: // jump: skip ahead to the frame without drawing, unless it is the next one
                    if (cmd.frame != cmd.functionParams[0] - 1)
                    {
                        skipToFrame = cmd.functionParams[0] - 1;
                        skipping = true;
                    }
                    break;

                case 12: // punt: whether Escape may end the cutscene
                    puntAllowed = cmd.functionParams[0] != 0;
                    break;

                case 13: // text-play with audio
                    {
                        // Subtitle rendering: uses FONTBIG.SYS (font index 3),
                        // hardcoded via OpenFont(3) at ovr108_2E1A (line 445603).
                        // Text positioning from RenderCutsceneText_ovr108_157B (line 441321):
                        //   line spacing = font height (TextPos[bx+6], line 441390)
                        //   bottom margin = 2px (inc dx; inc dx at ovr108_15CE, lines 441401-441403)
                        //   word wrap at 320px, no inter-character spacing
                        // NOTE: game uses underscore-as-dot convention: "_." should render as " ."
                        // 999 is text only and UW2's 998 no speech: their subtitles go up at their frame, like
                        // txt (the text the original shows without speech). A say with its own speech waits
                        // for the speech before it to end: the original would not start it while another plays.
                        var speechFile = cmd.functionParams[2];
                        var hasSpeech = speechFile != 999 && !(_RES == GAME_UW2 && speechFile == 998);
                        if (!hasSpeech && skipping)
                        {
                            break;
                        }
                        var audioPlayer = main.instance?.DigitalAudioPlayer;
                        while (hasSpeech && audioPlayer != null && audioPlayer.Playing)
                        {
                            if (cancelRequested)
                            {
                                yield break;
                            }

                            RefreshPaletteFade();
                            yield return new WaitForSeconds(0.05f);
                        }

                        if ((short)cmd.functionParams[1] >= 0)
                        {
                            uimanager.instance.CutsSubtitle.Text = $"[center]{GameStrings.GetDisplayString(StringBlock, cmd.functionParams[1])}[/center]";
                        }
                        else
                        {
                            uimanager.instance.CutsSubtitle.Text = ""; // clear subtitle (0xFFFF = -1)
                        }
                        if (hasSpeech)
                        {
                            string vocfile;
                            if (_RES == GAME_UW2)
                            {
                                //in UW2 the cutscene audio is the guardian speech.
                                vocfile = System.IO.Path.Combine(
                                    BasePath, "SOUND",
                                    $"BSP{cmd.functionParams[2]:0#}.VOC");
                            }
                            else
                            {
                                vocfile = System.IO.Path.Combine(
                                    BasePath, "SOUND",
                                    $"{cmd.functionParams[2]:0#}.VOC");
                            }
                            var sound = vocLoader.Load(vocfile);
                            if (sound != null)
                            {
                                main.instance.DigitalAudioPlayer.Stream = sound.toWav();
                                main.instance.DigitalAudioPlayer.Play();
                            }
                        }
                        break;
                    }

                case 14: // wait: with speech (always available here) UW1 holds until the speech ends and then
                         // arg[1] seconds; UW2 holds arg[1] seconds ignoring input
                    if (!skipping)
                    {
                        if (_RES == GAME_UW2)
                        {
                            yield return HoldFrame(cmd.functionParams[1], ignoreInput: true);
                        }
                        else
                        {
                            yield return HoldFrame(cmd.functionParams[1], untilSpeechEnds: true);
                        }
                    }
                    break;

                case 15: // clang: UW1 plays sound effect 11h, UW2 nothing
                    if (_RES != GAME_UW2)
                    {
                        UWsoundeffects.PlaySoundEffectAtAvatar(UWsoundeffects.SoundEffectKlang, 0x40, 0);
                    }
                    break;

                // 16 palrange and 17 palfade are not used by any script of either game; they are kept as the original has them.
                case 16: // palrange(first, count, colours): set palette entries from the script
                    if (cuts?.EmbeddedPalette != null)
                    {
                        var pal = CurrentScriptPalette();
                        SetScriptColours(pal, cmd.functionParams, 0, 2);
                        palInterpSource = pal;
                        palInterpTarget = pal;
                        palInterpTotal = 1;
                        palInterpSpeed = 1;
                        palInterpStartMsec = Time.GetTicksMsec();
                        palInterpActive = true;
                    }
                    break;

                case 17: // palfade(period, total, first, count, colours): fade those entries to the script's colours
                    if (cuts?.EmbeddedPalette != null)
                    {
                        palInterpSource = CurrentScriptPalette();
                        palInterpTarget = CurrentScriptPalette();
                        SetScriptColours(palInterpTarget, cmd.functionParams, 2, 4);
                        palInterpSpeed = cmd.functionParams[0];
                        palInterpTotal = cmd.functionParams[1];
                        palInterpStartMsec = Time.GetTicksMsec();
                        palInterpActive = true;
                    }
                    break;

                case 19: // pal-interp — interpolate palette toward PALS.DAT target
                    {
                        // Cutscene_19_Unk_ovr108_1229 (line 440603):
                        //   param[0] = PALS.DAT index (loaded via OpenPalsData, line 440668)
                        //   param[1] = speed (timer delay — interpolate every N frames)
                        //   param[2] = total frames
                        // Source palette snapshot stored at [si+55CAh] (lines 440689-440699).
                        palInterpTargetIndex = cmd.functionParams[0];
                        palInterpSpeed = cmd.functionParams[1];
                        palInterpTotal = cmd.NoOfParams > 2 ? cmd.functionParams[2] : 80;
                        palInterpStartMsec = Time.GetTicksMsec();
                        // Snapshot current palette as source
                        if (cuts != null && cuts.EmbeddedPalette != null)
                        {
                            palInterpSource = new Palette();
                            for (int i = 0; i < 256; i++)
                            {
                                palInterpSource.red[i] = cuts.EmbeddedPalette.red[i];
                                palInterpSource.green[i] = cuts.EmbeddedPalette.green[i];
                                palInterpSource.blue[i] = cuts.EmbeddedPalette.blue[i];
                            }
                        }
                        // Load target palette directly from PALS.DAT file.
                        // Note: PaletteLoader.Palettes has a bug (uses palNo*256 instead
                        // of palNo*768 for record size), so we load directly here.
                        var palsPath = System.IO.Path.Combine(BasePath, "DATA", "PALS.DAT");
                        if (Loader.ReadStreamFile(palsPath, out byte[] palsData))
                        {
                            int palOffset = palInterpTargetIndex * 768; // 256 entries * 3 bytes
                            if (palOffset + 768 <= palsData.Length)
                            {
                                palInterpTarget = new Palette();
                                for (int i = 0; i < 256; i++)
                                {
                                    // VGA 6-bit to 8-bit: shift left 2 (same as PaletteLoader)
                                    palInterpTarget.red[i] = (byte)((palsData[palOffset + i * 3] & 0x3F) << 2);
                                    palInterpTarget.green[i] = (byte)((palsData[palOffset + i * 3 + 1] & 0x3F) << 2);
                                    palInterpTarget.blue[i] = (byte)((palsData[palOffset + i * 3 + 2] & 0x3F) << 2);
                                }
                                palInterpActive = true;
                            }
                        }
                        Debug.Print($"  Palette interp: target PALS.DAT[{palInterpTargetIndex}], speed={palInterpSpeed}, total={palInterpTotal}");
                        break;
                    }

                case 20: // viewport-setup
                    {
                        // Sets panorama canvas dimensions and subtitle offset.
                        // When canvas != 320x200, sets [si+4Fh] panorama mode flag
                        // (ovr108_12D3, line 440713).
                        vpCanvasWidth = cmd.functionParams[0];
                        vpCanvasHeight = cmd.functionParams[1];
                        SetSplit(cmd.NoOfParams > 2 ? cmd.functionParams[2] : 999, cutscontrol);
                        vpScrollActive = false;
                        vpFrameOffset = 0;
                        vpFileMappings.Clear();
                        vpComposite = null;
                        vpCompositeClean = null;
                        vpSpriteLoader = null;
                        vpSpriteFrame = 0;
                        Debug.Print($"  Viewport: {vpCanvasWidth}x{vpCanvasHeight} split={splitY}");
                        break;
                    }

                case 21: // set-start
                    {
                        vpStartX = cmd.functionParams[0];
                        vpStartY = cmd.NoOfParams > 1 ? cmd.functionParams[1] : 0;
                        Debug.Print($"  Start: x={vpStartX} y={vpStartY}");
                        // Build composite now so pre-scroll segment can display it
                        if (vpFileMappings.Count > 0 && cuts != null)
                            BuildPanoramaComposite();
                        break;
                    }

                case 22: // map-file — load LBACK background bitmap
                    {
                        // LBACK*.BYT files are raw 320x200 indexed pixel data (64000 bytes, no header).
                        // Loaded by CutsceneBitmap_ovr108_33E0 (line 446657) via LoadDATFile.
                        var fileIdx = cmd.functionParams[2];
                        var lbackName = $"LBACK{fileIdx:D3}.BYT";
                        var lbackPath = System.IO.Path.Combine(BasePath, "CUTS", lbackName);
                        byte[] rawPixels = null;
                        if (Loader.ReadStreamFile(lbackPath, out byte[] lbackData) && lbackData.Length >= 64000)
                        {
                            rawPixels = lbackData;
                        }
                        vpFileMappings.Add((cmd.functionParams[0], cmd.functionParams[1], fileIdx, rawPixels));
                        Debug.Print($"  Map: fileIdx={fileIdx} -> {lbackName}");
                        break;
                    }

                case 23: // start-scroll — direction table lookup
                    {
                        // Scroll direction from table (disassembly dseg_67d6+0x1068/0x1070)
                        // Index 0=Down, 1=Right, 2=Up, 3=Left
                        var tableIdx = cmd.functionParams[0];
                        var delta = cmd.functionParams[1];
                        if (tableIdx < 4)
                        {
                            vpScrollDX = ScrollDX[tableIdx] * delta;
                            vpScrollDY = ScrollDY[tableIdx] * delta;
                        }
                        vpScrollActive = true;
                        vpFrameOffset = 0;
                        string[] dirs = { "Down", "Right", "Up", "Left" };
                        Debug.Print($"  Scroll: {dirs[tableIdx % 4]}, delta={delta}, dx={vpScrollDX} dy={vpScrollDY}");

                        // Prepare sprite overlay: decode current LPF file in sprite mode
                        // with LBACK as base. Resets buffer to LBACK before each keyframe
                        // so RLE skip areas contain LBACK content. Write masks track which
                        // pixels the RLE explicitly wrote (animated flags/cart).
                        var lbackBase = BuildLbackBase();
                        if (lbackBase != null)
                        {
                            var spritePath = System.IO.Path.Combine(
                                BasePath, "CUTS", GetsCutsceneFileName(currentCutsceneNo, currentFileExt));
                            if (System.IO.File.Exists(spritePath))
                            {
                                vpSpriteLoader = new CutsLoader(
                                    GetsCutsceneFileName(currentCutsceneNo, currentFileExt), lbackBase);
                                vpSpriteFrame = 0;
                                Debug.Print($"  Sprite overlay: ext {currentFileExt} (LBACK base, per-keyframe reset)");
                            }
                        }

                        // Backdrop swap (horizontal only): LBACK000 has the cart at its
                        // rest position; the next LPF (N04) is a cart-less variant and
                        // N03 is the cart-only sprite overlay. N03's first 3 frames are
                        // all-Skip (no writes) so LBACK shows through unchanged — cart
                        // stays visible. When the sprite starts writing (frame 3+), the
                        // backdrop flips to N04 and the sprite picks up the cart from
                        // ~the same rest position, giving a seamless hand-off.
                        // Vertical scenes (N06) include erase writes so no swap needed.
                        vpPendingBackdropSwap = vpIsHorizontal;
                        vpBackdropSwapped = false;
                        vpSpriteStaleOffset = 0;
                        break;
                    }

                case 24: // splity: where the picture stops and the subtitle bar begins
                    SetSplit(cmd.functionParams[0], cutscontrol);
                    break;

                case 25: // music
                    Debug.Print($"  Music: theme {cmd.functionParams[0]}");
                    XMIMusic.LoadXMI((byte)cmd.functionParams[0]);
                    break;

                case 27: // wait_for_sound: wait up to arg[0] seconds for the speech, then cut it off
                    {
                        var audioPlayer = main.instance?.DigitalAudioPlayer;
                        var timeoutSec = (float)cmd.functionParams[0];
                        var elapsed = 0f;
                        while (audioPlayer != null && audioPlayer.Playing)
                        {
                            if (cancelRequested)
                            {
                                break;
                            }
                            if (elapsed >= timeoutSec)
                            {
                                audioPlayer.Stop();
                                break;
                            }

                            RefreshPaletteFade();
                            yield return new WaitForSeconds(0.05f);
                            elapsed += 0.05f;
                        }

                        break;
                    }

                case 3: // pause: hold this frame for arg[0] seconds (256 ticks of the 1/256 s clock) or until input;
                        // 999 until input. Nothing while skipping.
                    if (!skipping)
                    {
                        yield return HoldFrame(cmd.functionParams[0]);
                    }
                    break;

                default:
                    Debug.Print($"  Unimplemented cmd {cmd.functionNo} {cmd.FunctionName}");
                    break;
            }
        }

        static ShaderMaterial hdUpscaleMaterial;

        /// <summary>
        /// When HD art is on, cutscene frames are smoothed at display time by
        /// resources/shaders/cuts_upscale.gdshader instead of being replaced like the rest of the art,
        /// because the player composes, fades and colour-cycles them at the original resolution.
        /// Red-channel cutscenes are drawn through their own palette material, which is left alone,
        /// and so is any other material already on the control.
        /// </summary>
        /// <summary>
        /// show_cutscene's window for a cutscene of 100h or more, in the original's 320x200 with y the top row counted up
        /// from the bottom: UW1 (0x34, 0xB4, 0xAC by 0x70), UW2 (0x10, 0xB6, 0xD0 by 0x7F); 103h, the death, is 9 rows higher
        /// and 0xB taller in UW1, 4 and 5 in UW2. show_anm draws the frame's last w columns and h rows there (it skips 320 - w and 200 - h), so the
        /// whole 320x200 frame sits that far up and to the left of the window.
        /// </summary>
        public static void PlaceSmallWindow(TextureRect control, int cutsceneNo)
        {
            int x, y, w, h;
            if (_RES == GAME_UW2)
            {
                x = 0x10; y = 0xB6; w = 0xD0; h = 0x7F;
                if (cutsceneNo == 0x103)
                {
                    y += 4;
                    h += 5;
                }
            }
            else
            {
                x = 0x34; y = 0xB4; w = 0xAC; h = 0x70;
                if (cutsceneNo == 0x103)
                {
                    y += 9;
                    h += 0xB;
                }
            }
            //only the window shows: the control sits in a clipping parent of the window's size
            var clip = control.GetParent() as Control;
            if ((clip == null) || (clip.Name != "CutsSmallClip"))
            {
                var layer = control.GetParent();
                clip = new Control { Name = "CutsSmallClip", ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore };
                layer.AddChild(clip);
                layer.MoveChild(clip, control.GetIndex());
                control.Reparent(clip, false);
            }
            clip.Position = new Vector2(x * 4, (199 - y) * 4);
            clip.Size = new Vector2(w * 4, h * 4);
            control.Position = new Vector2(-(320 - w) * 4, -(200 - h) * 4);
        }

        static void ApplyHdUpscale(TextureRect control, bool useSingleRedChannel)
        {
            bool wanted = !useSingleRedChannel && !string.IsNullOrWhiteSpace(uwsettings.instance?.hdartpath);
            if (wanted)
            {
                hdUpscaleMaterial ??= new ShaderMaterial
                {
                    Shader = (Shader)ResourceLoader.Load("res://resources/shaders/cuts_upscale.gdshader")
                };
                if (control.Material == null || control.Material == hdUpscaleMaterial)
                {
                    control.Material = hdUpscaleMaterial;
                }
            }
            else if (hdUpscaleMaterial != null && control.Material == hdUpscaleMaterial)
            {
                control.Material = null;
            }
        }

        /// <summary>
        /// Main cutscene playback coroutine. Processes commands in segments.
        /// Each segment ends at a frame-set (func 5) or end-cutsc (func 6).
        /// </summary>
        public static IEnumerator RunCutscene(int CutsceneNo, CallBacks.CutsceneCallBack callBackMethod = null, bool useSingleRedChannel = false)
        {
            var OrigGameMode = uimanager.CurrentGameMode;
            uimanager.CurrentGameMode = uimanager.GameModes.CUTSCENE;
            MessageDisplay.WaitingForMore = false;
            Debug.Print($"Running cutscene {CutsceneNo}");
            IsPlaying = true;
            TextureRect cutscontrol;
            if (FullScreen)
                cutscontrol = uimanager.CutsFullscreen;
            else
            {
                cutscontrol = uimanager.CutsSmall;
                PlaceSmallWindow(cutscontrol, CutsceneNo);
            }

            activeControl = cutscontrol;
            if (FullScreen)
            {
                PlaceSubtitle(cutscontrol.Size.Y / 200f);
            }
            ApplyHdUpscale(cutscontrol, useSingleRedChannel);
            uimanager.EnableDisable(cutscontrol, true);
            uimanager.EnableDisable(uimanager.instance.CutsSubtitle, true);
            uimanager.instance.CutsSubtitle.Text = "";

            FrameNo = 0;
            currentFileExt = 1;
            vpScrollActive = false;

            // Splash screen sequence for the intro, matching the original engine's
            // SplashPart1_ovr112_36 (line 461013) → SplashPart2 (line 461467):
            //   1. Origin logo: BYT.ARK entry 6, palette 5 (LoadBitMap line 461220)
            //   2. LGS logo: BYT.ARK entry 7, palette 6 (LoadBitMap line 461269)
            //   3. CS011 animated title (PlayCutscene(9) from SplashPart2 line 461480)
            // CutsceneNo 9 = CS011 (octal 011, the title screen cutscene).
            // DOSBox capture: Origin at frame 1, LGS at frame 20, title at frame 89.
            if (CutsceneNo == 9 && _RES == GAME_UW2 && !cancelRequested)
            {
                cutscontrol.Modulate = new Color(1f, 1f, 1f, 1f);
                cutscontrol.Texture = uimanager.bitmaps.LoadImageAt(6); // Origin logo
                if (cancelRequested) goto cleanup;
                yield return new WaitForSeconds(2.0f);
                if (cancelRequested) goto cleanup;
                cutscontrol.Texture = null;
                yield return new WaitForSeconds(0.5f);
                if (cancelRequested) goto cleanup;
                cutscontrol.Texture = uimanager.bitmaps.LoadImageAt(7); // LGS logo
                yield return new WaitForSeconds(2.0f);
                if (cancelRequested) goto cleanup;
                cutscontrol.Texture = null;
                yield return new WaitForSeconds(0.5f);
                if (cancelRequested) goto cleanup;
            }
            else if (CutsceneNo == 9 && _RES != GAME_UW2 && !cancelRequested)
            {
                cutscontrol.Modulate = new Color(1f, 1f, 1f, 1f);
                cutscontrol.Texture = uimanager.bitmaps.LoadImageAt(5); // Origin logo
                if (cancelRequested) goto cleanup;
                yield return new WaitForSeconds(2.0f);
                if (cancelRequested) goto cleanup;
                if (_RES != GAME_UWDEMO)
                {
                    cutscontrol.Texture = null;
                    yield return new WaitForSeconds(0.5f);
                    if (cancelRequested) goto cleanup;
                    cutscontrol.Texture = uimanager.bitmaps.LoadImageAt(6); // Blue sky logo
                    yield return new WaitForSeconds(2.0f);
                    if (cancelRequested) goto cleanup;
                    cutscontrol.Texture = null;
                    yield return new WaitForSeconds(0.5f);
                    if (cancelRequested) goto cleanup;
                }
            }
            palInterpActive = false;

            // Load the first animation file (.N01)
            cuts = null;
            var firstFile = System.IO.Path.Combine(BasePath, "CUTS", GetsCutsceneFileName(CutsceneNo, 1));
            //check if the first command in the n01 file is not a change file command.
            if (commands[0] != null)
            {
                if (commands[0].functionNo == 8)
                {
                    if (commands[0].functionParams[0] == 996)
                    {
                        if (_RES == GAME_UW2)
                        {
                            Debug.Print("opening random guardian background.");
                            //when file no is 996 then a random file is opened from index 0x1C onwards (guardian taunts.
                            var randomfile = 0x1C + Rng.r.Next(4);
                            firstFile = System.IO.Path.Combine(
                                BasePath, "CUTS", GetsCutsceneFileName(randomfile, 1));
                        }
                        else
                        {
                            Debug.Print("use of 996 open-file in uw1. fixme.");
                        }
                    }
                }
            }
            if (System.IO.File.Exists(firstFile))
            {
                cuts = new CutsLoader(firstFile, useSingleRedChannel);
            }
            else
            {
                Debug.Print($"File not found {firstFile}");
            }
            InitCrngCycling(cuts);

            // Set initial frame to black. A cutscene in the 3D view's window never fades (cutsop_fadein and
            // cutsop_fadeout act on the full screen only).
            cutscontrol.Modulate = FullScreen ? new Color(0f, 0f, 0f, 1f) : new Color(1f, 1f, 1f, 1f);
            uimanager.FlashColour(
                colour: 0, targetControl: cutscontrol, duration: 1, IgnoreDelay: true);

            // Process commands in segments
            int cmdIndex = 0;
            bool cutsceneRunning = true;
            bool firstSegment = true;

            bool fileChanged = false;

            while (cutsceneRunning && !cancelRequested && cmdIndex < commands.Count)
            {
                if (cancelRequested)
                {
                    goto cleanup;
                }
                // Auto-advance to the next animation file (unless open-file changed it)
                if (!firstSegment && !fileChanged)
                {
                    int nextExt = currentFileExt + 1;
                    var nextFile = System.IO.Path.Combine(
                        BasePath, "CUTS", GetsCutsceneFileName(CutsceneNo, nextExt));
                    if (System.IO.File.Exists(nextFile))
                    {
                        cuts = new CutsLoader(nextFile);
                        InitCrngCycling(cuts);
                    }
                    currentFileExt = nextExt;
                    FrameNo = 0;
                    ResetFileState();
                }
                firstSegment = false;
                fileChanged = false;

                // Reset palette interpolation per segment (func 19 re-enables it).
                palInterpActive = false;

                // Collect this segment's commands
                var scheduledCmds = new List<CutSceneCommand>();
                var postCmds = new List<CutSceneCommand>();
                int segmentFrameCount = 0;
                bool hasFrameSet = false;
                bool isEndCutscene = false;

                int scanIndex = cmdIndex;
                while (scanIndex < commands.Count)
                {
                    var cmd = commands[scanIndex];
                    scanIndex++;

                    if (cmd.functionNo == 5) // frame-set
                    {
                        segmentFrameCount = cmd.frame;
                        hasFrameSet = true;
                        scheduledCmds.Add(cmd);
                        break;
                    }
                    if (cmd.functionNo == 6) // end-cutsc
                    {//cutsop_end stops the file at its frame, as next does, and ends the cutscene after it
                        if (cmd.frame > 0)
                        {
                            segmentFrameCount = cmd.frame;
                            hasFrameSet = true;
                        }
                        scheduledCmds.Add(cmd);
                        isEndCutscene = true;
                        break;
                    }
                    scheduledCmds.Add(cmd);
                }

                // Collect post-animation (frame=999) commands
                while (scanIndex < commands.Count && commands[scanIndex].frame == 999)
                {
                    postCmds.Add(commands[scanIndex]);
                    scanIndex++;
                }

                Debug.Print($"--- Segment: {scheduledCmds.Count} cmds, {postCmds.Count} post-cmds, {segmentFrameCount} frames, ext={currentFileExt} ---");

                // Scroll state: if this segment has no start-scroll command,
                // deactivate scrolling. The panorama composite persists (only
                // cleared by viewport-setup func 20) but the viewport stops moving.
                // Segments without start-scroll render LPF frames directly.
                bool hasScrollCmd = false;
                foreach (var cmd in scheduledCmds)
                    if (cmd.functionNo == 23) { hasScrollCmd = true; break; }
                if (!hasScrollCmd)
                    vpScrollActive = false;

                // show_anm plays the file's records one by one and stops early at next or end: a file runs out at its
                // last record (the closing loop-back delta not counted), and commands past it are never reached.
                if (hasFrameSet && cuts != null && !vpScrollActive && segmentFrameCount > cuts.ImageCache.Length)
                {
                    segmentFrameCount = cuts.ImageCache.Length;
                }

                // Execute the segment
                if (hasFrameSet && segmentFrameCount > 0)
                {
                    // Frame timing from the LPF header's rate (offset 0x44): show_anm waits until 0x100 / rate whole
                    // ticks of the 1/256 s clock have passed since the last frame.
                    float frameTime = 0.1f; // default 10fps fallback
                    if (cuts != null && cuts.FramesPerSecond > 0)
                    {
                        frameTime = (0x100 / cuts.FramesPerSecond) / 256f;
                    }

                    int consumedUpTo = -1;//a loop replays the frames without their commands, which have run
                    for (int frame = 0; frame < segmentFrameCount; frame++)
                    {
                        if (cancelRequested)
                        {
                            goto cleanup;
                        }

                        // Input after the frame inside a skip starts skipping and ends any loop; UW1 takes it
                        // only after a fade-in and not while speech plays. Skipping stops at its frame.
                        if (inputPending && _RES != GAME_UW2 && (SpeechPlaying || !fadedIn))
                        {
                            inputPending = false;
                        }
                        if (!skipping && inputPending && skipToFrame > frame)
                        {
                            inputPending = false;
                            skipping = true;
                            loopRepeats = 0;
                        }
                        if (skipToFrame != 0 && frame >= skipToFrame)
                        {
                            skipping = false;
                            skipToFrame = 0;
                        }

                        // Fire commands scheduled for this frame
                        foreach (var cmd in scheduledCmds)
                        {
                            if (cmd.frame == frame && cmd.functionNo != 5 && cmd.frame > consumedUpTo)
                            {
                                if (cancelRequested)
                                {
                                    goto cleanup;
                                }
                                yield return ExecuteCommand(cmd, cutscontrol, CutsceneNo);
                                if (cmd.functionNo == 8) // open-file
                                    fileChanged = true;
                            }
                        }

                        // loop: back to the first frame once this frame's commands have run
                        if (loopRepeats > 0 && frame == loopFrame)
                        {
                            loopRepeats--;
                            consumedUpTo = System.Math.Max(consumedUpTo, loopFrame);
                            FrameNo = 0;
                            frame = -1;
                            continue;
                        }

                        if (skipping)
                        {
                            FrameNo++;//played without being drawn
                            continue;
                        }

                        // Display animation frame.
                        // When panorama mode is active ([si+4Fh]!=0, set by func 20
                        // when canvas != 320x200), the normal DrawBitMap call is SKIPPED
                        // (ovr108_24AB, line 443935). Instead:
                        // 1. AnimateViewportScroll shifts the VGA CRT start address
                        // 2. LPF frame decoded into persistent buffer (delta chaining)
                        // 3. DrawArtToScreen draws at fixed screen position
                        // We simulate this by cropping the LBACK composite and
                        // overlaying sprite pixels via the LPF's RLE write mask.
                        // See the panorama-scroll block comment at the top of the
                        // file for the horizontal-scroll fixes layered on top.
                        if (vpScrollActive && vpComposite != null)
                        {
                            int totalFrame = vpFrameOffset + frame;
                            var scrollRegion = GetScrollFrame(totalFrame);
                            if (scrollRegion != null)
                            {
                                // Null-delta stale-offset: keep the sprite
                                // scene-aligned while the panorama pans between
                                // LPF keyframes. Resets on each LPF keyframe;
                                // increments on each null-delta frame. Offset
                                // direction is opposite the scroll delta so the
                                // sprite slides in the same direction the scene
                                // is moving.
                                int offX = 0, offY = 0;
                                if (vpSpriteLoader != null
                                    && vpSpriteLoader.IsKeyFrame != null
                                    && vpSpriteFrame < vpSpriteLoader.IsKeyFrame.Length)
                                {
                                    if (vpSpriteLoader.IsKeyFrame[vpSpriteFrame])
                                        vpSpriteStaleOffset = 0;
                                    else
                                        vpSpriteStaleOffset++;
                                    if (vpIsHorizontal)
                                        offX = -vpSpriteStaleOffset * vpScrollDX;
                                    else
                                        offY = vpSpriteStaleOffset * vpScrollDY;
                                }
                                ApplySpriteOverlay(scrollRegion, offX, offY, advance: true);
                                uimanager.DisplayScrollFrame(scrollRegion, cutscontrol);
                                DumpFrame(scrollRegion, CutsceneNo, currentFileExt, totalFrame);
                            }
                            FrameNo++; // advance LPF frame for delta chaining
                        }
                        else if (cuts != null)
                        {
                            if (FrameNo > cuts.ImageCache.GetUpperBound(0))
                                FrameNo = 0;

                            // Palette interpolation: re-render frame with interpolated palette.
                            // Linear interpolation from InterpolatePaletteRange_ovr108_32CC (line 446461):
                            //   result = source + (step * (target - source)) / total
                            if (palInterpActive && palInterpSource != null && palInterpTarget != null
                                && cuts.RawPixelData != null && FrameNo < cuts.RawPixelData.Length
                                && cuts.RawPixelData[FrameNo] != null)
                            {
                                var interpPal = InterpolatedPalette();

                                // Re-render frame with interpolated palette
                                var rerendered = ArtLoader.Image(
                                    databuffer: cuts.RawPixelData[FrameNo],
                                    dataOffSet: 0,
                                    width: 320, height: 200,
                                    palette: interpPal,
                                    useAlphaChannel: false,
                                    useSingleRedChannel: false,
                                    crop: false);
                                cutscontrol.Texture = rerendered;
                                FrameNo++;
                            }
                            else if (HasActiveCrng()
                                && cuts.RawPixelData != null && FrameNo < cuts.RawPixelData.Length
                                && cuts.RawPixelData[FrameNo] != null)
                            {
                                // CRNG colour cycling: the DOS engine updates VGA palette
                                // at the PIT timer rate (~18.2 Hz), independently of the
                                // LPF animation frame rate. We render multiple cycling
                                // ticks per LPF frame to match, then hold the last result.
                                byte[] pixelData = cuts.RawPixelData[FrameNo];
                                int cycleTicksPerFrame = System.Math.Max(1,
                                    (int)(18.2f / System.Math.Max(1, cuts.FramesPerSecond)));
                                float tickTime = frameTime / cycleTicksPerFrame;

                                for (int tick = 0; tick < cycleTicksPerFrame; tick++)
                                {
                                    if (cancelRequested)
                                    {
                                        goto cleanup;
                                    }
                                    ApplyCrngCycling();
                                    var rerendered = ArtLoader.Image(
                                        databuffer: pixelData,
                                        dataOffSet: 0,
                                        width: 320, height: 200,
                                        palette: crngPalette,
                                        useAlphaChannel: false,
                                        useSingleRedChannel: false,
                                        crop: false);
                                    cutscontrol.Texture = rerendered;
                                    if (tick < cycleTicksPerFrame - 1)
                                        yield return new WaitForSeconds(tickTime);
                                }
                                FrameNo++;
                            }
                            else
                            {
                                uimanager.DisplayCutsImage(
                                    cuts: cuts, imageNo: FrameNo++, targetControl: cutscontrol,
                                    cropHeight: SceneDisplayH, useSingleRedChannel: useSingleRedChannel);
                            }
                        }
                        if (cancelRequested)
                        {
                            goto cleanup;
                        }
                        yield return new WaitForSeconds(frameTime);
                        if (cancelRequested)
                        {
                            goto cleanup;
                        }
                    }

                    // Execute commands at the frame-set boundary frame number.
                    // These fire after the last displayed frame (frame-set frame
                    // is not displayed per DPaint LPF format). Typically fade or
                    // open-file commands that carry over into the next segment.
                    foreach (var cmd in scheduledCmds)
                    {
                        if (cmd.frame == segmentFrameCount && cmd.functionNo != 5)
                        {
                            yield return ExecuteCommand(cmd, cutscontrol, CutsceneNo);
                            if (cmd.functionNo == 8) // open-file
                                fileChanged = true;
                        }
                    }

                    if (vpScrollActive)
                        vpFrameOffset += segmentFrameCount;
                }
                else
                {
                    // No frame-set: commands have varying frame numbers.
                    // Use the max frame number as the segment length and
                    // interleave commands with frame display.
                    int maxFrame = 0;
                    foreach (var cmd in scheduledCmds)
                        if (cmd.frame < 999 && cmd.frame > maxFrame)
                            maxFrame = cmd.frame;

                    if (maxFrame > 0)
                    {
                        // Frame timing from the LPF rate (0x100 / rate ticks)
                        float frameTime = 0.1f;
                        if (cuts != null && cuts.FramesPerSecond > 0)
                            frameTime = (0x100 / cuts.FramesPerSecond) / 256f;

                        for (int frame = 0; frame <= maxFrame; frame++)
                        {
                            foreach (var cmd in scheduledCmds)
                            {
                                if (cmd.frame == frame)
                                    yield return ExecuteCommand(cmd, cutscontrol, CutsceneNo);
                            }

                            // Display frame
                            if (cuts != null)
                            {
                                if (FrameNo > cuts.ImageCache.GetUpperBound(0))
                                    FrameNo = 0;
                                if (FrameNo <= cuts.ImageCache.GetUpperBound(0))
                                {
                                    uimanager.DisplayCutsImage(
                                        cuts: cuts, imageNo: FrameNo++, targetControl: cutscontrol,
                                        cropHeight: SceneDisplayH, useSingleRedChannel: useSingleRedChannel);
                                }
                            }
                            yield return new WaitForSeconds(frameTime);
                            if (cancelRequested)
                            {
                                goto cleanup;
                            }
                        }
                    }
                    else
                    {
                        // All commands at frame 0: execute sequentially (blocking)
                        foreach (var cmd in scheduledCmds)
                        {
                            if (cancelRequested)
                            {
                                goto cleanup;
                            }
                            yield return ExecuteCommand(cmd, cutscontrol, CutsceneNo);
                            if (cancelRequested)
                            {
                                goto cleanup;
                            }
                            if (cmd.functionNo == 4) // to-frame (inline)
                            {
                                for (int i = 0; i < cmd.functionParams[0]; i++)
                                {
                                    if (cuts != null)
                                    {
                                        if (FrameNo > cuts.ImageCache.GetUpperBound(0))
                                            FrameNo = 0;
                                        uimanager.DisplayCutsImage(
                                            cuts: cuts, imageNo: FrameNo++, targetControl: cutscontrol, useSingleRedChannel: useSingleRedChannel);
                                    }
                                    if (cancelRequested)
                                    {
                                        goto cleanup;
                                    }
                                    yield return new WaitForSeconds(0.2f);
                                    if (cancelRequested)
                                    {
                                        goto cleanup;
                                    }
                                }
                            }
                            else if (cmd.functionNo == 7) // rep-seg
                            {
                                Debug.Print($"  Rep-seg: repeat {cmd.functionParams[0]} times");
                                //NOTE. From what it appears to do. 
                                // For the frame from the previous frame-set function. 
                                // Repeats that frame and frame + 1 , arg[0] times. Eg death skulls eye flashing                            
                            }
                        }
                    } // end else (all commands at frame 0)
                } // end else (no frame-set)

                // Execute post-animation (frame=999) commands with blocking
                foreach (var cmd in postCmds)
                {
                    if (cancelRequested)
                    {
                        goto cleanup;
                    }
                    yield return ExecuteCommand(cmd, cutscontrol, CutsceneNo);
                    if (cancelRequested)
                    {
                        goto cleanup;
                    }
                }

                if (isEndCutscene)
                {
                    cutsceneRunning = false;
                }

                cmdIndex = scanIndex;
            }

        cleanup:
            main.instance?.DigitalAudioPlayer?.Stop();//the speech ends with the cutscene (show_anm)
            IsPlaying = false;
            uimanager.CurrentGameMode = OrigGameMode;//restore gamemode before any new cutscenes start.
            uimanager.EnableDisable(cutscontrol, false);
            uimanager.EnableDisable(uimanager.instance.CutsSubtitle, false);

            if ((cancelRequested) && (_RES == GAME_UW2) && (CutsceneNo == 0))
            {
                // Escape was pressed — skip the chained callback (which would play
                // the next cutscene in the intro sequence) and return straight to
                // the main menu. 
                // Done for UW2 intro cutscene only. Otherwise always make sure to run the callback as it may be game critical. 
                // Eg endgame sequence or if a cutscene is interupted during normal gameplay.
                uimanager.ReturnToMainMenu();
                callBackMethod = null;
            }
            else if (callBackMethod != null)
            {
                callBackMethod();
                callBackMethod = null;
            }

            yield return new WaitOneFrame();
        }

        /// <summary>
        /// Used to support some windowed cutscenes that turn off the cameras.
        /// </summary>
        public static void RestoreViewPort()
        {
            uimanager.EnableDisable(uimanager.instance.uwviewport, true);
        }

    } // end class
} // end namespace
