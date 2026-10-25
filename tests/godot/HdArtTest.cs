using System;
using System.IO;
using Godot;
using Underworld;

/// <summary>
/// Proves that HD art is wired in without moving or recolouring anything.
///
/// It loads every wall/floor texture and every critter frame twice: with the original art and
/// with an HD set made by pixel duplication (upscale-lab's export_hd.py nearest2x or nearest4x).
/// Duplication adds no information, so every HD texture must report the original size, be an
/// exact whole multiple of it underneath (the same multiple throughout), and give back the
/// original bytes when every s-th pixel is taken. Anything else
/// means a frame was placed on its canvas at the wrong spot, a nibble went through the wrong
/// auxiliary palette, or a texture came from the wrong index.
///
/// Critters are checked with every auxiliary palette the game actually assigns them (ASSOC.ANM in
/// UW1, AS.AN plus the stone palette 3 in UW2), because the HD frames store nibbles and only
/// become palette indices at load time. A palette number the page does not have is not tested:
/// the loader would read past the palette table into other data.
///
/// Needs the game data and UW_HDTEST_NEAREST set to a nearest2x or nearest4x folder (holding uw1/ and uw2/);
/// reports SKIP otherwise. Runs for whichever game settings.json selects.
/// </summary>
public partial class HdArtTest : Node
{
    private int _failures;
    private int _checked;
    private int _hdSeen;
    private int _scale;//the HD set's scale, from the first texture; every other must match
    private void Fail(string m) { if (_failures++ < 20) { GD.PrintErr("FAIL " + m); } }

    public override void _Ready() => Callable.From(Run).CallDeferred();

    private void Run()
    {
        _ = uwsettings.instance;
        string hdPath = OS.GetEnvironment("UW_HDTEST_NEAREST");
        string data = Path.Combine(UWClass.BasePath ?? "", "DATA");
        if (string.IsNullOrWhiteSpace(hdPath) || !Directory.Exists(hdPath) || !Directory.Exists(data)
            || UWClass._RES == UWClass.GAME_UWDEMO)
        {
            GD.Print("GODOT-TEST-VERDICT SKIP needs UW1 or UW2 game data and UW_HDTEST_NEAREST");
            GetTree().Quit(0); return;
        }
        string saved = uwsettings.instance.hdartpath;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            CheckTextures(hdPath);
            GD.Print($"textures done in {clock.Elapsed.TotalSeconds:0.0}s");
            CheckCritters(hdPath);
            GD.Print($"critters done in {clock.Elapsed.TotalSeconds:0.0}s");
            CheckObjects(hdPath);
            GD.Print($"objects done in {clock.Elapsed.TotalSeconds:0.0}s");
            CheckFonts(hdPath);
            GD.Print($"fonts done in {clock.Elapsed.TotalSeconds:0.0}s");
            CheckWeapons(hdPath);
            GD.Print($"weapons done in {clock.Elapsed.TotalSeconds:0.0}s");
            CheckScreens(hdPath);
            GD.Print($"screens done in {clock.Elapsed.TotalSeconds:0.0}s");
        }
        catch (Exception e)
        {
            Fail($"exception {e}");
        }
        finally
        {
            uwsettings.instance.hdartpath = saved;
        }
        if (_hdSeen == 0)
        {
            Fail("no HD texture was loaded at all, so nothing was tested");
        }
        GD.Print($"checked {_checked} textures, {_hdSeen} of them HD at {_scale}x");
        GD.Print(_failures == 0 ? "GODOT-TEST-VERDICT PASS" : $"GODOT-TEST-VERDICT FAIL {_failures} mismatches");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private void Compare(string what, ImageTexture orig, ImageTexture hd)
    {
        if (orig == null && hd == null) { return; }
        if (orig == null || hd == null) { Fail($"{what}: one side is missing"); return; }
        _checked++;
        if (hd.GetWidth() != orig.GetWidth() || hd.GetHeight() != orig.GetHeight())
        {
            Fail($"{what}: reports {hd.GetWidth()}x{hd.GetHeight()}, original {orig.GetWidth()}x{orig.GetHeight()}");
            return;
        }
        CompareImages(what, orig.GetImage(), hd.GetImage());
    }

