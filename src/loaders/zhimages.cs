using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Underworld
{
    /// <summary>
    /// Chinese for the English painted into the interface pictures, when the zh-TW translation
    /// is loaded (see ZhText): UW1's main menu buttons (OPBTN.GR), both games' in-game options
    /// (OPTBTNS.GR) and the stats panel's attribute names (PANELS.GR picture 2).
    ///
    /// Works on the palette indices GRLoader decodes, before the texture is made, so the palette
    /// shaders and HD art apply as they do to every other picture. The English is erased by
    /// replacing its letters and shadow, widened by an original pixel to take in the edges HD art
    /// gives them, with the nearest pixel that is not letter, which keeps the button's fill or the
    /// panel's grain; the stats panel is patched from the plain panel beside its names instead.
    /// The Chinese is then drawn in the
    /// picture's own indices, ink and (on the main menu) outline, without blending, so it stays pixel art.
    ///
    /// The pictures are 14 to 23 rows high, too few for Chinese, so they are lettered at Scale
    /// times their size whatever the art (original art is enlarged with nearest neighbour first),
    /// and GRLoader reports their original size as it does for HD art.
    ///
    /// Letters are told apart by their colour in palette 0 rather than by index, because HD art
    /// edges them with whichever neighbouring index the upscaler chose: the options' white or grey
    /// letters and black shadow, the stats panel's black.
    ///
    /// The places and colours were measured from the GOG data (upscale-lab's dumps); the words
    /// are the game's own where it has them (the attribute names in block 2), otherwise the
    /// usual Traditional Chinese for the menu item.
    /// </summary>
    public static class ZhImages
    {
        public const int Scale = 4;

        /// <summary>Some English painted in <paramref name="Area"/> (original pixels), and the Chinese for it.</summary>
        record Label(Rect2I Area, string Text);

        /// <summary>How one picture is lettered.</summary>
        /// <param name="Erase">Which indices are the English letters and their shadow; null clears the whole picture to transparent.</param>
        /// <param name="Ink">The letters' index, or -1 to use the commonest light letter index in each area.</param>
        /// <param name="Outline">The outline's index, or -1 for none.</param>
        /// <param name="Em">The Chinese em in original pixels.</param>
        /// <param name="Patch">When set, each area is covered with the picture this far away (original pixels) instead of erased letter by letter.</param>
        /// <param name="Bold">Draws with the game fonts' emboldened strokes.</param>
        /// <param name="Indent">With Left, how far in from the area's left the lines start, in lettered pixels (Scale per original pixel).</param>
        /// <param name="OutlineWidth">How thick the outline is, in lettered pixels.</param>
        record Lettering(Label[] Labels, System.Func<byte, bool> Erase, int Ink, int Outline, float Em, bool Left = false, Vector2I? Patch = null,
            bool Bold = false, int Indent = Scale / 2, int OutlineWidth = Scale / 2);

        static float Luminance(byte i)
        {
            var c = PaletteLoader.Palettes[0].ColorAtIndex(i, false, false);
            return 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
        }

        /// <summary>White or grey: bright and colourless, unlike the buttons' light orange bevels.</summary>
        static bool Light(byte i)
        {
            var c = PaletteLoader.Palettes[0].ColorAtIndex(i, false, false);
            return Luminance(i) > 0.59f && Mathf.Max(c.R, Mathf.Max(c.G, c.B)) - Mathf.Min(c.R, Mathf.Min(c.G, c.B)) < 0.16f;
        }

        /// <summary>The options' white and grey letters and their black shadow.</summary>
        static bool OptionLetters(byte i) => Light(i) || Luminance(i) < 0.14f;

        static Dictionary<(string, int), Lettering> table, tableUW2;

        static Dictionary<(string, int), Lettering> Table => UWClass._RES == UWClass.GAME_UW2 ? tableUW2 ??= BuildUW2() : table ??= Build();

        static Rect2I ButtonArea => new(1, 2, 30, 11);
        static Rect2I SlotButton(int row) => new(2, 4 + 15 * row, 30, 11);

        static Lettering Option(params Label[] labels) => new(labels, OptionLetters, -1, -1, 5);

        /// <summary>The main menu's palette (GRLoader's PaletteNo 2 for OPBTN.GR): bronze letters, gold when highlighted, near black outline.</summary>
        const int MenuInk = 170, MenuHighlight = 161, MenuOutline = 213;

        /// <summary>UW2's main menu, in the same palette: near black letters edged in light grey, light lilac when highlighted.</summary>
        const int MenuInkUW2 = 222, MenuEdgeUW2 = 196, MenuHighlightUW2 = 152;

        /// <summary>Bold and nearly the height of the smallest button (17 rows), to stand as large as UW2's heavy English.</summary>
        const int MenuEmUW2 = 16;

        /// <summary>
        /// The main menu's em, the same for every button (the pictures are 18 to 23 rows high), so
        /// the four lines read as one list; the menu centres the buttons (uimanager.InitMainMenu).
        /// </summary>
        const int MenuEm = 15;

        static Dictionary<(string, int), Lettering> Build()
        {
            var t = new Dictionary<(string, int), Lettering>();
            //the in-game options: single buttons in Off/On pairs, then labels, then the five panels they sit on
            var buttons = new (int Off, string Text)[]
            {
                (6, "儲存遊戲"), (8, "讀取遊戲"), (10, "音樂"), (12, "音效"), (14, "畫面細節"), (16, "返回遊戲"),
                (18, "離開遊戲"), (20, "開"), (22, "關"), (24, "取消"), (26, "完成"), (28, "完成"),
                (38, "低"), (40, "中"), (42, "高"), (44, "極高"), (57, "是"), (59, "否"),
            };
            foreach (var (off, text) in buttons)
            {
                t[("OPTBTNS", off)] = Option(new Label(ButtonArea, text));
                t[("OPTBTNS", off + 1)] = Option(new Label(ButtonArea, text));
            }
            var labels = new (int Index, string Text)[]
            {
                (46, "讀取遊戲："), (47, "音樂：開"), (48, "音樂：關"), (49, "音效：開"), (50, "音效：關"),
                (51, "切換音樂："), (52, "切換音效："),
            };
            foreach (var (index, text) in labels)
            {
                t[("OPTBTNS", index)] = Option(new Label(new Rect2I(0, 0, 31, 14), text));
            }
            var details = new[] { "低", "中", "高", "極高" };
            for (int i = 0; i < 4; i++)
            {
                t[("OPTBTNS", 53 + i)] = Option(new Label(new Rect2I(0, 0, 34, 18), $"畫面細節：\n{details[i]}"));
            }
            t[("OPTBTNS", 1)] = Option(
                new Label(SlotButton(0), "儲存遊戲"), new Label(SlotButton(1), "讀取遊戲"), new Label(SlotButton(2), "音樂"),
                new Label(SlotButton(3), "音效"), new Label(SlotButton(4), "畫面細節"), new Label(SlotButton(5), "返回遊戲"),
                new Label(SlotButton(6), "離開遊戲"));
            t[("OPTBTNS", 2)] = Option(new Label(new Rect2I(1, 2, 33, 13), "儲存遊戲："), new Label(SlotButton(5), "取消"));
            t[("OPTBTNS", 3)] = Option(new Label(new Rect2I(1, 2, 33, 13), "離開遊戲："), new Label(new Rect2I(1, 17, 33, 13), "確定嗎？"),
                new Label(SlotButton(2), "是"), new Label(SlotButton(3), "否"));
            t[("OPTBTNS", 4)] = Option(new Label(new Rect2I(1, 2, 33, 13), "音樂：開"), new Label(new Rect2I(1, 17, 33, 13), "切換音樂："),
                new Label(SlotButton(2), "開"), new Label(SlotButton(3), "關"), new Label(SlotButton(4), "完成"));
            t[("OPTBTNS", 5)] = Option(new Label(new Rect2I(1, 1, 33, 17), "畫面細節：\n低"), new Label(new Rect2I(1, 19, 33, 13), "改成："),
                new Label(SlotButton(2), "低"), new Label(SlotButton(3), "中"), new Label(SlotButton(4), "高"),
                new Label(SlotButton(5), "極高"), new Label(SlotButton(6), "完成"));

            //the main menu: ornate red letters, gold when highlighted, wound about with ivy that cannot be kept apart from them
            var menu = new[] { "開場介紹", "創造角色", "製作群", "繼續旅程" };
            var sizes = new[] { (113, 18), (159, 21), (168, 23), (146, 23) };
            for (int i = 0; i < 4; i++)
            {
                var area = new Rect2I(0, 0, sizes[i].Item1, sizes[i].Item2);
                t[("OPBTN", i * 2)] = new Lettering(new[] { new Label(area, menu[i]) }, null, MenuInk, MenuOutline, MenuEm);
                t[("OPBTN", i * 2 + 1)] = new Lettering(new[] { new Label(area, menu[i]) }, null, MenuHighlight, MenuOutline, MenuEm);
            }
            return t;
        }

        /// <summary>
        /// UW2's options are whole menus of seven buttons a picture (rows 16 apart from row 3, plain
        /// at 8 and up, lit at 3 to 7 plus 5) and strips of four (from row 1) that the options cut
        /// single buttons from (uimanager.InitGameOptions). The letters are blue-grey, a highlight
        /// and a body over a black edge and a dark blue shadow; the lit pictures use the palette
        /// entries two below. The buttons' mottle is the next blue-grey down, so it is left alone.
        /// </summary>
        static Dictionary<(string, int), Lettering> BuildUW2()
        {
            var t = new Dictionary<(string, int), Lettering>();
            static Label Row(int top, int row, string text) => new(new Rect2I(2, top + 16 * row + 1, 74, 13), text);
            void Menu(int plain, params string[] rows)
            {
                foreach (var (index, lit) in new[] { (plain, false), (plain + 5, true) })
                {
                    var labels = rows.Select((text, row) => text == null ? null : Row(3, row, text)).Where(l => l != null).ToArray();
                    t[("OPTBTNS", index)] = OptionUW2(lit, labels);
                }
            }
            void Strip(int index, params string[] rows) =>
                t[("OPTBTNS", index)] = OptionUW2(false, rows.Select((text, row) => text == null ? null : Row(1, row, text)).Where(l => l != null).ToArray());

            Menu(3, "儲存遊戲", "讀取遊戲", "音樂", "音效", "畫面細節", "離開遊戲", "返回遊戲");
            Menu(4, "畫面細節：低", "改成：", "低", "中", "高", "極高", "完成");
            Menu(5, "離開遊戲：", "確定嗎？", "是", "否");
            Menu(6, "儲存遊戲：", null, null, null, null, "取消"); //the slots are numbered 1 to 4
            Menu(7, "音樂：關", "切換音樂：", "開", "關", "完成");
            Strip(13, "畫面細節：低", "畫面細節：中", "畫面細節：高", "畫面細節：極高");
            Strip(14, "儲存遊戲：", "讀取遊戲：", "切換音效："); //the fourth says "bug." and is never shown
            Strip(15, "音樂：關", "音樂：開", "音效：關", "音效：開");

            //the main menu: letters on pieces of the stone behind them, so a cleared button shows the same stone
            var menu = new[] { "開場介紹", "創造角色", "製作群", "繼續旅程" };
            var sizes = new[] { (109, 17), (159, 21), (167, 24), (145, 25) };
            for (int i = 0; i < 4; i++)
            {
                var area = new Rect2I(0, 0, sizes[i].Item1, sizes[i].Item2);
                t[("OPBTN", i * 2)] = new Lettering(new[] { new Label(area, menu[i]) }, null, MenuInkUW2, MenuEdgeUW2, MenuEmUW2, Bold: true);
                t[("OPBTN", i * 2 + 1)] = new Lettering(new[] { new Label(area, menu[i]) }, null, MenuHighlightUW2, MenuInkUW2, MenuEmUW2, Bold: true);
            }
            return t;
        }

        static Lettering OptionUW2(bool lit, Label[] labels)
        {
            int highlight = lit ? 152 : 154, body = lit ? 156 : 158, shadow = lit ? 161 : 163;
            //bold, with the English's full pixel of black edge, else the strokes are lost on the dark buttons
            return new Lettering(labels, i => i <= 1 || i == highlight || i == body || i == shadow, highlight, 1, 8, Bold: true, OutlineWidth: Scale);
        }

        /// <summary>
        /// The stats panel's six attribute names, a line each 7 rows apart: black on UW1's panel,
        /// light grey on UW2's, which is 4 pixels narrower and starts them 3 rows higher. The
        /// values beside them are labels the game draws, so the panel 27 pixels to the right is plain.
        /// </summary>
        /// <summary>Puts the names' left edge under the class name's (CharClass), whose ink starts 1.5 original pixels further in than the area.</summary>
        const int StatsIndent = 8;

        static Lettering StatsPanel()
        {
            var uw2 = UWClass._RES == UWClass.GAME_UW2;
            var lines = new Label[6];
            for (int i = 0; i < 6; i++)
            {
                lines[i] = new Label(new Rect2I(uw2 ? 4 : 6, (uw2 ? 16 : 20) + 7 * i, 26, 7), GameStrings.GetDisplayString(2, 17 + i).TrimEnd('：', ':', ' '));
            }
            return uw2
                ? new Lettering(lines, Light, -1, -1, 5, Left: true, Patch: new Vector2I(27, 0), Bold: true, Indent: StatsIndent)
                : new Lettering(lines, i => Luminance(i) < 0.2f, 1, -1, 5, Left: true, Patch: new Vector2I(27, 0), Bold: true, Indent: StatsIndent);
        }

        static Lettering For(string file, int index)
        {
            if (!ZhText.Enabled || (UWClass._RES != UWClass.GAME_UW1 && UWClass._RES != UWClass.GAME_UW2))
            {
                return null;
            }
            file = file.ToUpper();
            if (file == "PANELS" && index == 2)
            {
                return StatsPanel();
            }
            return Table.TryGetValue((file, index), out var l) ? l : null;
        }

        /// <summary>Whether <paramref name="file"/>'s picture <paramref name="index"/> is lettered in Chinese.</summary>
        public static bool Has(string file, int index) => For(file, index) != null;

        /// <summary>
        /// Letters picture <paramref name="index"/> of <paramref name="file"/> (a .GR name without
        /// extension) in Chinese, if it has English in it. <paramref name="indices"/> are its palette
        /// indices at <paramref name="scale"/> times its <paramref name="width"/> by <paramref name="height"/>;
        /// on return they are the lettered picture at Scale times (at the art's scale when that is a larger
        /// multiple of Scale, as 8x). False leaves both as they were.
        /// </summary>
        public static bool Apply(string file, int index, ref byte[] indices, int width, int height, ref int scale)
        {
            var lettering = For(file, index);
            //lettered at Scale, or at the art's own scale when that is a larger multiple of it (8x)
            int l = scale <= Scale ? (Scale % scale == 0 ? Scale : 0) : (scale % Scale == 0 ? scale : 0);
            if (lettering == null || l == 0)
            {
                return false;
            }
            int k = l / Scale; //the lettering's sizes are in Scale pixels
            var px = Enlarge(indices, width * scale, height * scale, l / scale);
            int w = width * l, h = height * l;
            HashSet<byte> erase = null;
            if (lettering.Erase != null)
            {
                erase = new HashSet<byte>();
                for (int i = 0; i < 256; i++)
                {
                    if (lettering.Erase((byte)i)) { erase.Add((byte)i); }
                }
            }
            if (erase == null)
            {
                System.Array.Clear(px);
            }
            foreach (var label in lettering.Labels)
            {
                var area = new Rect2I(label.Area.Position * l, label.Area.Size * l);
                int ink = lettering.Ink;
                if (erase != null)
                {
                    if (ink < 0)
                    {
                        ink = Commonest(px, w, area, erase);
                    }
                    if (lettering.Patch is Vector2I patch)
                    {
                        Copy(px, w, area, patch * l);
                    }
                    else
                    {
                        Erase(px, w, h, area, erase, l);
                    }
                }
                Letter(px, w, h, area, label.Text, Mathf.RoundToInt(lettering.Em * l), (byte)ink, lettering.Outline, lettering.OutlineWidth * k,
                    lettering.Left ? lettering.Indent * k : -1, lettering.Bold);
            }
            indices = px;
            scale = l;
            return true;
        }

        static byte[] Enlarge(byte[] src, int w, int h, int f)
        {
            if (f == 1)
            {
                return (byte[])src.Clone();
            }
            var dst = new byte[w * f * h * f];
            for (int y = 0; y < h * f; y++)
            {
                for (int x = 0; x < w * f; x++)
                {
                    dst[y * w * f + x] = src[(y / f) * w + x / f];
                }
            }
            return dst;
        }

        /// <summary>The commonest light letter index in the area, the colour to letter it in.</summary>
        static int Commonest(byte[] px, int w, Rect2I area, HashSet<byte> letters)
        {
            var counts = new Dictionary<byte, int>();
            for (int y = area.Position.Y; y < area.End.Y; y++)
            {
                for (int x = area.Position.X; x < area.End.X; x++)
                {
                    var v = px[y * w + x];
                    if (letters.Contains(v) && Light(v))
                    {
                        counts[v] = counts.GetValueOrDefault(v) + 1;
                    }
                }
            }
            return counts.Count == 0 ? letters.First(Light) : counts.OrderByDescending(kv => kv.Value).First().Key;
        }

        /// <summary>Covers the area with the pixels <paramref name="offset"/> away.</summary>
        static void Copy(byte[] px, int w, Rect2I area, Vector2I offset)
        {
            for (int y = area.Position.Y; y < area.End.Y; y++)
            {
                for (int x = area.Position.X; x < area.End.X; x++)
                {
                    px[y * w + x] = px[(y + offset.Y) * w + x + offset.X];
                }
            }
        }

        /// <summary>
        /// Replaces the letters in the area, and every pixel within <paramref name="widen"/> of one,
        /// with the nearest pixel in the area outside them.
        /// </summary>
        static void Erase(byte[] px, int w, int h, Rect2I area, HashSet<byte> letters, int widen)
        {
            int aw = area.Size.X, ah = area.Size.Y;
            var letter = new bool[ah, aw];
            for (int y = 0; y < ah; y++)
            {
                for (int x = 0; x < aw; x++)
                {
                    letter[y, x] = letters.Contains(px[(area.Position.Y + y) * w + area.Position.X + x]);
                }
            }
            var masked = new bool[ah, aw];
            for (int y = 0; y < ah; y++)
            {
                for (int x = 0; x < aw; x++)
                {
                    masked[y, x] = Near(letter, x, y, widen);
                }
            }
            var src = (byte[])px.Clone();
            for (int y = 0; y < ah; y++)
            {
                for (int x = 0; x < aw; x++)
                {
                    if (!masked[y, x]) { continue; }
                    for (int r = 1; r < Mathf.Max(aw, ah); r++)
                    {
                        int found = -1;
                        //the ring at distance r, scanned from its top row
                        for (int dy = -r; dy <= r && found < 0; dy++)
                        {
                            for (int dx = -r; dx <= r; dx++)
                            {
                                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) { continue; }
                                int qy = y + dy, qx = x + dx;
                                if (qy < 0 || qy >= ah || qx < 0 || qx >= aw || masked[qy, qx]) { continue; }
                                found = src[(area.Position.Y + qy) * w + area.Position.X + qx];
                                break;
                            }
                        }
                        if (found >= 0)
                        {
                            px[(area.Position.Y + y) * w + area.Position.X + x] = (byte)found;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws the text's lines (split at \n) at <paramref name="size"/> pixels, centred in the
        /// area or, with an <paramref name="indent"/> of 0 or more, that far in from its left, each
        /// line trimmed to its ink, ink at half coverage or more.
        /// </summary>
        static void Letter(byte[] px, int w, int h, Rect2I area, string text, int size, byte ink, int outline, int radius, int indent, bool bold)
        {
            var lines = new List<bool[,]>();
            foreach (var line in text.Split('\n'))
            {
                var cover = CjkGlyphs.Rasterize(line, size, out _, bold);
                if (cover == null)
                {
                    return; //no font: the English is already gone, but nothing else can be done
                }
                lines.Add(Trim(cover));
            }
            int gap = size / 5;
            int total = lines.Sum(l => l.GetLength(0)) + gap * (lines.Count - 1);
            int top = area.Position.Y + (area.Size.Y - total) / 2;
            var mask = new bool[h, w];
            foreach (var line in lines)
            {
                int lh = line.GetLength(0), lw = line.GetLength(1);
                int x0 = indent >= 0 ? area.Position.X + indent : area.Position.X + (area.Size.X - lw) / 2;
                for (int y = 0; y < lh; y++)
                {
                    for (int x = 0; x < lw; x++)
                    {
                        int py = top + y, qx = x0 + x;
                        if (line[y, x] && py >= 0 && py < h && qx >= 0 && qx < w) { mask[py, qx] = true; }
                    }
                }
                top += lh + gap;
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (mask[y, x])
                    {
                        px[y * w + x] = ink;
                    }
                    else if (outline >= 0 && Near(mask, x, y, radius))
                    {
                        px[y * w + x] = (byte)outline;
                    }
                }
            }
        }

        static bool Near(bool[,] mask, int x, int y, int r)
        {
            int h = mask.GetLength(0), w = mask.GetLength(1);
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int qy = y + dy, qx = x + dx;
                    if (qy >= 0 && qy < h && qx >= 0 && qx < w && mask[qy, qx]) { return true; }
                }
            }
            return false;
        }

        /// <summary>The coverage at half or more, cropped to the rows and columns with ink.</summary>
        static bool[,] Trim(float[,] cover)
        {
            int h = cover.GetLength(0), w = cover.GetLength(1);
            int x0 = w, x1 = -1, y0 = h, y1 = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (cover[y, x] >= 0.5f)
                    {
                        x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                    }
                }
            }
            if (x1 < 0)
            {
                return new bool[1, 1];
            }
            var r = new bool[y1 - y0 + 1, x1 - x0 + 1];
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    r[y - y0, x - x0] = cover[y, x] >= 0.5f;
                }
            }
            return r;
        }
    }
}
