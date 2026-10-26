using System.IO;
using Godot;

namespace Underworld
{
    /// <summary>
    /// Optional higher resolution replacements for wall/floor textures and critter frames.
    ///
    /// The files are made from the player's own data and are never shipped: by the launcher's
    /// "Make Pixel HD art" (HdArtGenerator, HdArtFonts), or outside the game (upscale-lab's export_hd.py). They stay indexed: every byte is still a palette
    /// index (textures) or an auxiliary palette nibble (critters), so the existing red-channel
    /// shaders and lightmaps apply to them unchanged and the lighting stays the original's.
    ///
    /// Each texture reports its original size through SetSizeOverride, so anything that sizes
    /// geometry from texture dimensions (NPC billboards use NPCSpriteScale * width) is unaffected.
    ///
    /// The scale is whatever the files hold (2x and 4x are exported), read from their sizes.
    ///
    /// File format (.uwhd): "UWHD", uint16 width, uint16 height, then width*height bytes row major.
    ///   {hdartpath}/{uw1|uw2}/tex/{W64|F32|T64}_{index:000}.uwhd  palette indices
    ///   {hdartpath}/{uw1|uw2}/crit/{page file}_{frame:000}.uwhd   nibble + 1, 0 = transparent
    ///   {hdartpath}/{uw1|uw2}/gr/{GR name}_{picture:000}.uwhd     palette indices, as GRLoader decodes them
    ///   {hdartpath}/{uw1|uw2}/font/{font name}.uwhd               glyph strip, ink coverage 0-255
    ///   {hdartpath}/{uw1|uw2}/weapon/WEAPON_{frame:000}.uwhd      4-bit values, 0 = transparent
    ///   {hdartpath}/{uw1|uw2}/byt/{name}.uwhd                     palette indices of a 320x200 screen
    /// </summary>
    public static class HdArt
    {
        static bool announced;

        static string GameFolder
        {
            get
            {
                if (string.IsNullOrWhiteSpace(uwsettings.instance?.hdartpath))
                {
                    return null;
                }
                switch (UWClass._RES)
                {
                    case UWClass.GAME_UW1: return Path.Combine(uwsettings.instance.HdArtPath(), "uw1");
                    case UWClass.GAME_UW2: return Path.Combine(uwsettings.instance.HdArtPath(), "uw2");
                    default: return null; //the demo has its own art files, none are exported for it.
                }
            }
        }

        /// <summary>Whether an HD art set is in use for this game.</summary>
        public static bool Active => GameFolder != null && Directory.Exists(GameFolder);

        static bool TryRead(string kind, string name, out byte[] data, out int width, out int height)
        {
            data = null; width = 0; height = 0;
            var folder = GameFolder;
            if (folder == null)
            {
                return false;
            }
            var path = Path.Combine(folder, kind, name + ".uwhd");
            if (!File.Exists(path))
            {
                return false;
            }
            if (!announced)
            {
                announced = true;
                GD.Print($"HdArt: using {folder}");
            }
            var raw = File.ReadAllBytes(path);
            if (raw.Length < 8 || raw[0] != 'U' || raw[1] != 'W' || raw[2] != 'H' || raw[3] != 'D')
            {
                return false;
            }
            width = raw[4] | (raw[5] << 8);
            height = raw[6] | (raw[7] << 8);
            if (raw.Length != 8 + width * height)
            {
                return false;
            }
            data = new byte[width * height];
            System.Array.Copy(raw, 8, data, 0, data.Length);
            return true;
        }

        static ImageTexture IndexTexture(byte[] indices, int width, int height, int logicalWidth, int logicalHeight)
        {
            var img = Godot.Image.CreateFromData(width, height, false, Godot.Image.Format.R8, indices);
            var tex = ImageTexture.CreateFromImage(img);
            tex.SetSizeOverride(new Vector2I(logicalWidth, logicalHeight));
            return tex;
        }

        /// <summary>
        /// Reads a replacement that must be a whole multiple (2 or more) of width x height.
        /// </summary>
        static bool TryScaled(string kind, string name, int width, int height, out byte[] data, out int scale)
        {
            scale = 0;
            if (width <= 0 || height <= 0 || !TryRead(kind, name, out data, out int w, out int h))
            {
                data = null;
                return false;
            }
            scale = w / width;
            return scale >= 2 && w == width * scale && h == height * scale;
        }