    /// <summary>
    /// The HD image must be the original at a whole multiple, in the same pixel format (R8 for the
    /// red-channel shaders, RGB8 or RGBA8 for UI art), giving back the original's bytes when every
    /// s-th pixel is taken.
    /// </summary>
    private void CompareImages(string what, Image oi, Image hi)
    {
        if (hi.GetWidth() == oi.GetWidth())
        {
            Fail($"{what}: not replaced by HD art");
            return;
        }
        _hdSeen++;
        int s = hi.GetWidth() / oi.GetWidth();
        if (s < 2 || hi.GetWidth() != oi.GetWidth() * s || hi.GetHeight() != oi.GetHeight() * s
            || hi.GetFormat() != oi.GetFormat())
        {
            Fail($"{what}: HD is {hi.GetWidth()}x{hi.GetHeight()} {hi.GetFormat()}, original {oi.GetWidth()}x{oi.GetHeight()} {oi.GetFormat()}");
            return;
        }
        if (_scale == 0) { _scale = s; }
        if (s != _scale) { Fail($"{what}: scale {s}, other textures {_scale}"); return; }
        byte[] o = oi.GetData(), h = hi.GetData();
        int w = oi.GetWidth(), hw = hi.GetWidth(), bpp = o.Length / (w * oi.GetHeight());
        for (int y = 0; y < oi.GetHeight(); y++)
        {
            for (int x = 0; x < w; x++)
            {
                for (int k = 0; k < bpp; k++)
                {
                    byte hv = h[((y * s) * hw + x * s) * bpp + k], ov = o[(y * w + x) * bpp + k];
                    if (hv != ov)
                    {
                        Fail($"{what}: pixel ({x},{y}) byte {k} is {hv}, original {ov}");
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Every picture of every .GR file GRLoader reads (objects and UI; WEAPONS.GR is WeaponsLoader's),
    /// both as the 3D view loads them (red channel) and as the UI does (colour with alpha), because
    /// HD pictures are built through the same path. Files a game does not have are skipped.
    /// </summary>
    private void CheckObjects(string hdPath)
    {
        for (int file = GRLoader.ThreeDWIN_GR; file <= GRLoader.GHED_GR; file++)
        {
            if (file == GRLoader.WEAPONS_GR) { continue; }
            if (!File.Exists(Path.Combine(UWClass.BasePath, "DATA", GrName(file) + ".GR"))) { continue; }
            foreach (bool red in new[] { true, false })
            {
                uwsettings.instance.hdartpath = "";
                var orig = new GRLoader(file, GRLoader.GRShaderMode.None) { UseRedChannel = red };
                uwsettings.instance.hdartpath = hdPath;
                var hd = new GRLoader(file, GRLoader.GRShaderMode.None) { UseRedChannel = red };
                byte[] grData = File.ReadAllBytes(Path.Combine(UWClass.BasePath, "DATA", GrName(file) + ".GR"));
                for (int i = 0; i < orig.ImageCache.Length; i++)
                {
                    //type 0xA pictures (the cursors, among others) have no HD replacement by design
                    int at = grData[3 + i * 4] | (grData[4 + i * 4] << 8) | (grData[5 + i * 4] << 16) | (grData[6 + i * 4] << 24);
                    if (at < grData.Length && grData[at] == 0xA) { continue; }
                    uwsettings.instance.hdartpath = "";
                    ImageTexture o;
                    try
                    {
                        o = orig.LoadImageAt(i);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        //PANELS.GR's last panel is truncated in both games, so the original loader
                        //reads past the end of the file; the game never asks for it
                        continue;
                    }
                    uwsettings.instance.hdartpath = hdPath;
                    Compare($"GR {file} picture {i} red {red}", o, hd.LoadImageAt(i));
                }
            }
        }
    }

    private static string GrName(int file)
    {
        foreach (var f in typeof(GRLoader).GetFields())
        {
            if (f.IsLiteral && f.FieldType == typeof(int) && (int)f.GetRawConstantValue() == file && f.Name.EndsWith("_GR"))
            {
                return f.Name == "ThreeDWIN_GR" ? "3DWIN" : f.Name.Substring(0, f.Name.Length - 3);
            }
        }
        return "";
    }

    /// <summary>
    /// The weapon frames, with two of the player's auxiliary palettes, because the HD frames hold
    /// the 4-bit values and are coloured at load time.
    /// </summary>
    private void CheckWeapons(string hdPath)
    {
        foreach (int aux in new[] { 0, 1 })
        {
            uwsettings.instance.hdartpath = "";
            var orig = new WeaponsLoader(aux);
            uwsettings.instance.hdartpath = hdPath;
            var hd = new WeaponsLoader(aux);
            //as WeaponsLoader counts them: the file's count in UW1, a fixed 230 in UW2
            byte[] gr = File.ReadAllBytes(Path.Combine(UWClass.BasePath, "DATA", UWClass._RES == UWClass.GAME_UW2 ? "WEAP.GR" : "WEAPONS.GR"));
            int count = UWClass._RES == UWClass.GAME_UW2 ? 230 : gr[1] | (gr[2] << 8);
            for (int i = 0; i < count; i++)
            {
                Compare($"weapon {i} aux {aux}", orig.LoadImageAt(i), hd.LoadImageAt(i));
            }
        }
    }

    /// <summary>The 320x200 screens (UW1 .BYT files, UW2 BYT.ARK), with and without alpha.</summary>
    private void CheckScreens(string hdPath)
    {
        int count = UWClass._RES == UWClass.GAME_UW2 ? 11 : 9;//UW1's tenth entry is the demo's
        foreach (bool alpha in new[] { true, false })
        {
            for (int i = 0; i < count; i++)
            {
                uwsettings.instance.hdartpath = "";
                ImageTexture o;
                try
                {
                    o = new BytLoader().LoadImageAt(i, alpha);
                }
                catch (IndexOutOfRangeException)
                {
                    //UW2's BYT.ARK blocks 3 and 10 are empty; the original loader reads past the
                    //end of the file for them, and the game never asks for them
                    continue;
                }
                uwsettings.instance.hdartpath = hdPath;
                Compare($"screen {i} alpha {alpha}", o, new BytLoader().LoadImageAt(i, alpha));
            }
        }
    }

    /// <summary>
    /// The four fonts: every glyph keeps its advance, box and offset exactly, the UV rect covers the
    /// same glyph at the HD scale, and the HD texture gives back the original's pixels.
    /// </summary>
    private void CheckFonts(string hdPath)
    {
        var key = new Vector2I(SysFontBuilder.DesignSize, 0);
        foreach (string name in SysFontProvider.FontNames)
        {
            var sys = SysFont.Parse(File.ReadAllBytes(Path.Combine(UWClass.BasePath, "DATA", name + ".SYS")));
            var (a, d) = SysFontProvider.LegacyMetricsFor(name);
            uwsettings.instance.hdartpath = "";
            var orig = SysFontBuilder.Build(sys, a, d, name);
            uwsettings.instance.hdartpath = hdPath;
            var hd = SysFontBuilder.Build(sys, a, d, name);
            _checked++;
            CompareImages($"font {name}", orig.GetTextureImage(0, key, 0), hd.GetTextureImage(0, key, 0));
            for (int c = 0; c < sys.GlyphCount; c++)
            {
                if (hd.GetGlyphAdvance(0, SysFontBuilder.DesignSize, c) != orig.GetGlyphAdvance(0, SysFontBuilder.DesignSize, c)
                    || hd.GetGlyphSize(0, key, c) != orig.GetGlyphSize(0, key, c)
                    || hd.GetGlyphOffset(0, key, c) != orig.GetGlyphOffset(0, key, c))
                {
                    Fail($"font {name} glyph {c}: metrics differ");
                }
                var ou = orig.GetGlyphUVRect(0, key, c);
                if (hd.GetGlyphUVRect(0, key, c) != new Rect2(ou.Position * _scale, ou.Size * _scale))
                {
                    Fail($"font {name} glyph {c}: UV rect {hd.GetGlyphUVRect(0, key, c)}, expected {_scale}x {ou}");
                }
            }
        }
    }

    private void CheckTextures(string hdPath)
    {
        int count = UWClass._RES == UWClass.GAME_UW2 ? 256 : 262;//UW1: 210 walls then 52 floors
        uwsettings.instance.hdartpath = "";
        var origLoader = new TextureLoader(true);
        var orig = new ImageTexture[count];
        for (int i = 0; i < count; i++) { orig[i] = origLoader.LoadImageAt(i); }
        uwsettings.instance.hdartpath = hdPath;
        var hdLoader = new TextureLoader(true);
        for (int i = 0; i < count; i++) { Compare($"texture {i}", orig[i], hdLoader.LoadImageAt(i)); }
    }

    private void CheckCritters(string hdPath)
    {
        string crit = Path.Combine(UWClass.BasePath, "CRIT");
        var pairs = new System.Collections.Generic.HashSet<(int file, int aux)>();
        if (UWClass._RES == UWClass.GAME_UW2)
        {
            byte[] assoc = File.ReadAllBytes(Path.Combine(crit, "AS.AN"));
            byte[] pgmp = File.ReadAllBytes(Path.Combine(crit, "PG.MP"));
            byte[] cran = File.ReadAllBytes(Path.Combine(crit, "CR.AN"));
            for (int k = 0; k < 64; k++)
            {
                int file = assoc[k * 2], aux = assoc[k * 2 + 1];
                if (file == 255) { continue; }
                pairs.Add((file, aux));
                pairs.Add((file, 3));//the stone version, as ReadUW2AssocFile loads it
            }
            foreach (var (file, aux) in pairs)
            {
                CompareCritter($"UW2 critter file {file} aux {aux}", hdPath,
                    () => new CritterArt(file, PaletteLoader.Palettes[0], aux, pgmp, cran));
            }
        }
        else
        {
            byte[] assoc = File.ReadAllBytes(Path.Combine(crit, "ASSOC.ANM"));
            for (int k = 0; k < 64; k++)
            {
                pairs.Add((assoc[256 + k * 2], assoc[256 + k * 2 + 1]));
            }
            foreach (var (file, aux) in pairs)
            {
                CompareCritter($"UW1 critter file {file} aux {aux}", hdPath,
                    () => new CritterArt(file, PaletteLoader.Palettes[0], aux));
            }
        }
        GD.Print($"critters: {pairs.Count} file and palette pairs");
    }

    private void CompareCritter(string what, string hdPath, Func<CritterArt> make)
    {
        uwsettings.instance.hdartpath = "";
        var orig = make();
        uwsettings.instance.hdartpath = hdPath;
        var hd = make();
        for (int i = 0; i < orig.animSprites.Length; i++)
        {
            Compare($"{what} frame {i}", orig.animSprites[i], hd.animSprites[i]);
        }
    }
}
