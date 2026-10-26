using System;
using Godot;

namespace Underworld
{
    /// <summary>
    /// Smooth, high resolution redraws of the two cursors that are on screen most, the crosshair
    /// (CURSORS.GR 0) and the ranged weapon's target ring (9), used with the HD art. The cursors
    /// are 4-bit pictures (type 0xA) the HD sets do not replace, and at 4x and more their one-pixel
    /// steps show; any upscale of so few pixels either keeps the steps or blurs them, so these are
    /// drawn again as shapes measured off the original: where the centre is, how long the arms
    /// are, the outline colour, the fill colours along an arm, the ring's colours by direction.
    /// Both games' cursors are read the same way. Each comes with its hot spot, the original
    /// pixel the cursor points with.
    /// </summary>
    public static class HdCursors
    {
        const int S = 16; //output pixels per original pixel

        /// <summary>The redraw of cursor <paramref name="index"/> from its original <paramref name="original"/>, or null for the cursors not redrawn.</summary>
        public static ImageTexture Make(int index, Texture2D original, out Vector2 hotspot)
        {
            hotspot = Vector2.Zero;
            if (original == null || (index != 0 && index != 9))
            {
                return null;
            }
            var image = original.GetImage();
            if (image.IsCompressed())
            {
                image.Decompress();
            }
            image.Convert(Image.Format.Rgba8);
            int w = image.GetWidth(), h = image.GetHeight();
            var px = new Color[h, w];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y, x] = image.GetPixel(x, y);
            var outp = Image.CreateEmpty(w * S, h * S, false, Image.Format.Rgba8);
            bool ok = index == 0 ? Crosshair(px, w, h, outp, out hotspot) : Ring(px, w, h, outp, out hotspot);
            if (!ok)
            {
                return null;
            }
            var tex = ImageTexture.CreateFromImage(outp);
            tex.SetSizeOverride(new Vector2I(w, h));
            return tex;
        }

        /// <summary>The centre of the lowest picture pixel furthest left: the tip of the map's quills, which point down to the left.</summary>
        public static Vector2 Tip(Texture2D cursor)
        {
            var image = cursor.GetImage();
            if (image.IsCompressed())
            {
                image.Decompress();
            }
            for (int y = image.GetHeight() - 1; y >= 0; y--)
            {
                for (int x = 0; x < image.GetWidth(); x++)
                {
                    if (Opaque(image.GetPixel(x, y)))
                    {
                        return new Vector2(x + 0.5f, y + 0.5f);
                    }
                }
            }
            return cursor.GetSize() / 2;
        }

        static bool Opaque(Color c) => c.A > 0.5f;
        static bool Dark(Color c) => c.R + c.G + c.B < 0.25f;

        /// <summary>Coverage of a pixel by a shape at signed distance <paramref name="d"/> (original pixels) from its edge.</summary>
        static float Cover(float d) => Mathf.Clamp(0.5f - d * S, 0f, 1f);

        static void Over(ref Color dst, Color c, float a)
        {
            if (a <= 0) { return; }
            dst = new Color(dst.R * (1 - a) + c.R * a, dst.G * (1 - a) + c.G * a, dst.B * (1 - a) + c.B * a, dst.A * (1 - a) + a);
        }