        /// <summary>
        /// The replacement bytes for picture <paramref name="index"/> of a .GR file: the same palette
        /// indices GRLoader hands to ArtLoader.Image, at <paramref name="scale"/> times the size.
        /// </summary>
        public static bool GrPicture(string grName, int index, int width, int height, out byte[] data, out int scale)
        {
            return TryScaled("gr", $"{grName}_{index:000}", width, height, out data, out scale);
        }

        /// <summary>
        /// The replacement for weapon frame <paramref name="index"/> of WEAPONS.GR (UW1) or WEAP.GR
        /// (UW2), placed on the weapon canvas the way WeaponsLoader places the original: nothing
        /// is drawn when the corner is off the canvas to the left or above (the original's loop never
        /// starts then), otherwise clipped at the right and bottom. The bytes are the frame's 4-bit
        /// values, 0 transparent, which WeaponsLoader turns into colours with the player's
        /// auxiliary palette. Returns null when there is no replacement.
        /// </summary>
        public static byte[] WeaponCanvas(int index, int frameWidth, int frameHeight, int canvasWidth, int canvasHeight, int x0, int y0, out int scale)
        {
            if (!TryScaled("weapon", $"WEAPON_{index:000}", frameWidth, frameHeight, out var data, out scale))
            {
                return null;
            }
            int w = frameWidth * scale, cw = canvasWidth * scale, ch = canvasHeight * scale;
            var canvas = new byte[cw * ch];
            if (x0 < 0 || y0 < 0)
            {
                return canvas;
            }
            for (int y = 0; y < frameHeight * scale && y0 * scale + y < ch; y++)
            {
                for (int x = 0; x < w && x0 * scale + x < cw; x++)
                {
                    canvas[(y0 * scale + y) * cw + x0 * scale + x] = data[y * w + x];
                }
            }
            return canvas;
        }

        /// <summary>
        /// The replacement for a 320x200 screen: a UW1 .BYT file by name (e.g. MAIN) or a UW2
        /// BYT.ARK block as BYT_nn, palette indices at <paramref name="scale"/> times the size.
        /// </summary>
        public static bool Screen(string name, out byte[] data, out int scale)
        {
            return TryScaled("byt", name, 320, 200, out data, out scale);
        }

        /// <summary>
        /// The replacement glyph strip of a .SYS font: every glyph cell side by side, as
        /// SysFontBuilder lays out its texture, ink coverage 0-255, at <paramref name="scale"/> times the size.
        /// </summary>
        public static bool FontStrip(string fontName, int width, int height, out byte[] ink, out int scale)
        {
            return TryScaled("font", fontName, width, height, out ink, out scale);
        }

        /// <summary>
        /// The replacement for texture <paramref name="index"/> of a .TR file, or null.
        /// </summary>
        public static ImageTexture Texture(string trFile, int index, int originalSize)
        {
            if (!TryRead("tex", $"{trFile}_{index:000}", out var data, out int w, out int h))
            {
                return null;
            }
            return IndexTexture(data, w, h, originalSize, originalSize);
        }

        /// <summary>
        /// The replacement for one critter frame, placed on its animation canvas the same way the
        /// original frame is (top left at x0, y0 of a canvasWidth x canvasHeight canvas, clipped),
        /// at the replacement's scale (any whole multiple of the original frame, e.g. 2 or 4).
        /// Returns null when there is no replacement or it is not a whole multiple of the frame.
        /// </summary>
        public static ImageTexture CritterFrame(string pageFile, int frame, byte[] auxpal, int frameWidth, int frameHeight, int canvasWidth, int canvasHeight, int x0, int y0)
        {
            if (frameWidth == 0 || frameHeight == 0 || !TryRead("crit", $"{pageFile}_{frame:000}", out var data, out int w, out int h))
            {
                return null;
            }
            int scale = w / frameWidth;
            if (scale < 2 || w != frameWidth * scale || h != frameHeight * scale)
            {
                return null;
            }
            int cw = canvasWidth * scale, ch = canvasHeight * scale;
            var canvas = new byte[cw * ch];
            for (int y = 0; y < h; y++)
            {
                int cy = y0 * scale + y;
                if (cy < 0 || cy >= ch) { continue; }
                for (int x = 0; x < w; x++)
                {
                    int cx = x0 * scale + x;
                    if (cx < 0 || cx >= cw) { continue; }
                    byte v = data[y * w + x];
                    canvas[cy * cw + cx] = v == 0 ? (byte)0 : auxpal[v - 1];
                }
            }
            return IndexTexture(canvas, cw, ch, canvasWidth, canvasHeight);
        }
    }
}
