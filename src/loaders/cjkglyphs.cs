using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Godot;

namespace Underworld
{
    /// <summary>
    /// Adds the Chinese characters of translated text (see Localization) to the four game fonts.
    ///
    /// The game fonts are fixed size bitmap fonts with a line box hand-tuned for the original
    /// glyphs (SysFontProvider.LegacyMetricsFor), so a fallback font would bring its own size and
    /// line height into every label. Instead each character is drawn with the CJK font when a
    /// translation first uses it and copied into the game font as one more bitmap glyph, sized
    /// and placed in the font's own units. Labels keep the original metrics and line boxes.
    ///
    /// A character is an em half a row shorter than the Latin glyphs (see EmFor), with the
    /// em's bottom on their bottom row. Any taller and the conversation box, whose lines are
    /// closer together than the glyphs' line box, overlaps them; the half row is for looks. Its bitmap has TexelsPerPixel
    /// texels per source pixel, whatever the HD art scale, because the original 4 to 7 rows
    /// cannot carry a Chinese character at 1x.
    ///
    /// The Latin glyphs are solid strokes a whole source pixel wide; the CJK font's are a fraction
    /// of one and antialiased, so on the game's brown panels they read faint. The strokes are
    /// therefore emboldened. (A dark outline baked into the glyphs was tried and rejected: it
    /// muddied the characters.)
    ///
    /// A proper name's English after its Chinese, "伊歐洛(Iolo)", is drawn small: the game's
    /// Latin glyphs are a whole em tall and outweigh the Chinese beside them. ShrinkGlosses moves
    /// those characters to the private use area at SmallBase + the character, and they are drawn
    /// from the CJK font, SmallScale of the em, standing on the Latin baseline.
    /// </summary>
    public static class CjkGlyphs
    {
        const int TexelsPerPixel = 8;
        const int AtlasSize = 2048;
        /// <summary>FONT4X5P's 4 rows are too few for a Chinese character to read.</summary>
        const int MinEm = 5;
        /// <summary>FreeType's emboldening of the CJK strokes.</summary>
        const float Embolden = 0.6f;

        /// <summary>How much shorter than its glyphs each font's Chinese is, in source pixels.</summary>
        const float Shrink = 0.5f;

        /// <summary>The private use characters standing for the small Latin of a gloss, one per ASCII character.</summary>
        const int SmallBase = 0xE000;
        /// <summary>The size of a gloss's Latin, as a share of the Chinese em.</summary>
        const float SmallScale = 0.8f;

        /// <summary>
        /// The em in source pixels for the font <paramref name="name"/>, <paramref name="height"/> rows high.
        /// FONT5X6I is only the stats panel's, where the skill names read better a row smaller
        /// than its glyphs.
        /// </summary>
        internal static float EmFor(string name, int height) =>
            (name == "FONT5X6I" ? height - 1 : Mathf.Max(height, MinEm)) - Shrink;

        class Target
        {
            public FontFile Font;
            public int Size; //the CJK font size the glyphs are drawn at, Em * TexelsPerPixel
            public bool Bold; //drawn with the emboldened strokes
            public float Shift; //added to each glyph's y offset to put the em's bottom on the Latin bottom row
            public int SmallSize; //the CJK font size a gloss's Latin is drawn at
            public readonly List<Image> Atlases = new();
            public int X, Y, RowHeight;
        }

        static readonly List<Target> targets = new();
        /// <summary>Characters already added, so fonts attached later can be given them too.</summary>
        static readonly HashSet<int> added = new();
        /// <summary>The CJK font for the game fonts, emboldened, and plain for most pictures (Rasterize).</summary>
        static FontFile bold, plain;
        static string sourcePath;

        /// <summary>Not in the game fonts, which stop at 126 (FONT4X5P at 96).</summary>
        static bool NeedsGlyph(char c) => c > 0x7E;

        /// <summary>Chinese characters and punctuation, which are wider than a Latin letter.</summary>
        public static bool IsWide(char c) => c >= 0x2000;

        /// <summary>A character of a gloss's small Latin (ShrinkGlosses).</summary>
        public static bool IsSmall(char c) => c >= SmallBase && c < SmallBase + 0x80;

        static readonly Regex Gloss = new(@"(?<=[\u2000-\uFFFF])\([A-Za-z0-9 '.,&\-]+\)");

