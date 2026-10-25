using System;
using System.IO;
using Godot;

namespace Underworld
{
    /// <summary>
    /// The fonts of the in-game Pixel HD set (see HdArtGenerator), kept smooth: the text, the
    /// cutscene subtitles above all, stays high definition whatever the art style.
    ///
    /// FONT4X5P, FONT5X6P and FONT5X6I are redrawn with the Latin letters of the bundled CJK font
    /// (jf open huninn, the font of the Chinese text), each into its original cell: same advance,
    /// same baseline, the capitals as tall as the original's. FONT5X6I is slanted. FONTBIG, the
    /// calligraphic subtitle font, keeps its own letter shapes: its pixels are upscaled smoothly
    /// and given soft edges (SmoothStrip). Each strip is laid out as SysFontBuilder lays out its texture, ink
    /// coverage 0-255 (HdArt.FontStrip). Needs Godot, so it runs on the main thread.
    /// </summary>
    public static class HdArtFonts
    {
        static readonly string[] VectorFonts = { "FONT4X5P", "FONT5X6I", "FONT5X6P" };
        const float Slant = 0.2f; //FONT5X6I's italic, pixels to the right per pixel up

        /// <summary>Writes the four font strips of <paramref name="game"/> at <paramref name="scale"/> under <paramref name="setDir"/>/{game}/font.</summary>
        public static void Generate(string gameDir, string game, string setDir, int scale)
        {
            var data = Path.Combine(gameDir, "DATA");
            var outDir = Path.Combine(setDir, game, "font");
            var latin = CjkGlyphs.LoadPlain(Path.Combine(Localization.Root, "fonts", Localization.FontFileName));
            foreach (var name in VectorFonts)
            {
                var font = Read(data, name);
                if (font == null || latin == null) { continue; } //no strip: the game keeps the original font
                Write(outDir, name, font, scale, VectorStrip(font, latin, scale, name == "FONT5X6I"));
            }
            var big = Read(data, "FONTBIG");
            if (big != null)
            {
                Write(outDir, "FONTBIG", big, scale, SmoothStrip(big, scale));
            }
        }

        static SysFont Read(string data, string name)
        {
            var path = Path.Combine(data, name + ".SYS");
            if (!File.Exists(path))
            {
                foreach (var f in Directory.GetFiles(data))
                {
                    if (string.Equals(Path.GetFileName(f), name + ".SYS", StringComparison.OrdinalIgnoreCase)) { path = f; break; }
                }
            }
            return File.Exists(path) ? SysFont.Parse(File.ReadAllBytes(path)) : null;
        }

        static void Write(string outDir, string name, SysFont font, int s, byte[] ink)
        {
            HdArtGenerator.WriteUwhd(Path.Combine(outDir, name + ".uwhd"),
                new HdArtGenerator.Picture(font.CellWidth * font.GlyphCount * s, font.Height * s, ink));
        }

