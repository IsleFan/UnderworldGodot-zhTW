using Peaky.Coroutines;
namespace Underworld
{
    /// <summary>
    /// Class for interactions involving the look verb
    /// </summary>
    public class look : UWClass
    {
        /// <summary>
        /// inv_look's identification: the first look rolls skill_check(Lore, UW1 10 / UW2 8) + 1 (at least 1), keeps the
        /// best result in the heading (bit 2 marks the attempt) and is not rerolled until a better Lore clears the mark.
        /// </summary>
        public static int LoreCheck(uwObject obj)
        {
            if (
                CanBeIdentified(obj)
                )
            {//can be identified
                if ((obj.heading & 0x4) == 0)
                {//no attempt has been made yet. try and id now
                    var result = (int)playerdat.SkillCheck(playerdat.Lore, _RES == GAME_UW2 ? 8 : 0xA);
                    result++;
                    if (result == 0)
                    {
                        result = 1;
                    }
                    if (result < (obj.heading & 0x3))
                    {
                        result = obj.heading & 0x3;//make sure identification does not lose a previous ID attempt if bit 3 has changed due to a lore skill increase
                    }
                    obj.heading = (short)(4 | result); //store result and flag that attempt was made.
                    return result; //1,2 or 3
                }
                else
                {
                    return obj.heading & 0x3; //return previous result
                }
            }
            return 1;//fail or cannot be identified
        }

        public static bool CanBeIdentified(uwObject obj)
        {
            return (obj.majorclass != 5)
                    &&
                    (obj.majorclass != 6)
                    &&
                    (commonObjDat.rendertype(obj.item_id) != 2);
        }

        /// <summary>
        /// player_3dlook: looks with lore 1 at an object in close reach that cannot be picked up, else lore 0. With the look
        /// icon selected a found trap may then be disarmed (the question fires the look triggers after it); otherwise the
        /// object's look triggers fire.
        /// </summary>
        /// <param name="lookMode">The look icon is selected (RightButtonThing 3), or modern controls, which always search.</param>
        public static void LookAtWorld(int index, int lore, bool lookMode)
        {
            var objList = UWTileMap.current_tilemap.LevelObjects;
            if (index > objList.GetUpperBound(0))
            {
                return;
            }
            var obj = objList[index];
            trap.ObjectThatStartedChain = index;
            LookAt(obj, objList, lore);
            uimanager.NextOutputPrependedString = "";//turn off any "writing reads" messages
            if (lookMode && trapdisarming.DetectTrapTrigger(index, objList, playerdat.Search) > 0)
            {//you've found a trap.
                _ = Coroutine.Run(
                    trapdisarming.DisarmAttemptQuestion(index, objList),
                    main.instance
                    );
                return;
            }
            trigger.TriggerObjectLink(
                character: 1,
                ObjectUsed: obj,
                triggerType: (int)triggerObjectDat.triggertypes.LOOK,
                triggerX: obj.tileX,
                triggerY: obj.tileY,
                objList: objList);
        }

        /// <summary>
        /// inv_look: the object's look triggers first, then a look with the identification of LoreCheck. No trap search.
        /// </summary>
        public static void LookAtInventory(int index)
        {
            var objList = playerdat.InventoryObjects;
            if (index > objList.GetUpperBound(0) || objList[index] == null)
            {
                return;
            }
            var obj = objList[index];
            trap.ObjectThatStartedChain = index;
            trigger.TriggerObjectLink(
                character: 1,
                ObjectUsed: obj,
                triggerType: (int)triggerObjectDat.triggertypes.LOOK,
                triggerX: obj.tileX,
                triggerY: obj.tileY,
                objList: objList);
            if (objList[index] != obj)
            {//the trigger took it
                return;
            }
            LookAt(obj, objList, LoreCheck(obj));
            uimanager.NextOutputPrependedString = "";
        }

        /// <summary>
        /// Prints to the conversation's scroll while a look runs from a conversation (trade, inventory), else to the message scroll.
        /// </summary>
        static bool OutputToConvo = false;