        /// <summary>
        /// <paramref name="text"/> with each English gloss in parentheses straight after Chinese,
        /// "(Iolo)" in "伊歐洛(Iolo)", parentheses included, as the small Latin characters.
        /// </summary>
        public static string ShrinkGlosses(string text) =>
            text.IndexOf('(') < 0 ? text : Gloss.Replace(text, m =>
            {
                var small = new StringBuilder(m.Length);
                foreach (var c in m.Value) { small.Append((char)(SmallBase + c)); }
                return small.ToString();
            });

        /// <summary>Forgets the fonts, before they are built again for another game.</summary>
        public static void Reset()
        {
            targets.Clear();
            added.Clear();
        }

        static FontFile Source(bool emboldened = true)
        {
            var path = Localization.FontPath;
            if (path == null)
            {
                return null;
            }
            if (bold == null || plain == null || sourcePath != path)
            {
                bold = Load(path, Embolden);
                plain = Load(path, 0);
                sourcePath = path;
            }
            return emboldened ? bold : plain;
        }

        static FontFile Load(string path, float embolden)
        {
            var f = new FontFile
            {
                Antialiasing = TextServer.FontAntialiasing.Gray,
                Hinting = TextServer.Hinting.None,
                SubpixelPositioning = TextServer.SubpixelPositioning.Disabled,
                GenerateMipmaps = false,
                Oversampling = 1f,
            };
            if (f.LoadDynamicFont(path) != Error.Ok)
            {
                GD.PrintErr($"CjkGlyphs: {path} could not be loaded");
                return null;
            }
            // Cache 0 is the only one used. Not TextServer.FontSetEmbolden: wrapping the text server
            // in C# crashes Godot 4.3 on quit, when it is released after C# is finalised (issue #78).
            f.SetEmbolden(0, embolden);
            return f;
        }

        /// <summary>
        /// Lets translated text use <paramref name="f"/>, a game font SysFontBuilder has just
        /// built from the font <paramref name="name"/> (null when unnamed), <paramref name="height"/>
        /// rows high with <paramref name="descent"/> of them below the baseline; <paramref name="bold"/>
        /// false keeps the CJK font's own strokes.
        /// </summary>
        public static void Attach(FontFile f, string name, int height, float descent, bool bold = true)
        {
            var src = Source(bold);
            if (src == null)
            {
                return;
            }
            float em = EmFor(name, height);
            int size = Mathf.RoundToInt(em * TexelsPerPixel);
            float asc = src.GetAscent(size);
            float desc = src.GetDescent(size);
            // FONT5X6I's digits stand on the baseline, above its descent row, so on the stats
            // panel ("第1級") the Chinese stands there too or the digits look raised.
            float bottom = name == "FONT5X6I" ? 0 : descent;
            var t = new Target
            {
                Font = f,
                Size = size,
                Bold = bold,
                Shift = bottom - em * desc / (asc + desc),
                SmallSize = Mathf.RoundToInt(em * SmallScale * TexelsPerPixel),
            };
            targets.RemoveAll(x => x.Font == f);
            targets.Add(t);
            Render(t, added);
        }

        /// <summary>Adds the characters of <paramref name="text"/> that are not in the fonts yet.</summary>
        public static void Ensure(string text)
        {
            List<int> missing = null;
            foreach (var c in text)
            {
                if (NeedsGlyph(c) && !added.Contains(c))
                {
                    added.Add(c);
                    (missing ??= new List<int>()).Add(c);
                }
            }
            if (missing == null)
            {
                return;
            }
            foreach (var t in targets)
            {
                Render(t, missing);
            }
        }

        /// <summary>
        /// One line of text drawn with the CJK font at <paramref name="size"/> pixels, as ink
        /// coverage 0..1 indexed [y, x]: the line's advance wide and the font's ascent plus
        /// descent high, with the baseline <paramref name="ascent"/> rows down. Null without the font.
        /// Used to letter the pictures that have English painted into them (ZhImages); <paramref name="bold"/>
        /// draws with the emboldened strokes of the game fonts.
        /// </summary>
        public static float[,] Rasterize(string line, int size, out int ascent, bool bold = false)
        {
            ascent = 0;
            var src = Source(emboldened: bold);
            if (src == null)
            {
                return null;
            }
            var key = new Vector2I(size, 0);
            ascent = Mathf.CeilToInt(src.GetAscent(size));
            int height = ascent + Mathf.CeilToInt(src.GetDescent(size));
            var glyphs = new List<(int gi, float x)>();
            float pen = 0;
            foreach (var c in line)
            {
                int gi = (int)src.GetGlyphIndex(size, c, 0);
                glyphs.Add((gi, pen));
                pen += src.GetGlyphAdvance(0, size, gi).X;
            }
            var ink = new float[height, Mathf.Max(1, Mathf.CeilToInt(pen))];
            foreach (var (gi, x) in glyphs)
            {
                if (gi == 0) { continue; }
                src.RenderGlyph(0, key, gi);
                var uv = src.GetGlyphUVRect(0, key, gi);
                if (uv.Size.X == 0) { continue; }
                var offset = src.GetGlyphOffset(0, key, gi);
                var img = src.GetTextureImage(0, key, src.GetGlyphTextureIdx(0, key, gi));
                int ox = Mathf.RoundToInt(x + offset.X), oy = ascent + Mathf.RoundToInt(offset.Y);
                for (int v = 0; v < (int)uv.Size.Y; v++)
                {
                    for (int u = 0; u < (int)uv.Size.X; u++)
                    {
                        int px = ox + u, py = oy + v;
                        if (px < 0 || py < 0 || py >= height || px >= ink.GetLength(1)) { continue; }
                        ink[py, px] = Mathf.Max(ink[py, px], img.GetPixel((int)uv.Position.X + u, (int)uv.Position.Y + v).A);
                    }
                }
            }
            return ink;
        }

