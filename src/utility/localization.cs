using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Godot;

namespace Underworld
{
    /// <summary>
    /// Optional translations of the game strings, shown in place of the English ones.
    ///
    /// The catalog is a master.tsv per game (block, index, category, source_hash, target, ...):
    /// UW1's is underworld-exhumed's, UW2's this project's own. Both are kept in the i18n folder (see i18n/README.md) unless i18npath
    /// names another. Only display goes
    /// through it: GameStrings.GetString keeps returning English, because game logic parses
    /// and compares those strings (object names, shrine mantras, conversation compares).
    /// Callers that only show a string ask GameStrings.GetDisplayString instead.
    ///
    /// Every lookup checks the English string it would replace against the row's source_hash,
    /// the SHA-256 of its CP437 bytes, and keeps the English on a mismatch. That covers other
    /// releases of the data and the strings the conversation VM adds at runtime, which reuse
    /// keys of the block they are added to.
    ///
    ///   {Root}/{uw1|uw2}/{language}/master.tsv
    ///   {Root}/fonts/jf-openhuninn-2.1.ttf   the CJK glyphs, see CjkGlyphs
    /// </summary>
    public static class Localization
    {
        public const string English = "en";
        public const string FontFileName = "jf-openhuninn-2.1.ttf";

        record struct Entry(byte[] SourceHash, string Text);

        static Dictionary<int, Entry> catalog;

        public static bool Enabled => catalog != null;

        /// <summary>The CJK font for CjkGlyphs, or null when there is none or no catalog.</summary>
        public static string FontPath { get; private set; }

        /// <summary>Lookups that found a translation, and ones that fell back on a hash mismatch.</summary>
        public static int Hits, Mismatches;

        /// <summary>The folder of translations: i18npath, or the i18n folder shipped with the program (AppPaths.DataDir) when that is empty.</summary>
        public static string Root =>
            string.IsNullOrWhiteSpace(uwsettings.instance?.i18npath)
                ? Path.Combine(AppPaths.DataDir(), "i18n")
                : uwsettings.instance.i18npath;

        static int Key(int block, int index) => (block << 16) | index;

        /// <summary>
        /// Loads the catalog for the game being played and the language in the settings. Leaves
        /// translation off, with a reason printed, when either is missing or unreadable.
        /// </summary>
        public static void Load()
        {
            catalog = null;
            FontPath = null;
            Hits = Mismatches = 0;
            var settings = uwsettings.instance;
            if (settings == null || string.IsNullOrWhiteSpace(settings.language) || settings.language == English)
            {
                return;
            }
            string game = UWClass._RES switch
            {
                UWClass.GAME_UW1 => "uw1",
                UWClass.GAME_UW2 => "uw2",
                _ => null, //the UW1 demo's strings differ from the game's
            };
            if (game == null)
            {
                GD.Print($"Localization: no {settings.language} translation for this game, using English");
                return;
            }
            var path = Path.Combine(Root, game, settings.language, "master.tsv");
            if (!File.Exists(path))
            {
                GD.PrintErr($"Localization: {path} not found, using English");
                return;
            }
            try
            {
                catalog = Parse(File.ReadAllLines(path, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Localization: {path} could not be read ({ex.Message}), using English");
                return;
            }
            var font = Path.Combine(Root, "fonts", FontFileName);
            if (File.Exists(font))
            {
                FontPath = font;
            }
            else
            {
                GD.PrintErr($"Localization: {font} not found, Chinese text will show as gaps");
            }
            GD.Print($"Localization: {catalog.Count} {settings.language} strings from {path}");
        }

        /// <summary>Rows keyed by block and index. Throws on a malformed row.</summary>
        static Dictionary<int, Entry> Parse(string[] lines)
        {
            var result = new Dictionary<int, Entry>();
            if (lines.Length == 0 || !lines[0].StartsWith("block\tindex\tcategory\tsource_hash\t"))
            {
                throw new FormatException("unexpected header");
            }
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) { continue; }
                var f = lines[i].Split('\t');
                if (f.Length < 5)
                {
                    throw new FormatException($"line {i + 1} has {f.Length} fields");
                }
                var text = Unescape(f[4]);
                if (text.Length == 0) { continue; }
                result[Key(int.Parse(f[0]), int.Parse(f[1]))] = new Entry(Convert.FromHexString(f[3]), text);
            }
            return result;
        }

        /// <summary>The TSV's escapes, as exhumed's extract_strings.unescape reads them.</summary>
        static string Unescape(string field)
        {
            if (field.IndexOf('\\') < 0) { return field; }
            var sb = new StringBuilder(field.Length);
            for (int i = 0; i < field.Length; i++)
            {
                char c = field[i];
                if (c == '\\')
                {
                    if (++i == field.Length) { throw new FormatException("dangling escape"); }
                    c = field[i] switch
                    {
                        '\\' => '\\',
                        't' => '\t',
                        'r' => '\r',
                        'n' => '\n',
                        '0' => '\0',
                        _ => throw new FormatException($"invalid escape \\{field[i]}")
                    };
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Ends a sentence the game puts together from strings and a hard-coded ".", with the
        /// full stop of the language the strings ended up in.
        /// </summary>
        public static string EndSentence(string sentence)
        {
            if (sentence.Length > 0 && CjkGlyphs.IsWide(sentence[^1]))
            {
                CjkGlyphs.Ensure("。");
                return sentence + "。";
            }
            return sentence + ".";
        }

        /// <summary>
        /// The translation of block/index when there is one for exactly this English string,
        /// otherwise the English. GameStrings decodes each byte to the char of the same value,
        /// so those chars are the CP437 bytes the hash was taken over.
        /// </summary>
        public static string Lookup(int block, int index, string english)
        {
            if (catalog == null || string.IsNullOrEmpty(english)
                || !catalog.TryGetValue(Key(block, index), out var entry))
            {
                return english;
            }
            var bytes = new byte[english.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (english[i] > 0xFF) { return english; } //not from STRINGS.PAK
                bytes[i] = (byte)english[i];
            }
            if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(entry.SourceHash))
            {
                Mismatches++;
                return english;
            }
            Hits++;
            var text = CjkGlyphs.ShrinkGlosses(entry.Text);
            CjkGlyphs.Ensure(text);
            return text;
        }
    }
}