        public static void Print(string text)
        {
            if (OutputToConvo)
            {
                uimanager.AddToMessageScroll(
                    stringToAdd: text,
                    option: 2,
                    mode: MessageDisplay.MessageDisplayMode.TemporaryMessage);
            }
            else
            {
                uimanager.AddToMessageScroll(text);
            }
        }

        /// <summary>
        /// LookAt: a lookable object gets its description line ('You see ...'), a critter its CritterLook, a decal its
        /// RectLook; then SpecialLook adds what books, keys, bones and doors say for themselves. At lore 3 a UW1 talisman
        /// is described by its own text instead. Lore 2 shows that an object is magical, 3 names its enchantment.
        /// </summary>
        public static void LookAt(uwObject obj, uwObject[] objList, int lore, bool OutputConvo = false)
        {
            if (obj == null)
            {
                return;
            }
            OutputToConvo = OutputConvo;
            if (commonObjDat.PrintableLook(obj.item_id))
            {
                if ((_RES != GAME_UW2) && (lore == 3) && TalismanDescription(obj))
                {
                    OutputToConvo = false;
                    return;
                }
                if (obj.majorclass == 1)
                {
                    npc.LookAt(obj);
                    OutputToConvo = false;
                    return;
                }
                Print(GetDescriptionString(obj: obj, objList: objList, lorecheckresult: lore));
            }
            else if (obj.OneF0Class == 0x16)
            {
                RectLook(obj, lore);
            }
            SpecialLook(obj, objList, lore);
            OutputToConvo = false;
        }

        /// <summary>
        /// RectLook for the decals: the texture behind a texture map object (a UW1 shaft only from close by), a bridge,
        /// a gravestone, a plaque.
        /// </summary>
        static void RectLook(uwObject obj, int lore)
        {
            switch (obj.classindex)
            {
                case 4:
                    bridge.LookAt(obj); break;
                case 5:
                    gravestone.Use(obj); break;
                case 6:
                    writing.LookAt(obj); break;
                case 0xE:
                case 0xF:
                    tmap.LookAt(obj, showShaft: lore > 0); break;
            }
        }

        /// <summary>
        /// SpecialLook: the second line of a look for books and scrolls (UW2 below class index 10), keys, bones, and a
        /// spiked closed door (UW1 0x83; UW2's 0x91 is an empty string).
        /// </summary>
        static void SpecialLook(uwObject obj, uwObject[] objList, int print)
        {
            switch (obj.majorclass)
            {
                case 4:
                    if (obj.minorclass == 3 && ((_RES != GAME_UW2) || (obj.classindex < 10)))
                    {
                        Readable.BookLook(obj, print);
                    }
                    else if (obj.minorclass == 0)
                    {//KeyLook: the key's description, 0x64 + owner of block 5
                        if (print != 0)
                        {
                            var str = GameStrings.GetDisplayString(5, obj.owner + 0x64);
                            if (str != "")
                            {
                                Print(str);
                            }
                        }
                    }
                    break;
                case 3:
                    if (obj.minorclass == 0 && obj.item_id >= 0xC2 && obj.item_id <= 0xC6)
                    {
                        BonesLook(obj, print);
                    }
                    break;
                case 5:
                    if ((_RES != GAME_UW2) && (obj.minorclass == 0) && (obj.classindex < 8) && ((obj.owner & 1) != 0))
                    {
                        Print(GameStrings.GetDisplayString(1, 0x83));//the door is spiked
                    }
                    break;
            }
        }

        /// <summary>
        /// BonesLook: whose bones, 'It looks to be that of ' (a pile or more than one: 'They look to be those of ') and the
        /// creature from the owner field (+ 0x40), or an adventurer (0x3F). Owners 0, 0x28 and 0x3C..0x3E say nothing.
        /// </summary>
        static void BonesLook(uwObject obj, int print)
        {
            var owner = obj.owner;
            if (print == 0 || owner == 0 || owner == 0x28 || (owner >= 0x3C && owner != 0x3F))
            {
                return;
            }
            var id = _RES == GAME_UW2 ? 0x1A : 0x16;
            if (obj.item_id == 0xC6 || obj.link > 1)
            {
                id++;
            }
            var creature = owner + 0x40;
            if (ZhText.Enabled)
            {
                Print(GameStrings.GetDisplayString(1, id) + ZhText.ObjectName(creature) + "。");
            }
            else
            {
                Print(GameStrings.GetString(1, id) + (owner == 0x3F ? "an adventurer." : $"{GameStrings.GetName(creature, true, false)}."));
            }
        }