        static void Render(Target t, IEnumerable<int> chars)
        {
            var src = Source(t.Bold);
            if (src == null)
            {
                return;
            }
            var key = new Vector2I(SysFontBuilder.DesignSize, 0);
            float k = 1f / TexelsPerPixel;
            var touched = new HashSet<int>();
            foreach (var c in chars)
            {
                bool small = IsSmall((char)c);
                int ch = small ? c - SmallBase : c;
                int fontSize = small ? t.SmallSize : t.Size;
                float shift = small ? 0 : t.Shift; //Latin stands on the baseline
                var srcKey = new Vector2I(fontSize, 0);
                int gi = (int)src.GetGlyphIndex(fontSize, ch, 0);
                if (gi == 0)
                {
                    continue; //not in the CJK font either; stays a gap
                }
                src.RenderGlyph(0, srcKey, gi);
                var uv = src.GetGlyphUVRect(0, srcKey, gi);
                var offset = src.GetGlyphOffset(0, srcKey, gi);
                var size = src.GetGlyphSize(0, srcKey, gi);
                var advance = src.GetGlyphAdvance(0, fontSize, gi);
                int w = (int)uv.Size.X, h = (int)uv.Size.Y;

                t.Font.SetGlyphAdvance(0, SysFontBuilder.DesignSize, c, new Vector2(advance.X * k, 0));
                t.Font.SetGlyphOffset(0, key, c, new Vector2(offset.X * k, offset.Y * k + shift));
                t.Font.SetGlyphSize(0, key, c, size * k);
                if (w == 0 || h == 0)
                {
                    continue; //blank, like the ideographic space: only the advance matters
                }
                if (t.X + w > AtlasSize)
                {
                    t.X = 0;
                    t.Y += t.RowHeight;
                    t.RowHeight = 0;
                }
                if (t.Atlases.Count == 0 || t.Y + h > AtlasSize)
                {
                    var atlas = Image.CreateEmpty(AtlasSize, AtlasSize, false, Image.Format.Rgba8);
                    atlas.Fill(new Color(1, 1, 1, 0));
                    t.Atlases.Add(atlas);
                    t.X = 0;
                    t.Y = 0;
                    t.RowHeight = 0;
                }
                int page = t.Atlases.Count - 1;
                //white with the coverage as alpha, the same as the Latin strip
                var glyphs = src.GetTextureImage(0, srcKey, src.GetGlyphTextureIdx(0, srcKey, gi));
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float a = glyphs.GetPixel((int)uv.Position.X + x, (int)uv.Position.Y + y).A;
                        t.Atlases[page].SetPixel(t.X + x, t.Y + y, new Color(1, 1, 1, a));
                    }
                }

                //texture 0 is the Latin glyph strip
                t.Font.SetGlyphUVRect(0, key, c, new Rect2(t.X, t.Y, w, h));
                t.Font.SetGlyphTextureIdx(0, key, c, page + 1);
                touched.Add(page);
                t.X += w + 1;
                t.RowHeight = Mathf.Max(t.RowHeight, h + 1);
            }
            foreach (var page in touched)
            {
                t.Font.SetTextureImage(0, key, page + 1, t.Atlases[page]);
            }
            if (touched.Count > 0)
            {
                // Setting a texture image makes a new texture and frees the old one, which labels
                // already drawn still point at, so they would show their Chinese as solid boxes
                // until something else redraws them. Changed has every label using the font redraw.
                t.Font.EmitChanged();
            }
        }
    }
}
