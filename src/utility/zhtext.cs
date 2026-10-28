namespace Underworld
{
    /// <summary>
    /// Sentences the game puts together from pieces, in Chinese word order, for when the zh-TW
    /// translation is loaded (see Localization).
    ///
    /// English builds "You see a heavy magical sword of Light belonging to a rotworm" by
    /// appending articles, plurals and fixed English around the strings. Translating the pieces
    /// one by one cannot reorder them, so the callers hand their pieces here instead. The
    /// object names in the catalog are bare nouns: articles and plurals do not exist in Chinese,
    /// and a count is said with a measure word. The phrases are underworld-exhumed's
    /// (src/port/i18n/phrases.c), so both ports say the same thing.
    ///
    /// Every piece falls back to its English when it has no translation, so a sentence never
    /// loses words, at worst it mixes the two languages.
    /// </summary>
    public static class ZhText
    {
        public const string Language = "zh-TW";

        public static bool Enabled => Localization.Enabled && uwsettings.instance?.language == Language;

        /// <summary>Joins the pieces and makes sure the fonts have their characters.</summary>
        static string Done(string sentence)
        {
            CjkGlyphs.Ensure(sentence);
            return sentence;
        }

        /// <summary>Pieces joined into one sentence, for the messages composed at their call site.</summary>
        public static string Compose(params string[] pieces) => Done(string.Concat(pieces));

        /// <summary>
        /// A block 1 phrase that the English follows with an object's name ("You destroyed the "),
        /// then the name and a full stop: the translations of those phrases end where the name goes.
        /// </summary>
        public static string PhraseAndName(int phrase, int item_id) =>
            Compose(GameStrings.GetDisplayString(1, phrase), ObjectName(item_id), "。");

        /// <summary>
        /// A shrine's "You have advanced in " with every skill named: 、 between them and 和
        /// before the last, then a full stop.
        /// </summary>
        public static string AdvancedIn(int phrase, System.Collections.Generic.List<string> skills)
        {
            var names = skills.Count <= 1
                ? string.Concat(skills)
                : string.Join("、", skills.GetRange(0, skills.Count - 1)) + "和" + skills[^1];
            return Compose(GameStrings.GetDisplayString(1, phrase), names, "。");
        }

        /// <summary>A creature's name as the player sees it: its own name if it has one, else what it is.</summary>
        public static string CreatureName(uwObject critter) =>
            critter.npc_whoami != 0 ? Localization.Lookup(7, critter.npc_whoami + 16, critter.a_name) : ObjectName(critter.item_id);

        /// <summary>"N out of M", the flasks' way of giving a value against its maximum.</summary>
        public static string OutOf(string phrase, int value, int max) => Compose(phrase.TrimEnd(), $" {value}／{max}。");

        /// <summary>The bare noun for an object, or its English name without the article.</summary>
        public static string ObjectName(int item_id)
        {
            var english = GameStrings.GetString(4, item_id);
            var name = Localization.Lookup(4, item_id, english);
            return name != english ? name : GameStrings.GetSimpleObjectNameUW(item_id);
        }

        /// <summary>"你看到" + what + "。", for the things the English writes as "You see " + what.</summary>
        public static string YouSee(string what) => Done($"你看到{what}。");

        /// <summary>A described object (look.GetDescriptionString), in Chinese order.</summary>
        /// <param name="youSee">Starts with "你看到" and ends the sentence, as the English starts with "You see ".</param>
        /// <param name="qty">How many, said as " N 個" when more than one.</param>
        /// <param name="qualityId">The quality's string in block 5, or -1 for none.</param>
        /// <param name="enchantment">The enchantment as MagicEnchantment.Describe found it, or null.</param>
        /// <param name="ownerRace">The owner's race (its string is 370 + race in block 1), or -1 for none.</param>
        /// <param name="warmthString">A blackrock gem's "warm"/"cool" in block 1 (its translation ends with 的), or -1.</param>
        public static string LookObject(bool youSee, int qty, int qualityId, bool magical, bool cursed,
            int item_id, MagicEnchantment.Description enchantment, int ownerRace, int warmthString = -1)
        {
            var s = youSee ? "你看到" : "";
            if (qty > 1)
            {
                s += $" {qty} 個";
            }
            if (qualityId >= 0)
            {
                s += $"{GameStrings.GetDisplayString(5, qualityId)}的";
            }
            if (magical) { s += "有魔力的"; }
            if (cursed) { s += "受詛咒的"; }
            if (warmthString >= 0) { s += GameStrings.GetDisplayString(1, warmthString); }
            s += ObjectName(item_id);
            if (enchantment != null)
            {
                if (enchantment.Poison)
                {
                    s += "，有毒";
                }
                else if (enchantment.StringNo >= 0)
                {
                    var spell = GameStrings.GetDisplayString(enchantment.StringNo);
                    s += $"，附有「{(spell == "" ? "無名" : spell)}」";
                    if (enchantment.Charges > 0) { s += $"，還有 {enchantment.Charges} 次充能"; }
                    else if (enchantment.Charges == 0) { s += "，已經沒有充能"; }
                }
            }
            if (ownerRace >= 0)
            {
                s += $"，屬於{GameStrings.GetDisplayString(1, 370 + ownerRace).Trim()}";
            }
            if (youSee)
            {
                s += "。";
            }
            return Done(s);
        }

        /// <summary>A creature (npc.LookAt, CritterLook): its mood, what it is, and its name, each only when it has one.</summary>
        /// <param name="attitudeString">The mood's string in block 5, or -1 for none.</param>
        /// <param name="kind">What it is, translated, or null when not said.</param>
        /// <param name="name">Its name, translated, or null.</param>
        /// <param name="named">The name follows the kind ("named"), rather than standing for it.</param>
        public static string LookCreature(int attitudeString, string kind, string name, bool named)
        {
            var s = "你看到";
            if (attitudeString >= 0)
            {
                s += $"{GameStrings.GetDisplayString(5, attitudeString)}的";
            }
            if (kind != null)
            {
                s += kind;
            }
            if (name != null)
            {
                s += named ? $"，名叫{name}" : name;
            }
            return Done(s + "。");
        }

        public static string YouRead(int item_id) => Done($"你讀了{ObjectName(item_id)}……");
    }
}