        /// <summary>
        /// talisman_desc (UW1): at lore 3 an object of COMOBJ fate 10 is described as 'You see ' (0x104) and its own text,
        /// 0x105 + which talisman it is. Returns false for anything else.
        /// </summary>
        static bool TalismanDescription(uwObject obj)
        {
            if (commonObjDat.projectilecullingpriority(obj.item_id) != 10)
            {
                return false;
            }
            var which = System.Array.IndexOf(new[] { 0x136, 0x93, 0x97, 0xBF, 0x11F, 0x37, 0xAE, 0x0A, 0x36 }, obj.item_id);
            if (which < 0)
            {
                return false;
            }
            Print(GameStrings.GetDisplayString(1, 0x104) + GameStrings.GetDisplayString(1, 0x105 + which));
            return true;
        }

        /// <summary>
        /// The description line of LookAt: 'You see' + count or article + quality word + do_mods' word + name + do_of's
        /// words + 'belonging to' the owner's race. The article comes from the first word before the name (a count takes
        /// none, nor quality type 13), or else from the name's own ("a_", "some_", "the_", or none).
        /// </summary>
        /// <param name="lorecheckresult">How fully description of enchantments the string is</param>
        public static string GetDescriptionString(uwObject obj, uwObject[] objList, int lorecheckresult, bool IncludeYouSee = true)
        {
            var youSee = IncludeYouSee;
            var text = youSee ? GameStrings.GetString(1, GameStrings.str_you_see_) : "";

            var first = '\0';
            var mods = DoMods(obj, objList, lorecheckresult, out var setsArticle, out var magical, out var cursed, out var warmth);
            if (setsArticle)
            {
                first = mods[0];
            }

            var q = 0;
            if ((obj.quality != 0) && ((_RES != GAME_UW2) || (obj.quality > 1) || ((obj.item_id & 0x1F8) != 0x90)))
            {//UW2 shows no quality word for a light at quality 1
                q = commonObjDat.qualityclass(obj.item_id) != 3 ? (obj.quality >> 4) + 1 : 5;
            }
            var qualitytype = commonObjDat.qualitytype(obj.item_id);
            var qualitystringid = qualitytype * 6 + q;
            var qualitystring = GameStrings.GetString(5, qualitystringid);
            if (qualitystring.Length > 0)
            {
                first = qualitystring[0];
            }

            var plural = false;
            if ((obj.is_quant == 1) && ((obj.link & 0x200) == 0) && (obj.link > 1))
            {
                plural = true;
                first = 'x';
                text += $"{obj.link} ";
            }
            else if ((first != '\0') && (qualitytype != 13))
            {
                text += "aeiou".IndexOf(first) >= 0 ? "an " : "a ";
            }
            if (qualitystring.Length > 0)
            {
                text += $"{qualitystring} ";
            }
            text += mods;
            text += GameStrings.GetName(obj.item_id, article: first == '\0', plural: plural);
            text += DoOf(obj, objList, lorecheckresult, out var enchantment);

            var ownerRace = -1;
            if (commonObjDat.canhaveowner(obj.item_id) && (obj.owner > 0) && (obj.race <= (_RES == GAME_UW2 ? 0x1E : 0x1B)))
            {
                ownerRace = obj.race;
                text += $" belonging to{GameStrings.GetString(1, 0x172 + obj.race)}";
            }

            if (ZhText.Enabled)
            {
                return ZhText.LookObject(
                    youSee: youSee,
                    qty: plural ? obj.link : 0,
                    qualityId: qualitystring.Length > 0 ? qualitystringid : -1,
                    magical: magical,
                    cursed: cursed,
                    item_id: obj.item_id,
                    enchantment: enchantment,
                    ownerRace: ownerRace,
                    warmthString: warmth);
            }
            return youSee ? $"{text}." : text;
        }