        static bool Bounds(Color[,] px, int w, int h, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = w; y0 = h; x1 = -1; y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (Opaque(px[y, x])) { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
            return x1 >= 0;
        }

        /// <summary>
        /// Four arms, each an outlined capsule from where its outline starts to its tip, filled with
        /// the original's colours along it, and the centre: a dot of the centre pixel's colour, or a
        /// small outline ring when the centre is see-through (UW2).
        /// </summary>
        static bool Crosshair(Color[,] px, int w, int h, Image outp, out Vector2 hotspot)
        {
            hotspot = Vector2.Zero;
            if (!Bounds(px, w, h, out int x0, out int y0, out int x1, out int y1)) { return false; }
            int cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, len = x1 - cx;
            if (len < 3 || cy < 1) { return false; }
            int outlineFrom = -1;
            Color outline = Colors.Black;
            for (int d = 1; d <= len; d++)
            {
                var side = px[cy - 1, cx + d];
                if (Opaque(side) && Dark(side)) { outlineFrom = d; outline = side; break; }
            }
            var fillAt = new System.Collections.Generic.List<int>();
            for (int d = 1; d <= len; d++)
            {
                var c = px[cy, cx + d];
                if (Opaque(c) && !Dark(c)) { fillAt.Add(d); }
            }
            if (outlineFrom < 0 || fillAt.Count == 0) { return false; }
            var centre = px[cy, cx];
            for (int oy = 0; oy < h * S; oy++)
            {
                for (int ox = 0; ox < w * S; ox++)
                {
                    float fx = (ox + 0.5f) / S - 0.5f - cx, fy = (oy + 0.5f) / S - 0.5f - cy;
                    var c = new Color(0, 0, 0, 0);
                    foreach (var (ax, ay) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        float s = fx * ax + fy * ay, t = Math.Abs(fx * ay - fy * ax); //along the arm, across it
                        if (s <= 0) { continue; }
                        //outline 1.5 either side of the arm's middle, its ends where the original's outline starts and ends
                        Over(ref c, outline, Cover(Capsule(s, t, outlineFrom + 1f, len - 1f, 1.5f)));
                        float f0 = fillAt[0] - 0.1f, f1 = fillAt[^1] + 0.1f;
                        Over(ref c, FillAt(px, cy, cx, fillAt, s), Cover(Capsule(s, t, f0, f1, 0.55f)));
                    }
                    float r = Mathf.Sqrt(fx * fx + fy * fy);
                    if (Opaque(centre))
                    {
                        Over(ref c, centre, Cover(r - 0.6f));
                    }
                    else
                    {
                        Over(ref c, outline, Cover(Math.Abs(r - 1f) - 0.35f));
                    }
                    outp.SetPixel(ox, oy, c);
                }
            }
            hotspot = new Vector2(cx + 0.5f, cy + 0.5f);
            return true;
        }

        /// <summary>Distance from a capsule (segment <paramref name="a"/> to <paramref name="b"/> along the arm, radius <paramref name="r"/>).</summary>
        static float Capsule(float s, float t, float a, float b, float r)
        {
            float sc = Mathf.Clamp(s, a, Math.Max(a, b));
            return Mathf.Sqrt((s - sc) * (s - sc) + t * t) - r;
        }

        /// <summary>The original's fill colour <paramref name="s"/> along the right arm, blended between its pixels.</summary>
        static Color FillAt(Color[,] px, int cy, int cx, System.Collections.Generic.List<int> fillAt, float s)
        {
            if (s <= fillAt[0]) { return px[cy, cx + fillAt[0]]; }
            if (s >= fillAt[^1]) { return px[cy, cx + fillAt[^1]]; }
            int i = 0;
            while (fillAt[i + 1] < s) { i++; }
            float f = (s - fillAt[i]) / (fillAt[i + 1] - fillAt[i]);
            return px[cy, cx + fillAt[i]].Lerp(px[cy, cx + fillAt[i + 1]], f);
        }

        /// <summary>
        /// A coloured band between two outline rings at the original's ring radius, its colour by
        /// direction from the original (averaged over the nearest coloured pixels, so it shades
        /// round the ring rather than in steps), and a small outline ring in the middle.
        /// </summary>
        static bool Ring(Color[,] px, int w, int h, Image outp, out Vector2 hotspot)
        {
            hotspot = Vector2.Zero;
            if (!Bounds(px, w, h, out int x0, out int y0, out int x1, out int y1)) { return false; }
            float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f, band = (x1 - x0) / 2f - 1f;
            Color outline = Colors.Black;
            bool found = false;
            for (int y = 0; y < h && !found; y++)
                for (int x = 0; x < w && !found; x++)
                    if (Opaque(px[y, x]) && Dark(px[y, x])) { outline = px[y, x]; found = true; }
            if (band < 2) { return false; }
            for (int oy = 0; oy < h * S; oy++)
            {
                for (int ox = 0; ox < w * S; ox++)
                {
                    float fx = (ox + 0.5f) / S - 0.5f - cx, fy = (oy + 0.5f) / S - 0.5f - cy;
                    float r = Mathf.Sqrt(fx * fx + fy * fy);
                    var c = new Color(0, 0, 0, 0);
                    Over(ref c, outline, Cover(Math.Abs(r - band) - 1.4f));
                    if (Math.Abs(r - band) < 1f)
                    {
                        Over(ref c, BandColour(px, w, h, cx + fx / Math.Max(r, 0.01f) * band, cy + fy / Math.Max(r, 0.01f) * band), Cover(Math.Abs(r - band) - 0.55f));
                    }
                    Over(ref c, outline, Cover(Math.Abs(r - 1f) - 0.35f));
                    outp.SetPixel(ox, oy, c);
                }
            }
            hotspot = new Vector2(cx + 0.5f, cy + 0.5f);
            return true;
        }

        /// <summary>The average of the coloured (not outline) pixels within 1.5 pixels of (<paramref name="x"/>, <paramref name="y"/>), nearer ones counting more.</summary>
        static Color BandColour(Color[,] px, int w, int h, float x, float y)
        {
            float r = 0, g = 0, b = 0, total = 0;
            for (int yy = (int)Math.Floor(y - 1.5f); yy <= (int)Math.Ceiling(y + 1.5f); yy++)
            {
                for (int xx = (int)Math.Floor(x - 1.5f); xx <= (int)Math.Ceiling(x + 1.5f); xx++)
                {
                    if (yy < 0 || xx < 0 || yy >= h || xx >= w) { continue; }
                    var c = px[yy, xx];
                    if (!Opaque(c) || Dark(c)) { continue; }
                    float d2 = (xx - x) * (xx - x) + (yy - y) * (yy - y);
                    if (d2 > 2.25f) { continue; }
                    float wt = 1f / (0.25f + d2);
                    r += c.R * wt; g += c.G * wt; b += c.B * wt; total += wt;
                }
            }
            return total > 0 ? new Color(r / total, g / total, b / total) : Colors.DarkRed;
        }
    }
}
