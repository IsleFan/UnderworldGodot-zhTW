using System.IO;
using System.Linq;
using Godot;
using Underworld;

/// <summary>
/// Translated game text (Localization, CjkGlyphs and the message scroll's wrapping).
///
/// Loads the catalog in the project's i18n folder (or UW_I18N_DIR) for the UW1 data settings.json selects, then checks that
/// every row is shown in place of exactly the English it was made from and nowhere else, that
/// GetString, which game logic reads, stays English, that the Chinese characters land in the
/// four game fonts inside their line box, and that Chinese text breaks between characters but
/// never before closing punctuation. Reports SKIP without UW1 data.
/// Restores the settings it changes, in memory and on disk.
/// </summary>
public partial class LocalizationTest : Node
{
    private int _failures;
    private void Fail(string m) { _failures++; GD.PrintErr("FAIL " + m); }

    public override void _Ready() => Callable.From(Run).CallDeferred();

    private void Run()
    {
        string dir = OS.GetEnvironment("UW_I18N_DIR");
        if (string.IsNullOrEmpty(dir)) { dir = ProjectSettings.GlobalizePath("res://i18n"); }
        var settings = uwsettings.instance;
        //_RES is UW1 by default even with no game installed, so look for the game's strings too
        string data = string.IsNullOrWhiteSpace(UWClass.BasePath) ? null : Path.Combine(UWClass.BasePath, "DATA");
        if (UWClass._RES != UWClass.GAME_UW1 || data == null || !File.Exists(Path.Combine(data, "STRINGS.PAK"))
            || !File.Exists(Path.Combine(dir, "uw1", "zh-TW", "master.tsv")))
        {
            GD.Print("GODOT-TEST-VERDICT SKIP needs UW1 game data");
            GetTree().Quit(0); return;
        }
        string savedLanguage = settings.language, savedPath = settings.i18npath;
        try
        {
            CheckWrapping();
            CheckLauncher(dir);

            settings.language = Localization.English;
            settings.i18npath = dir;
            Localization.Load();
            if (Localization.Enabled) { Fail("English loaded a catalog"); }
            if (GameStrings.GetDisplayString(1, 2) != GameStrings.GetString(1, 2)) { Fail("English display differs from GetString"); }

            settings.language = "zh-TW";
            Localization.Load();
            if (!Localization.Enabled || Localization.FontPath == null) { Fail("zh-TW catalog or font not loaded"); }
            CheckCatalog(Path.Combine(dir, "uw1", "zh-TW", "master.tsv"));
            CheckDrift();
            CheckComposed();
            CheckPictures();
            CheckGlyphs();
        }
        catch (System.Exception e)
        {
            Fail($"exception {e}");
        }
        finally
        {
            settings.language = savedLanguage;
            settings.i18npath = savedPath;
            settings.Save();//the launcher check saves
            Localization.Load();
            CjkGlyphs.Reset();
        }
        GD.Print(_failures == 0 ? "GODOT-TEST-VERDICT PASS" : $"GODOT-TEST-VERDICT FAIL {_failures}");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    /// <summary>The TSV's escapes, as exhumed's extract_strings.escape writes them.</summary>
    private static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\0", "\\0");

    /// <summary>
    /// Every row with English in this data is shown as its target, and the English stays what
    /// GetString returns. Some targets are the English on purpose (the lizardmen's language).
    /// </summary>
    private void CheckCatalog(string tsv)
    {
        int rows = 0, shown = 0, noEnglish = 0, same = 0;
        foreach (var line in File.ReadLines(tsv).Skip(1))
        {
            var f = line.Split('\t');
            int block = int.Parse(f[0]), index = int.Parse(f[1]);
            rows++;
            var english = GameStrings.GetString(block, index);
            if (english == "") { noEnglish++; continue; }
            var display = GameStrings.GetDisplayString(block, index);
            if (Escape(display) != CjkGlyphs.ShrinkGlosses(f[4])) { Fail($"{block}_{index} shows '{display}', not its target"); continue; }
            if (english.Any(CjkGlyphs.IsWide)) { Fail($"GetString({block}, {index}) returned the translation"); }
            if (display == english) { same++; } else { shown++; }
        }
        GD.Print($"catalog: {rows} rows, {shown} shown translated, {noEnglish} with no English in this data, "
            + $"{same} the same as the English, {Localization.Mismatches} hash mismatches");
        if (Localization.Mismatches != 0) { Fail($"{Localization.Mismatches} rows do not match this data's English"); }
        if (shown < 6800) { Fail($"only {shown} rows shown translated"); }
    }

    /// <summary>A string that is no longer the one a row was made from is shown as it is.</summary>
    private void CheckDrift()
    {
        int before = Localization.Mismatches;
        var changed = GameStrings.GetString(1, 2) + "!";
        if (Localization.Lookup(1, 2, changed) != changed) { Fail("a changed string was replaced by its old translation"); }
        if (Localization.Mismatches != before + 1) { Fail("the changed string was not counted as a mismatch"); }
        if (Localization.Lookup(1, 2, "中") != "中") { Fail("text that is not from STRINGS.PAK was replaced"); }
        if (Localization.EndSentence("你清醒") != "你清醒。" || Localization.EndSentence("awake") != "awake.")
        {
            Fail("EndSentence does not follow the language of the sentence");
        }
    }

    /// <summary>
    /// Sentences composed from pieces come out in Chinese order with the translated pieces:
    /// count with a measure word, quality before the noun with 的, enchantment and owner after
    /// it, a full stop only when the sentence starts with 你看到. A name without a translation
    /// stays English rather than disappearing.
    /// </summary>
    private void CheckComposed()
    {
        void Expect(string got, string want)
        {
            if (got != want) { Fail($"composed '{got}', expected '{want}'"); }
        }
        if (!ZhText.Enabled) { Fail("ZhText is off with zh-TW loaded"); return; }
        Expect(ZhText.LookObject(true, 3, 3, false, false, 0, null, -1), "你看到 3 個堅固的手斧。");
        Expect(ZhText.LookObject(true, 1, -1, true, false, 0, new MagicEnchantment.Description(false, false, 0xC00 | 1, 2), 1),
            "你看到有魔力的手斧，附有「火柴微光」，還有 2 次充能，屬於一隻腐蟲。");
        Expect(ZhText.LookObject(false, 1, -1, false, true, 0, new MagicEnchantment.Description(true, false, -1, -2), -1),
            "受詛咒的手斧，有毒");
        Expect(ZhText.LookObject(true, 1, -1, false, false, 0, new MagicEnchantment.Description(false, false, 0xC00 | 1, 0), -1),
            "你看到手斧，附有「火柴微光」，已經沒有充能。");
        Expect(ZhText.LookCreature(1, 64, 0, "", false), "你看到不悅的腐蟲。");
        Expect(ZhText.LookCreature(1, 71, 8, GameStrings.GetString(7, 24), false), "你看到不悅的惡鬼，名叫瑞緹查爾。");
        Expect(ZhText.YouRead(0), "你讀了手斧……");
        Expect(ZhText.PhraseAndName(GameStrings.str_you_destroyed_the_, 0), "你摧毀了手斧。");
        Expect(ZhText.OutOf(GameStrings.GetDisplayString(1, GameStrings.str_your_current_vitality_is_), 25, 30), "你目前的生命力是 25／30。");
        Expect(ZhText.Compose(GameStrings.GetDisplayString(1, GameStrings.str_you_are_), GameStrings.GetDisplayString(1, GameStrings.str_barely),
            GameStrings.GetDisplayString(1, GameStrings.str__poisoned_)), "你輕微中毒了。\n");
        var skills = new System.Collections.Generic.List<string> { "攻擊" };
        Expect(ZhText.AdvancedIn(GameStrings.str_you_have_advanced_in_, skills), "你提升了攻擊。");
        skills.Add("防禦");
        Expect(ZhText.AdvancedIn(GameStrings.str_you_have_advanced_in_, skills), "你提升了攻擊和防禦。");
        skills.Add("開鎖");
        Expect(ZhText.AdvancedIn(GameStrings.str_you_have_advanced_greatly_in_, skills), "你大幅提升了攻擊、防禦和開鎖。");
        //the barter appraisal is three pieces the translation chains in the English order
        Expect(GameStrings.GetDisplayString(7, 3) + GameStrings.GetDisplayString(7, 2) + GameStrings.GetDisplayString(7, 12),
            "……我猜我這是一筆公平的交易……");
        //a name whose English no longer matches its row keeps the English, without the article
        Expect(ZhText.LookCreature(1, 71, 8, "Somebody", false), "你看到不悅的惡鬼，名叫Somebody。");

        uwsettings.instance.language = Localization.English;
        if (ZhText.Enabled) { Fail("ZhText is on in English"); }
        uwsettings.instance.language = ZhText.Language;
    }

    /// <summary>
    /// The pictures with English painted in are lettered in Chinese: drawn at ZhImages.Scale
    /// times, reporting their original size so layouts are untouched, and different from the
    /// original. Pictures without English are left alone. With UW_ZHIMG_DUMP set to a folder,
    /// each lettered picture is saved there in palette 0 to look at.
    /// </summary>
    private void CheckPictures()
    {
        var dump = OS.GetEnvironment("UW_ZHIMG_DUMP");
        var savedArt = uwsettings.instance.hdartpath;
        uwsettings.instance.hdartpath = OS.GetEnvironment("UW_ZHIMG_ART");//empty for the original art
        try
        {
            var lettered = new (int File, string Name, int[] Pictures)[]
            {
                (GRLoader.OPTBTNS_GR, "OPTBTNS", Enumerable.Range(1, 60).Where(i => i < 30 || i > 37).ToArray()),
                (GRLoader.OPBTN_GR, "OPBTN", Enumerable.Range(0, 8).ToArray()),
                (GRLoader.PANELS_GR, "PANELS", new[] { 2 }),
            };
            foreach (var (file, name, pictures) in lettered)
            {
                //the main menu's buttons are shown in palette 2 (uimanager_art)
                var zh = new GRLoader(file, GRLoader.GRShaderMode.UIShader) { PaletteNo = (short)(file == GRLoader.OPBTN_GR ? 2 : 0) };
                uwsettings.instance.language = Localization.English;
                var en = new GRLoader(file, GRLoader.GRShaderMode.UIShader) { PaletteNo = zh.PaletteNo };
                foreach (var i in pictures)
                {
                    var original = en.LoadImageAt(i);
                    uwsettings.instance.language = ZhText.Language;
                    var picture = zh.LoadImageAt(i);
                    uwsettings.instance.language = Localization.English;
                    if (picture.GetWidth() != original.GetWidth() || picture.GetHeight() != original.GetHeight())
                    {
                        Fail($"{name} {i} reports {picture.GetSize()}, the original is {original.GetSize()}");
                    }
                    var img = picture.GetImage();
                    if (img.GetWidth() != original.GetWidth() * ZhImages.Scale)
                    {
                        Fail($"{name} {i} is {img.GetWidth()} wide, not {ZhImages.Scale} times {original.GetWidth()}"); continue;
                    }
                    var orig = original.GetImage();
                    int os = orig.GetWidth() / original.GetWidth();//the original's own scale, 1 or the HD art's
                    bool changed = false;
                    for (int y = 0; y < img.GetHeight() && !changed; y++)
                    {
                        for (int x = 0; x < img.GetWidth() && !changed; x++)
                        {
                            changed = img.GetPixel(x, y) != orig.GetPixel(x * os / ZhImages.Scale, y * os / ZhImages.Scale);
                        }
                    }
                    if (!changed) { Fail($"{name} {i} was not lettered"); }
                    if (!string.IsNullOrEmpty(dump) && img.GetFormat() != Image.Format.R8)
                    {
                        img.SavePng(Path.Combine(dump, $"{name}_{i:00}.png"));
                    }
                    else if (!string.IsNullOrEmpty(dump))
                    {
                        var rgb = Image.CreateEmpty(img.GetWidth(), img.GetHeight(), false, Image.Format.Rgba8);
                        for (int y = 0; y < img.GetHeight(); y++)
                        {
                            for (int x = 0; x < img.GetWidth(); x++)
                            {
                                var v = (byte)Mathf.RoundToInt(img.GetPixel(x, y).R * 255);
                                rgb.SetPixel(x, y, v == 0 ? new Color(0.2f, 0.2f, 0.2f) : PaletteLoader.Palettes[0].ColorAtIndex(v, false, false));
                            }
                        }
                        rgb.SavePng(Path.Combine(dump, $"{name}_{i:00}.png"));
                    }
                }
                //a picture with no English keeps its original
                uwsettings.instance.language = ZhText.Language;
                if (name == "OPTBTNS" && zh.LoadImageAt(30).GetImage().GetWidth() != en.LoadImageAt(30).GetImage().GetWidth())
                {
                    Fail("OPTBTNS 30, a roman numeral, was lettered");
                }
            }
        }
        finally
        {
            uwsettings.instance.language = ZhText.Language;
            uwsettings.instance.hdartpath = savedArt;
        }
    }

    /// <summary>
    /// Each game font gets a character once translated text uses it, drawn in its own texture,
    /// with ink, as wide as the em, and with the em inside the font's line box.
    /// </summary>
    private void CheckGlyphs()
    {
        var key = new Vector2I(SysFontBuilder.DesignSize, 0);
        CjkGlyphs.Reset();
        var fonts = SysFontProvider.FontNames.Select(name =>
        {
            var sys = SysFont.Parse(File.ReadAllBytes(Path.Combine(UWClass.BasePath, "DATA", name + ".SYS")));
            var (a, d) = SysFontProvider.LegacyMetricsFor(name);
            return (name, sys, a, d, font: SysFontBuilder.Build(sys, a, d, name));
        }).ToList();
        int changed = 0;
        foreach (var f in fonts) { f.font.Changed += () => changed++; }
        CjkGlyphs.Ensure("國。");
        //labels already showing Chinese must redraw, or they draw the freed texture as solid boxes
        if (changed != fonts.Count) { Fail($"{changed} of {fonts.Count} fonts announced their new characters"); }
        changed = 0;
        CjkGlyphs.Ensure("國");
        if (changed != 0) { Fail("characters already added changed the fonts again"); }
        foreach (var (name, sys, a, d, font) in fonts)
        {
            float em = CjkGlyphs.EmFor(name, sys.Height);
            //half a row shorter than the glyphs, the stats panel's font a row and a half
            if (em != (name == "FONT5X6I" ? sys.Height - 1 : Mathf.Max(sys.Height, 5)) - 0.5f) { Fail($"{name} em is {em}"); }
            int c = '國';
            if (!font.HasChar(c)) { Fail($"{name} has no glyph for 國"); continue; }
            int tex = (int)font.GetGlyphTextureIdx(0, key, c);
            if (tex < 1) { Fail($"{name} 國 is in texture {tex}, the Latin strip's"); continue; }
            var uv = font.GetGlyphUVRect(0, key, c);
            var img = font.GetTextureImage(0, key, tex);
            float ink = 0;
            for (int y = (int)uv.Position.Y; y < uv.End.Y; y++)
            {
                for (int x = (int)uv.Position.X; x < uv.End.X; x++) { ink += img.GetPixel(x, y).A; }
            }
            if (ink < uv.Area * 0.1f) { Fail($"{name} 國 has almost no ink ({ink} over {uv.Area} texels)"); }

            float advance = font.GetGlyphAdvance(0, SysFontBuilder.DesignSize, c).X;
            var offset = font.GetGlyphOffset(0, key, c);
            var size = font.GetGlyphSize(0, key, c);
            if (Mathf.Abs(advance - em) > 1) { Fail($"{name} 國 advances {advance}, em is {em}"); }
            if (size.Y > em + 0.5f) { Fail($"{name} 國 is {size.Y} high, more than the {em} em"); }
            //the em's bottom is the Latin glyphs' bottom row, d below the baseline
            if (offset.Y + size.Y > d + 0.5f) { Fail($"{name} 國 reaches {offset.Y + size.Y} below the baseline, the line box ends at {d}"); }
            if (offset.Y < -a) { Fail($"{name} 國 starts {-offset.Y} above the baseline, the line box starts at {a}"); }
            if (!font.HasChar('。')) { Fail($"{name} has no glyph for 。"); }
            GD.Print($"{name}: 國 advance {advance}, box {size} at {offset}, ink {ink / uv.Area:F2}");
        }
        //a font built after the characters were added gets them too
        var late = fonts[0];
        var again = SysFontBuilder.Build(late.sys, late.a, late.d, late.name);
        if (!again.HasChar('國')) { Fail("a font built later did not get the characters already added"); }
    }

    /// <summary>
    /// The launcher lists English and the translations under i18npath, shows the language the
    /// settings name, and choosing one writes its code to the settings.
    /// </summary>
    private void CheckLauncher(string dir)
    {
        var settings = uwsettings.instance;
        settings.i18npath = dir;
        settings.language = "zh-TW";
        var menu = GD.Load<PackedScene>("res://scenes/Launch.tscn").Instantiate<LaunchMenu>();
        AddChild(menu);//runs _Ready, which fills the list
        var list = menu.LanguageOptionSelector;
        string items = "";
        for (int i = 0; i < list.ItemCount; i++) { items += (i > 0 ? "," : "") + list.GetItemText(i); }
        if (items != "English,繁體中文") { Fail($"languages are {items}"); }
        if (list.Selected != 1) { Fail($"selected {list.Selected}, expected 繁體中文"); }
        menu._on_language_options_item_selected(0);
        if (settings.language != Localization.English) { Fail($"choosing English set language to '{settings.language}'"); }
        menu._on_language_options_item_selected(1);
        if (settings.language != "zh-TW") { Fail($"choosing 繁體中文 set language to '{settings.language}'"); }
        menu.QueueFree();
    }

    /// <summary>
    /// English splits at spaces as it always did; Chinese splits between characters, with
    /// closing punctuation kept on the character before it and opening brackets on the one after.
    /// </summary>
    private void CheckWrapping()
    {
        void Expect(string line, string[] words, string[] seps)
        {
            var got = MessageDisplay.Tokenise(line, out var gotSeps);
            if (!got.SequenceEqual(words) || !gotSeps.SequenceEqual(seps))
            {
                Fail($"'{line}' splits as [{string.Join("|", got)}] / [{string.Join("|", gotSeps)}]");
            }
        }
        Expect("You see  a sword.", new[] { "You", "see", "", "a", "sword." }, new[] { " ", " ", " ", " ", " " });
        Expect("你好，世界。", new[] { "你", "好，", "世", "界。" }, new[] { "", "", "", " " });
        Expect("說「是」……Avatar爵士", new[] { "說", "「是」……", "Avatar", "爵", "士" }, new[] { "", "", "", "", " " });
        Expect("", new[] { "" }, new[] { " " });
        if (MessageDisplay.Width("ab") != 2 || MessageDisplay.Width("你好") <= 2) { Fail("Chinese is not counted wider than letters"); }
    }
}