        /// <summary>
        /// UW2 GetObjDesc, the short description the enchanting and mending messages use: the quality word and the
        /// name without its article, plural for a stack.
        /// </summary>
        public static string GetObjDesc(uwObject obj)
        {
            var q = 0;
            if (obj.quality != 0)
            {
                q = commonObjDat.qualityclass(obj.item_id) != 3 ? (obj.quality >> 4) + 1 : 5;
            }
            var qual = GameStrings.GetString(5, commonObjDat.qualitytype(obj.item_id) * 6 + q);
            var qty = ((obj.is_quant == 1) && ((obj.link & 0x200) == 0)) ? obj.link : 1;
            return (qual != "" ? $"{qual} " : "") + GameStrings.GetName(obj.item_id, article: false, plural: qty > 1);
        }

        /// <summary>
        /// The conversation's identify_inv (conv_inv_name): an article (when describe is set, by do_mods' word or the
        /// name's own) or a count, do_mods' word, the name and do_of's words; no quality word and no owner.
        /// </summary>
        public static string ConversationItemName(uwObject obj, uwObject[] objList, bool describe, int ident)
        {
            var qty = ((obj.is_quant == 1) && ((obj.link & 0x200) == 0)) ? obj.link : 1;
            var mods = DoMods(obj, objList, ident, out var setsArticle, out var magical, out var cursed, out var warmth);
            var name = "";
            if (setsArticle && describe && (qty == 1))
            {
                name = "aeiou".IndexOf(mods[0]) >= 0 ? "an " : "a ";
                describe = false;
            }
            else if (qty > 1)
            {
                name = $"{qty} ";
                describe = false;
            }
            name += mods + GameStrings.GetName(obj.item_id, article: describe, plural: qty > 1);
            name += DoOf(obj, objList, ident, out var enchantment);
            if (ZhText.Enabled)
            {
                return ZhText.LookObject(youSee: false, qty: qty > 1 ? qty : 0, qualityId: -1, magical: magical, cursed: cursed,
                    item_id: obj.item_id, enchantment: enchantment, ownerRace: -1, warmthString: warmth);
            }
            return name;
        }

        /// <summary>
        /// Whether decode_obj_spell looks at obj: a container only when a spell object is among its contents.
        /// </summary>
        static MagicEnchantment DecodeSpell(uwObject obj, uwObject[] objList, bool alwaysDecode)
        {
            if ((obj.majorclass == 2) && (obj.minorclass == 0))
            {
                var linkedspell = objectsearch.FindMatchInObjectChain(
                    ListHeadIndex: obj.link,
                    majorclass: 4, minorclass: 2, classindex: 0,
                    objList: objList,
                    SkipNext: false,
                    SkipLinks: true);
                if ((linkedspell == null) || (linkedspell.item_id != 288))
                {
                    return null;
                }
            }
            MagicEnchantment.AlwaysDecode = alwaysDecode;
            var spell = MagicEnchantment.GetSpellEnchantment(obj, objList);
            MagicEnchantment.AlwaysDecode = false;
            return spell;
        }

