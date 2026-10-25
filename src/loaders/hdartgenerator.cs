using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Underworld
{
    /// <summary>
    /// Makes the "Pixel HD" art sets (RotSprite at 2x, 4x or 8x, read by HdArt) from the player's own game
    /// files, on the player's machine, so no game art is ever distributed.
    ///
    /// A port of upscale-lab's export_hd.py in its rotsprite2x, rotsprite4x and rotsprite8x modes, whose decoders follow the
    /// loaders here (GRLoader, CritterLoader, WeaponsLoader, BytLoader, TextureLoader). Everything
    /// but the fonts comes out byte for byte the same as that script's output; the fonts are
    /// made by GenerateFonts, which needs Godot. This part has no Godot types, so the headless
    /// tests run it.
    ///
    /// RotSprite's upscale without the rotation: a Scale2x that treats similar colours as equal
    /// (and keeps the centre when a neighbour is only similar to it) run three times to 8x, then
    /// the most common index of each block (2x2 for 4x, 4x4 for 2x; 8x is the three passes as they are). Every output byte is an index the original
    /// picture used, so palettes, lighting and the shaders work as with the original art.
    /// </summary>
    public static class HdArtGenerator
    {
        /// <summary>The scales offered: 2x, 4x and 8x (about 80 MB, 250 MB and 1 GB a set for both games).</summary>
        public static readonly int[] Scales = { 2, 4, 8 };

        /// <summary>The folder name of the set at <paramref name="scale"/>, which the launcher shows as Pixel HD (RotSprite2x, RotSprite4x).</summary>
        public static string SetName(int scale) => $"rotsprite{scale}x";

        /// <summary>Colours this close (RGB distance, 0-255 per channel) count as the same.</summary>
        const int SimilarDistance = 24;

        // The GR files the game shows as sprites (only the outline is transparent) and as surfaces.
        static readonly string[] SpriteGr = { "OBJECTS", "ANIMO" };
        static readonly string[] SurfaceGr = { "TMOBJ", "TMFLAT", "DOORS" };
        // Interface GR files: GRLoader's list less the object files above and WEAPONS (WeaponsLoader's).
        static readonly string[] UiGr = { "3DWIN", "ARMOR_F", "ARMOR_M", "BODIES", "BUTTONS", "CHAINS", "CHARHEAD", "CHRBTNS",
            "COMPASS", "CONVERSE", "CURSORS", "DRAGONS", "EYES", "FLASKS", "GENHEAD", "HEADS", "INV", "LFTI", "OPBTN", "OPTB",
            "OPTBTNS", "PANELS", "POWER", "QUEST", "SCRLEDGE", "SPELLS", "GEMPT", "GHED" };
        // Full screens: UW1 by file (BytLoader's names and palettes), UW2 by BYT.ARK block.
        static readonly (string Name, int Palette)[] BytUw1 = { ("BLNKMAP", 1), ("CHARGEN", 3), ("CONV", 0), ("MAIN", 0),
            ("OPSCR", 2), ("PRES1", 5), ("PRES2", 5), ("WIN1", 7), ("WIN2", 7) };
        static readonly int[] BytUw2Palette = { 1, 0, 0, 0, 0, 0, 5, 5, 0, 0, 0 };

        /// <summary>An indexed picture, row major.</summary>
        public sealed class Picture
        {
            public readonly int Width, Height;
            public readonly byte[] Data;
            public Picture(int width, int height, byte[] data = null)
            {
                Width = width; Height = height; Data = data ?? new byte[width * height];
            }
            public bool AnyNonZero() => Array.Exists(Data, b => b != 0);
            public bool AnyZero() => Array.IndexOf(Data, (byte)0) >= 0;
        }

        /// <summary>
        /// Writes the set at <paramref name="scale"/> (one of Scales) for one game (<paramref name="game"/> is "uw1"
        /// or "uw2", <paramref name="gameDir"/> the folder holding DATA and CRIT) under <paramref name="setDir"/>/{game}/,
        /// everything but the fonts. <paramref name="progress"/> gets each part's name and how many pictures are done.
        /// </summary>
        public static int Generate(string gameDir, string game, string setDir, int scale, Action<string, int> progress = null, CancellationToken cancel = default)
        {
            if (Array.IndexOf(Scales, scale) < 0) { throw new ArgumentOutOfRangeException(nameof(scale)); }
            var data = Path.Combine(gameDir, "DATA");
            var outDir = Path.Combine(setDir, game);
            var pal = LoadPalette(data, 0);
            int written = 0;
            void Write(string kind, string name, Picture p)
            {
                cancel.ThrowIfCancellationRequested();
                WriteUwhd(Path.Combine(outDir, kind, name + ".uwhd"), p);
                written++;
                progress?.Invoke(kind, written);
            }

            var allpals = File.ReadAllBytes(FindFile(data, "ALLPALS.DAT"));
            foreach (var name in SpriteGr.Concat(SurfaceGr))
            {
                var path = FindFile(data, name + ".GR");
                if (path == null) { continue; }
                bool sprite = SpriteGr.Contains(name);
                foreach (var (i, idx) in GrPictures(File.ReadAllBytes(path), allpals, null))
                {
                    Write("gr", $"{name}_{i:000}", sprite ? UpSprite(idx, pal, scale) : UpSurface(idx, pal, scale));
                }
            }
            foreach (var name in UiGr)
            {
                var path = FindFile(data, name + ".GR");
                if (path == null) { continue; }
                (int, int)? panel = name == "PANELS" ? (game == "uw1" ? (83, 114) : (79, 112)) : null; //GRLoader's fixed PANELS size
                foreach (var (i, idx) in GrPictures(File.ReadAllBytes(path), allpals, panel))
                {
                    //pictures with transparency (index 0) are sprites, wholly opaque ones surfaces
                    Write("gr", $"{name}_{i:000}", idx.AnyZero() ? UpSprite(idx, pal, scale) : UpSurface(idx, pal, scale));
                }
            }

            var weaponGr = FindFile(data, game == "uw1" ? "WEAPONS.GR" : "WEAP.GR");
            var weaponCm = FindFile(data, game == "uw1" ? "WEAPONS.CM" : "WEAP.CM");
            if (weaponGr != null && weaponCm != null)
            {
                var buf = File.ReadAllBytes(weaponGr);
                int count = game == "uw1" ? buf[1] | (buf[2] << 8) : 230; //WeaponsLoader's fixed 230 for UW2
                var table = new byte[256 * 3]; //4-bit value to colour (auxiliary palette 0, only to shape the upscale)
                var cm = File.ReadAllBytes(weaponCm);
                for (int v = 0; v < 16; v++) { Array.Copy(pal, cm[v] * 3, table, v * 3, 3); }
                foreach (var (i, nib) in WeaponFrames(buf, count))
                {
                    Write("weapon", $"WEAPON_{i:000}", UpSprite(nib, table, scale));
                }
            }

            foreach (var (name, screen, palette) in Screens(data, game))
            {
                Write("byt", name, UpSurface(screen, LoadPalette(data, palette), scale));
            }

            foreach (var tr in game == "uw1" ? new[] { "W64", "F32" } : new[] { "T64" })
            {
                var path = FindFile(data, tr + ".TR");
                if (path == null) { continue; }
                int i = 0;
                foreach (var t in Textures(File.ReadAllBytes(path)))
                {
                    Write("tex", $"{tr}_{i++:000}", UpTexture(t, pal, scale));
                }
            }

            var crit = Path.Combine(gameDir, "CRIT");
            if (Directory.Exists(crit))
            {
                var pattern = new Regex(game == "uw1" ? @"^CR..PAGE\.N0.$" : @"^CR..\...$", RegexOptions.IgnoreCase);
                var pages = Directory.GetFiles(crit).Select(Path.GetFileName).Where(n => pattern.IsMatch(n))
                    .OrderBy(n => n, StringComparer.Ordinal).ToList();
                foreach (var page in pages)
                {
                    var buf = File.ReadAllBytes(Path.Combine(crit, page));
                    foreach (var (slot, raw, aux) in CritterFrames(buf, game))
                    {
                        Write("crit", $"{page}_{slot:000}", UpFrame(raw, aux, pal, scale));
                    }
                }
            }
            return written;
        }

        /// <summary>A file in <paramref name="dir"/> by name, matching case as Windows and the GOG data do not.</summary>
        static string FindFile(string dir, string name)
        {
            var exact = Path.Combine(dir, name);
            if (File.Exists(exact)) { return exact; }
            if (!Directory.Exists(dir)) { return null; }
            return Directory.GetFiles(dir).FirstOrDefault(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
        }

        // ---- the upscale of each kind of picture (export_hd.py's up_*) ----

        static Picture UpTexture(Picture idx, byte[] pal, int scale)
        {
            //tiled 3x3 so the edges join when the texture repeats, then the middle tile
            int w = idx.Width, h = idx.Height;
            var tiled = new Picture(w * 3, h * 3);
            for (int y = 0; y < h * 3; y++)
                for (int x = 0; x < w * 3; x++)
                    tiled.Data[y * w * 3 + x] = idx.Data[(y % h) * w + x % w];
            var big = RotSprite(tiled, pal, scale, false);
            var outp = new Picture(w * scale, h * scale);
            for (int y = 0; y < h * scale; y++)
                Array.Copy(big.Data, (h * scale + y) * big.Width + w * scale, outp.Data, y * outp.Width, outp.Width);
            return outp;
        }

        static Picture UpSprite(Picture idx, byte[] colours, int scale) =>
            idx.AnyNonZero() ? RotSprite(idx, colours, scale, true) : Nearest(idx, scale);

        static Picture UpSurface(Picture idx, byte[] colours, int scale) => RotSprite(idx, colours, scale, false);

        /// <summary>
        /// A critter frame of nibble + 1 codes (0 never written). A nibble counts as transparent only
        /// when every auxiliary palette of the page maps it to 0; its colour for the upscale is the
        /// first palette's that is not 0.
        /// </summary>
        static Picture UpFrame(Picture raw, byte[][] auxpals, byte[] pal, int scale)
        {
            var colour = new byte[32];
            for (int n = 0; n < 32; n++)
            {
                foreach (var aux in auxpals)
                {
                    if (aux[n] != 0) { colour[n] = aux[n]; break; }
                }
            }
            var code = new Picture(raw.Width, raw.Height);
            for (int i = 0; i < code.Data.Length; i++)
            {
                int r = raw.Data[i];
                int nib = Math.Max(r - 1, 0);
                code.Data[i] = (r == 0 || colour[nib] == 0) ? (byte)0 : (byte)r;
            }
            if (!code.AnyNonZero())
            {
                return Nearest(code, scale);
            }
            var table = new byte[256 * 3];
            for (int k = 0; k < 32; k++) { Array.Copy(pal, colour[k] * 3, table, (k + 1) * 3, 3); }
            return RotSprite(code, table, scale, true);
        }

        // ---- RotSprite ----

        /// <summary>For each pair of indices, whether their colours count as the same. With
        /// <paramref name="transparent0"/>, index 0 is only the same as itself.</summary>
        public static bool[] SimilarTable(byte[] colours, bool transparent0)
        {
            var sim = new bool[256 * 256];
            for (int a = 0; a < 256; a++)
            {
                for (int b = 0; b < 256; b++)
                {
                    int dr = colours[a * 3] - colours[b * 3], dg = colours[a * 3 + 1] - colours[b * 3 + 1], db = colours[a * 3 + 2] - colours[b * 3 + 2];
                    sim[a * 256 + b] = dr * dr + dg * dg + db * db <= SimilarDistance * SimilarDistance;
                }
            }
            if (transparent0)
            {
                for (int i = 0; i < 256; i++) { sim[i] = false; sim[i * 256] = false; }
                sim[0] = true;
            }
            return sim;
        }

        /// <summary>Scale2x with <paramref name="sim"/> for equality; outside the picture repeats its edge.</summary>
        public static Picture Scale2xSimilar(Picture p, bool[] sim)
        {
            int w = p.Width, h = p.Height;
            var outp = new Picture(w * 2, h * 2);
            var d = p.Data;
            for (int y = 0; y < h; y++)
            {
                int yu = Math.Max(y - 1, 0), yd = Math.Min(y + 1, h - 1);
                for (int x = 0; x < w; x++)
                {
                    int xl = Math.Max(x - 1, 0), xr = Math.Min(x + 1, w - 1);
                    byte e = d[y * w + x], b = d[yu * w + x], hh = d[yd * w + x], l = d[y * w + xl], r = d[y * w + xr];
                    bool db = sim[l * 256 + b], bf = sim[b * 256 + r], dh = sim[l * 256 + hh], hf = sim[hh * 256 + r];
                    int o = (y * 2) * w * 2 + x * 2;
                    outp.Data[o] = db && !bf && !dh && !sim[e * 256 + l] ? l : e;
                    outp.Data[o + 1] = bf && !db && !hf && !sim[e * 256 + r] ? r : e;
                    outp.Data[o + w * 2] = dh && !db && !hf && !sim[e * 256 + l] ? l : e;
                    outp.Data[o + w * 2 + 1] = hf && !dh && !bf && !sim[e * 256 + r] ? r : e;
                }
            }
            return outp;
        }

        /// <summary>Each k x k block becomes its most common index (on a tie, the first in reading order).</summary>
        public static Picture BlockMode(Picture big, int k)
        {
            int w = big.Width / k, h = big.Height / k;
            var outp = new Picture(w, h);
            var block = new byte[k * k];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int dy = 0; dy < k; dy++)
                        for (int dx = 0; dx < k; dx++)
                            block[dy * k + dx] = big.Data[(y * k + dy) * big.Width + x * k + dx];
                    int best = 0, bestCount = -1;
                    for (int j = 0; j < block.Length; j++)
                    {
                        int c = 0;
                        for (int m = 0; m < block.Length; m++) { if (block[m] == block[j]) { c++; } }
                        if (c > bestCount) { bestCount = c; best = j; }
                    }
                    outp.Data[y * w + x] = block[best];
                }
            }
            return outp;
        }

        /// <summary>RotSprite's upscale to <paramref name="scale"/> (2, 4 or 8): three similar-colour Scale2x passes to 8x, then block modes (none at 8x).</summary>
        public static Picture RotSprite(Picture idx, byte[] colours, int scale, bool transparent0)
        {
            var sim = SimilarTable(colours, transparent0);
            var big = idx;
            for (int i = 0; i < 3; i++) { big = Scale2xSimilar(big, sim); }
            return BlockMode(big, 8 / scale);
        }

        static Picture Nearest(Picture p, int s)
        {
            var outp = new Picture(p.Width * s, p.Height * s);
            for (int y = 0; y < outp.Height; y++)
                for (int x = 0; x < outp.Width; x++)
                    outp.Data[y * outp.Width + x] = p.Data[(y / s) * p.Width + x / s];
            return outp;
        }

        public static void WriteUwhd(string path, Picture p)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var bytes = new byte[8 + p.Data.Length];
            bytes[0] = (byte)'U'; bytes[1] = (byte)'W'; bytes[2] = (byte)'H'; bytes[3] = (byte)'D';
            bytes[4] = (byte)(p.Width & 0xFF); bytes[5] = (byte)(p.Width >> 8);
            bytes[6] = (byte)(p.Height & 0xFF); bytes[7] = (byte)(p.Height >> 8);
            Array.Copy(p.Data, 0, bytes, 8, p.Data.Length);
            File.WriteAllBytes(path, bytes);
        }

        // ---- decoders (uwart.py, which follows the loaders) ----

        /// <summary>PALS.DAT palette <paramref name="n"/> as 256 RGB triples, 6-bit values widened as DOSBox does.</summary>
        public static byte[] LoadPalette(string dataDir, int n)
        {
            var raw = File.ReadAllBytes(FindFile(dataDir, "PALS.DAT"));
            var pal = new byte[768];
            for (int i = 0; i < 768; i++)
            {
                int v = raw[n * 0x300 + i];
                pal[i] = (byte)(((v << 2) | (v >> 4)) & 0xFF);
            }
            return pal;
        }

        /// <summary>A .TR file's textures, skipping the slots whose offsets point past the data.</summary>
        static IEnumerable<Picture> Textures(byte[] buf)
        {
            int edge = buf[1], count = buf[2] | (buf[3] << 8);
            for (int i = 0; i < count; i++)
            {
                int off = BitConverter.ToInt32(buf, 4 + i * 4);
                if (off + edge * edge > buf.Length) { continue; }
                var p = new Picture(edge, edge);
                Array.Copy(buf, off, p.Data, 0, edge * edge);
                yield return p;
            }
        }

        /// <summary>
        /// The pictures of a .GR file as GRLoader decodes them: type 4 raw indices, type 8 4-bit RLE
        /// through an ALLPALS.DAT auxiliary palette, type 0xA none, anything else only for PANELS.GR,
        /// a headerless picture of <paramref name="panel"/>'s fixed size.
        /// </summary>
        static IEnumerable<(int, Picture)> GrPictures(byte[] buf, byte[] allpals, (int W, int H)? panel)
        {
            int count = buf[1] | (buf[2] << 8);
            for (int i = 0; i < count; i++)
            {
                long off = BitConverter.ToUInt32(buf, 3 + i * 4);
                if (off >= buf.Length - 1) { continue; }
                int kind = buf[off], w = buf[off + 1], h = buf[off + 2];
                if (kind == 4)
                {
                    var p = new Picture(w, h);
                    Array.Copy(buf, off + 5, p.Data, 0, w * h);
                    yield return (i, p);
                }
                else if (kind == 8)
                {
                    var aux = new byte[16];
                    Array.Copy(allpals, buf[off + 3] * 16, aux, 0, 16);
                    int datalen = buf[off + 4] | (buf[off + 5] << 8);
                    yield return (i, new Picture(w, h, GrRle(buf, (int)off + 6, datalen, w, h, aux, false)));
                }
                else if (kind != 0xA && panel != null)
                {
                    var (pw, ph) = panel.Value;
                    if (off + pw * ph <= buf.Length) //the third panel of both games is a few hundred bytes short; the game never uses it
                    {
                        var p = new Picture(pw, ph);
                        Array.Copy(buf, off, p.Data, 0, pw * ph);
                        yield return (i, p);
                    }
                }
            }
        }

        /// <summary>WEAPONS.GR / WEAP.GR as WeaponsLoader reads them: 4-bit values, 0 transparent, all 0 when datalen is under 6.</summary>
        static IEnumerable<(int, Picture)> WeaponFrames(byte[] buf, int count)
        {
            var identity = Enumerable.Range(0, 16).Select(v => (byte)v).ToArray();
            for (int i = 0; i < count; i++)
            {
                long off = BitConverter.ToUInt32(buf, 3 + i * 4);
                if (off + 6 > buf.Length) { continue; }
                int w = buf[off + 1], h = buf[off + 2];
                int datalen = buf[off + 4] | (buf[off + 5] << 8);
                if (w == 0 || h == 0) { continue; }
                yield return (i, datalen >= 6 ? new Picture(w, h, GrRle(buf, (int)off + 6, datalen, w, h, identity, true)) : new Picture(w, h));
            }
        }

        /// <summary>
        /// GRLoader's copyNibbles and 4-bit DecodeRLEBitmap, nibble buffer length, zero padding and loop
        /// end included; WeaponsLoader's version (<paramref name="weapon"/>) counts datalen in nibbles.
        /// </summary>
        static byte[] GrRle(byte[] buf, int start, int datalen, int w, int h, byte[] aux, bool weapon)
        {
            var nib = new byte[Math.Max(w * h * 2, weapon ? datalen * 2 : (datalen + 5) * 2)];
            int rawLen = Math.Min(weapon ? (datalen + 1) / 2 : datalen, buf.Length - start);
            int split = 0;
            for (int i = 0; i < rawLen; i++)
            {
                if (!weapon || split < datalen) { nib[split] = (byte)(buf[start + i] >> 4); }
                split++;
                if (!weapon || split < datalen) { nib[split] = (byte)(buf[start + i] & 0xF); }
                split++;
            }
            var outp = new byte[w * h];
            int pos = 0, pix = 0, repeat = 0, state = 0, c = 0;
            int Count()
            {
                int n = nib[pos++];
                if (n == 0) { n = (nib[pos] << 4) | nib[pos + 1]; pos += 2; }
                if (n == 0) { n = (((nib[pos] << 4) | nib[pos + 1]) << 4) | nib[pos + 2]; pos += 3; }
                return n;
            }
            while (pix < w * h || (weapon ? pos < datalen : pos <= datalen))
            {
                if (state == 0)
                {
                    c = Count();
                    if (c == 1) { state = 2; }
                    else if (c == 2) { repeat = Count() - 1; }
                    else { state = 1; }
                }
                else if (state == 1)
                {
                    int n = nib[pos++];
                    c = Math.Min(c, w * h - pix);
                    for (int k = 0; k < c; k++) { outp[pix + k] = aux[n]; }
                    pix += c;
                    if (repeat == 0) { state = 2; }
                    else { state = 0; repeat--; }
                }
                else
                {
                    int n = Math.Min(Count(), w * h - pix);
                    for (int k = 0; k < n; k++) { outp[pix++] = aux[nib[pos++]]; }
                    state = 0;
                }
            }
            return outp;
        }

        /// <summary>The 320x200 screens with their palettes: UW1's .BYT files, UW2's BYT.ARK blocks (BYT_nn).</summary>
        static IEnumerable<(string, Picture, int)> Screens(string data, string game)
        {
            if (game == "uw1")
            {
                foreach (var (name, palette) in BytUw1)
                {
                    var path = FindFile(data, name + ".BYT");
                    if (path == null) { continue; }
                    var raw = File.ReadAllBytes(path);
                    if (raw.Length < 64000) { continue; }
                    var p = new Picture(320, 200);
                    Array.Copy(raw, p.Data, 64000);
                    yield return (name, p, palette);
                }
                yield break;
            }
            var arkPath = FindFile(data, "BYT.ARK");
            if (arkPath == null) { yield break; }
            var ark = File.ReadAllBytes(arkPath);
            int count = ark[0];
            for (int i = 0; i < count; i++)
            {
                int off = BitConverter.ToInt32(ark, i * 4 + 6);
                if (off == 0) { continue; }
                int flag = BitConverter.ToInt32(ark, i * 4 + 6 + count * 4);
                byte[] raw;
                if (((flag >> 1) & 1) == 1)
                {
                    int len = 0;
                    raw = DataLoader.unpackUW2(ark, off, ref len);
                }
                else
                {
                    raw = new byte[Math.Max(0, Math.Min(64000, ark.Length - off))];
                    Array.Copy(ark, off, raw, 0, raw.Length);
                }
                if (raw.Length < 64000) { continue; } //blocks 3 and 10 are empty; the game never uses them
                var p = new Picture(320, 200);
                Array.Copy(raw, p.Data, 64000);
                yield return ($"BYT_{i:00}", p, i < BytUw2Palette.Length ? BytUw2Palette[i] : 0);
            }
        }

        /// <summary>
        /// A critter page's frames as nibble + 1 codes with the page's auxiliary palettes. UW1
        /// numbers frames by position in the page; UW2 by slot of its 256 offsets (only those not 0).
        /// </summary>
        static IEnumerable<(int, Picture, byte[][])> CritterFrames(byte[] buf, string game)
        {
            byte[][] auxpals;
            var offsets = new List<(int Slot, int Off)>();
            if (game == "uw1")
            {
                int p = 1 + 1 + buf[1];
                p += 1 + buf[p] * 8;
                int npals = buf[p]; p++;
                auxpals = Enumerable.Range(0, npals).Select(k => buf.Skip(p + k * 32).Take(32).ToArray()).ToArray();
                p += npals * 32;
                int nframes = buf[p]; p += 2;
                for (int i = 0; i < nframes; i++) { offsets.Add((i, buf[p + i * 2] | (buf[p + i * 2 + 1] << 8))); }
            }
            else
            {
                auxpals = Enumerable.Range(0, 4).Select(k => buf.Skip(k * 32).Take(32).ToArray()).ToArray(); //the 128 bytes before the offsets
                for (int slot = 0; slot < 256; slot++)
                {
                    int off = buf[128 + slot * 2] | (buf[129 + slot * 2] << 8);
                    if (off != 0) { offsets.Add((slot, off)); }
                }
            }
            var codes = Enumerable.Range(1, 32).Select(v => (byte)v).ToArray(); //nibble k decodes to k + 1
            foreach (var (slot, off) in offsets)
            {
                int w = buf[off], h = buf[off + 1], comp = buf[off + 4];
                int datalen = buf[off + 5] | (buf[off + 6] << 8);
                //some frames run out of RLE data before their last pixels, which the game leaves 0 (transparent)
                yield return (slot, new Picture(w, h, DecodeRle(buf, off + 7, comp == 6 ? 5 : 4, datalen, w * h, codes)), auxpals);
            }
        }

        /// <summary>
        /// The 4/5-bit critter RLE (compression 6 is 5-bit), as CritterArt.Ua_image_decode_rle, which
        /// is not called here because its class needs Godot.
        /// </summary>
        static byte[] DecodeRle(byte[] buf, int pos, int bits, int datalen, int npix, byte[] auxpal)
        {
            var outp = new byte[npix];
            int bitsAvail = 0, rawbits = 0, bitmask = ((1 << bits) - 1) << (8 - bits);
            int pix = 0, stage = 0, count = 0, record = 0, repeat = 0;
            while (datalen > 0 && pix < npix)
            {
                int nib;
                if (bitsAvail < bits)
                {
                    nib = bitsAvail > 0 ? ((rawbits & bitmask) >> (8 - bitsAvail)) << (bits - bitsAvail) : 0;
                    if (pos >= buf.Length) { break; }
                    rawbits = buf[pos++];
                    int shift = 8 - (bits - bitsAvail);
                    nib |= rawbits >> shift;
                    rawbits = (rawbits << (8 - shift)) & 0xFF;
                    bitsAvail = shift;
                }
                else
                {
                    nib = (rawbits & bitmask) >> (8 - bits);
                    bitsAvail -= bits;
                    rawbits = (rawbits << bits) & 0xFF;
                }
                datalen--;

                switch (stage)
                {
                    case 0:
                        if (nib == 0) { stage = 1; } else { count = nib; stage = 6; }
                        break;
                    case 1:
                        count = nib; stage = 2;
                        break;
                    case 2:
                        count = (count << 4) | nib;
                        stage = count == 0 ? 3 : 6;
                        break;
                    case 3: case 4: case 5:
                        count = (count << 4) | nib;
                        stage++;
                        break;
                }
                if (stage < 6) { continue; }

                switch (record)
                {
                    case 0:
                        if (count == 1) { record = 3; }
                        else if (count == 2) { record = 2; }
                        else { record = 1; continue; }
                        break;
                    case 1:
                        {
                            int n = Math.Min(count, npix - pix);
                            for (int k = 0; k < n; k++) { outp[pix + k] = auxpal[nib]; }
                            pix += n;
                            if (repeat == 0) { record = 3; } else { repeat--; record = 0; }
                        }
                        break;
                    case 2:
                        repeat = count - 1; record = 0;
                        break;
                    case 3:
                        record = 4;
                        continue;
                    case 4:
                        outp[pix++] = auxpal[nib];
                        if (--count != 0) { continue; }
                        record = 0;
                        break;
                }
                stage = 0;
            }
            return outp;
        }
    }
}