        /// <summary>
        /// Each printable glyph drawn with <paramref name="latin"/> at <paramref name="s"/> times the cell:
        /// the baseline under the original capitals' lowest inked row, the capitals filling the rows
        /// above it, squeezed to the declared advance less one when wider, centred in that width.
        /// </summary>
        static byte[] VectorStrip(SysFont font, FontFile latin, int s, bool italic)
        {
            int cell = font.CellWidth, height = font.Height, w = cell * font.GlyphCount * s;
            var outp = new byte[w * height * s];
            int baseRow = 0;
            foreach (var ch in "AHEX")
            {
                for (int r = 0; r < height; r++)
                    for (int c = 0; c < cell; c++)
                        if (font.PixelAt(ch, r, c)) { baseRow = Math.Max(baseRow, r + 1); }
            }
            //size the font so a capital H is baseRow * s pixels tall
            var probe = CjkGlyphs.Rasterize(latin, "H", 100, out _);
            if (probe == null) { return outp; }
            int top = -1, bottom = -1;
            for (int y = 0; y < probe.GetLength(0); y++)
                for (int x = 0; x < probe.GetLength(1); x++)
                    if (probe[y, x] > 0.5f) { if (top < 0) { top = y; } bottom = y; }
            if (top < 0) { return outp; }
            int size = Mathf.Max(1, Mathf.RoundToInt(100f * baseRow * s / (bottom - top + 1)));

            for (int c = 33; c < Math.Min(font.GlyphCount, 127); c++)
            {
                var ink = CjkGlyphs.Rasterize(latin, ((char)c).ToString(), size, out int ascent);
                if (ink == null) { continue; }
                int ih = ink.GetLength(0), iw = ink.GetLength(1);
                if (italic) { ink = Shear(ink, ascent); iw = ink.GetLength(1); }
                int left = iw, right = -1;
                for (int x = 0; x < iw; x++)
                    for (int y = 0; y < ih; y++)
                        if (ink[y, x] > 0) { left = Math.Min(left, x); right = Math.Max(right, x); }
                if (right < 0) { continue; }
                int gw = right - left + 1;
                int room = Math.Max(s, (font.AdvanceOf(c) - 1) * s);
                int outW = Math.Min(gw, room);
                int x0 = Math.Max(0, (room - outW) / 2);
                for (int y = 0; y < ih; y++)
                {
                    int oy = baseRow * s - ascent + y;
                    if (oy < 0 || oy >= height * s) { continue; }
                    for (int x = 0; x < outW && x0 + x < cell * s; x++)
                    {
                        //squeezed horizontally when wider than the room (box filter over the source columns)
                        float a0 = left + x * (float)gw / outW, a1 = left + (x + 1) * (float)gw / outW, sum = 0;
                        for (float a = a0; a < a1 - 1e-4f;)
                        {
                            int col = (int)a;
                            float next = Math.Min(a1, col + 1);
                            sum += ink[y, Math.Min(col, iw - 1)] * (next - a);
                            a = next;
                        }
                        float v = sum / (a1 - a0);
                        outp[oy * w + c * cell * s + x0 + x] = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255), 0, 255);
                    }
                }
            }
            return outp;
        }

        /// <summary>Slants <paramref name="ink"/> to the right above its baseline row (and left below it).</summary>
        static float[,] Shear(float[,] ink, int baseline)
        {
            int h = ink.GetLength(0), w = ink.GetLength(1);
            int pad = Mathf.CeilToInt(h * Slant) + 1;
            var outp = new float[h, w + pad * 2];
            for (int y = 0; y < h; y++)
            {
                float shift = (baseline - y) * Slant + pad;
                int whole = (int)Math.Floor(shift);
                float frac = shift - whole;
                for (int x = 0; x < w; x++)
                {
                    float v = ink[y, x];
                    if (v == 0) { continue; }
                    outp[y, x + whole] += v * (1 - frac);
                    outp[y, Math.Min(x + whole + 1, w + pad * 2 - 1)] += v * frac;
                }
            }
            return outp;
        }

        /// <summary>
        /// Each glyph's own pixels at <paramref name="s"/> times with smooth outlines: every output pixel
        /// weighs the 0/1 source pixels around it with a Gaussian (SmoothSigma source pixels; outside the
        /// cell is blank, so neighbours do not run together), then a soft threshold around the middle
        /// rounds the steps of the diagonals and curves and anti-aliases the edges.
        /// </summary>
        static byte[] SmoothStrip(SysFont font, int s)
        {
            const float SmoothSigma = 0.55f, Low = 0.40f, High = 0.56f; //chosen by eye on the UW1 intro subtitles
            int cell = font.CellWidth, height = font.Height, w = cell * font.GlyphCount * s;
            var outp = new byte[w * height * s];
            float Pixel(int ch, int r, int c) => r < 0 || c < 0 || r >= height || c >= cell ? 0f : font.PixelAt(ch, r, c) ? 1f : 0f;
            for (int ch = 0; ch < font.GlyphCount; ch++)
            {
                for (int y = 0; y < height * s; y++)
                {
                    float sy = (y + 0.5f) / s - 0.5f;
                    int r0 = (int)Math.Floor(sy);
                    for (int x = 0; x < cell * s; x++)
                    {
                        float sx = (x + 0.5f) / s - 0.5f;
                        int c0 = (int)Math.Floor(sx);
                        float sum = 0, weights = 0;
                        for (int dy = -2; dy <= 2; dy++)
                        {
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                float ey = sy - (r0 + dy), ex = sx - (c0 + dx);
                                float wgt = Mathf.Exp(-(ey * ey + ex * ex) / (2 * SmoothSigma * SmoothSigma));
                                sum += wgt * Pixel(ch, r0 + dy, c0 + dx);
                                weights += wgt;
                            }
                        }
                        float t = Mathf.Clamp((sum / weights - Low) / (High - Low), 0f, 1f);
                        outp[y * w + ch * cell * s + x] = (byte)Mathf.RoundToInt(t * t * (3 - 2 * t) * 255);
                    }
                }
            }
            return outp;
        }
    }
}