        /// <summary>
        /// do_mods, the word before the name. Its spell decode has no always_decode, so an uncharged spell may go unseen.
        /// UW1: 'magical ' at lore 2; at lore 3 a curse adds 'cursed ' without giving the article.
        /// UW2: 'cursed ' at lore 3, 'magical ' at lore 2 or for a magic book up to class index 8, and 'warm '/'cool '
        /// for a blackrock gem by whether its owner is set.
        /// </summary>
        /// <param name="setsArticle">The article goes by this word's first letter.</param>
        /// <param name="warmth">The blackrock gem's string in block 1, or -1.</param>
        static string DoMods(uwObject obj, uwObject[] objList, int lore, out bool setsArticle, out bool magical, out bool cursed, out int warmth)
        {
            var s = "";
            setsArticle = false; magical = false; cursed = false; warmth = -1;
            var spell = DecodeSpell(obj, objList, alwaysDecode: false);
            if (spell != null)
            {
                var curse = !spell.IsFlagBit2Set && spell.SpellMajorClass == 9;
                if (_RES != GAME_UW2)
                {
                    if (lore == 2)
                    {
                        s = "magical ";
                        magical = true;
                        setsArticle = true;
                    }
                    else if (lore == 3 && curse)
                    {
                        s = "cursed ";
                        cursed = true;
                    }
                }
                else
                {
                    if (lore == 3)
                    {
                        if (curse)
                        {
                            s = "cursed ";
                            cursed = true;
                            setsArticle = true;
                        }
                    }
                    else if ((lore == 2) || ((obj.OneF0Class == 0x13) && (obj.classindex <= 8)))
                    {
                        s = "magical ";
                        magical = true;
                        setsArticle = true;
                    }
                }
            }
            if ((_RES == GAME_UW2) && ((obj.item_id & 0x1F8) == 0x118))
            {
                warmth = obj.owner > 0 ? 0x165 : 0x164;
                s += GameStrings.GetString(1, warmth);
                setsArticle = true;
            }
            return s;
        }

        /// <summary>
        /// do_of, the words after the name, at lore 3 only: UW2 ' of Poison' for a potion holding a damage trap, else
        /// ' of ' + the enchantment ('UNNAMED' without a string) and, for a non-stack holding a charged spell object
        /// (id bit 11), ' with N full charge(s)' (a UW2 potion says 1). A curse has none.
        /// </summary>
        /// <param name="enchantment">The same in pieces for ZhText, or null.</param>
        static string DoOf(uwObject obj, uwObject[] objList, int lore, out MagicEnchantment.Description enchantment)
        {
            enchantment = null;
            if (lore != 3)
            {
                return "";
            }
            var potion = (_RES == GAME_UW2) && (obj.item_id >= 0xE1) && (obj.item_id <= 0xE7);
            if (potion && (obj.is_quant == 0))
            {
                var damagetrap = objectsearch.FindMatchInObjectChain(
                    ListHeadIndex: obj.link,
                    majorclass: 6, minorclass: 0, classindex: 0,
                    objList: objList,
                    SkipNext: false,
                    SkipLinks: true);
                if (damagetrap != null)
                {
                    enchantment = new MagicEnchantment.Description(Poison: true, Cursed: false, StringNo: -1, Charges: -2);
                    return " of Poison";
                }
            }
            var spell = DecodeSpell(obj, objList, alwaysDecode: true);
            if ((spell == null) || (spell.SpellMajorClass == 9))
            {
                return "";
            }
            var described = spell.Describe(obj, objList, 3);
            var name = GameStrings.GetString(described.StringNo);
            var s = $" of {(string.IsNullOrEmpty(name) ? "UNNAMED" : name)}";
            var charges = -2;
            if (obj.is_quant == 0)
            {
                var spellObject = objectsearch.FindMatchInObjectChain(
                    ListHeadIndex: obj.link,
                    majorclass: 4, minorclass: 2, classindex: 0,
                    objList: objList,
                    SkipNext: false,
                    SkipLinks: true);
                if ((spellObject != null) && (spellObject.flags2 == 1))
                {
                    charges = spellObject.quality;
                    if (potion && charges > 0)
                    {
                        charges = 1;
                    }
                    s += $" with {(charges > 0 ? charges.ToString() : "no")} full charge{(charges != 1 ? "s" : "")}";
                }
            }
            enchantment = described with { Charges = charges };
            return s;
        }


        public static string GetArticle(string noun, bool caps = false)
        {
            if (
                (noun.ToUpper().StartsWith("A"))
                ||
                (noun.ToUpper().StartsWith("E"))
                ||
                (noun.ToUpper().StartsWith("I"))
                ||
                (noun.ToUpper().StartsWith("O"))
                ||
                (noun.ToUpper().StartsWith("U"))
                )
            {
                if (caps)
                {
                    return "An ";
                }
                else
                {
                    return "an ";
                }

            }
            else
            {
                if (caps)
                {
                    return "A ";
                }
                else
                {
                    return "a ";
                }

            }
        }
    }//end class
}//end namespace
