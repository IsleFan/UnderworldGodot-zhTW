using System.Diagnostics;

namespace Underworld
{
    /// <summary>
    /// Object enchantment and spells. For spells cast by objects (exclude runic magic)
    /// </summary>
    public class MagicEnchantment : UWClass
    {

        public uwObject LinkedSpellObject;
        public int SpellMajorClass;
        public int SpellMinorClass;
        public bool IsFlagBit2Set;

        public MagicEnchantment(int _major, int _minor, bool _isflag2set, uwObject _linkedSpellObject)
        {
            SpellMajorClass = _major;
            SpellMinorClass = _minor;
            IsFlagBit2Set = _isflag2set;
            LinkedSpellObject = _linkedSpellObject;
        }

        /// <summary>
        /// UW2's potions, FIRST_POTION (0xE1) to ITEM_BROWN_POTION (0xE7); 0xE0 is a runestone.
        /// </summary>
        public static bool IsPotion(uwObject obj)
        {
            return (_RES == GAME_UW2) && (obj.item_id >= 0xE1) && (obj.item_id <= 0xE7);
        }

        /// <summary>
        /// What NameEnchantment says, in pieces, for sentences composed in another word order (ZhText).
        /// </summary>
        /// <param name="StringNo">The enchantment's full string number, or -1 when poison or cursed.</param>
        /// <param name="Charges">Charges left, 0 for none left, -2 when the enchantment has no charges.</param>
        public record Description(bool Poison, bool Cursed, int StringNo, int Charges);

        /// <summary>
        /// The enchantment of obj, or null when the lore check failed.
        /// </summary>
        public Description Describe(uwObject obj, uwObject[] objList, int LoreCheck = 3)
        {
            var stringNo = 0;
            //a poisoned potion is look.DoOf's, before any spell
            if (LoreCheck == 3)
            {//passed lorecheck
                if (SpellMajorClass == 0xC)
                {
                    stringNo = 0x1C0;
                    if (obj.minorclass >=2)
                    {
                        stringNo+=16;
                    }
                    stringNo+= SpellMinorClass;
                }
                else
                {
                    if (SpellMajorClass==9)
                        {
                            return new Description(Poison: false, Cursed: true, StringNo: -1, Charges: -2);
                        }
                    else
                        {
                            if (IsFlagBit2Set)
                            {
                                if (SpellMajorClass>0)
                                {
                                    stringNo = SpellMinorClass + (SpellMajorClass<<4);
                                }
                                else
                                {
                                    stringNo = 0x100 + SpellMinorClass;
                                }
                                
                            }
                            else
                            {
                                stringNo = SpellMinorClass + (SpellMajorClass<<4);
                            }                            
                        }
                }

                //try and determine charges
                int charges = -2;
                    var spell = objectsearch.FindMatchInObjectChain(
                        ListHeadIndex: obj.index, 
                        majorclass: 4, 
                        minorclass: 2, 
                        classindex: 0, 
                        objList: objList, 
                        SkipNext: true);
                    if (spell!=null)
                    {
                        if (spell.flags2 ==1)
                        {
                            var NoOfCharges = spell.quality;
                            if (IsPotion(obj))
                            {
                                NoOfCharges = 1;
                            }
                            charges = System.Math.Max(0, (int)NoOfCharges);
                        }
                    }


                stringNo = 0xC00 | stringNo;
                Debug.Print($"{SpellMajorClass},{SpellMinorClass}->{stringNo}");
                return new Description(Poison: false, Cursed: false, StringNo: stringNo, Charges: charges);
            }
            else
            {
                //failed lore check
                return null;
            }
        }

        /// <summary>
        /// Returns the name of the enchantement
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="objList"></param>
        /// <param name="LoreCheck"></param>
        /// <returns></returns>
        public string NameEnchantment(uwObject obj, uwObject[] objList, int LoreCheck = 3)
        {
            var d = Describe(obj, objList, LoreCheck);
            if (d == null)
            {
                return "";
            }
            if (d.Cursed)
            {
                return "cursed";
            }
            string charges = "";
            if (d.Charges == 0)
            {
                charges = " with no charges remaining";
            }
            else if (d.Charges > 1)
            {
                charges = $" with {d.Charges} full charges remaining";
            }
            else if (d.Charges == 1)
            {
                charges = $" with {d.Charges} full charge remaining";
            }
            return $"{GameStrings.GetString(d.StringNo)}{charges}";
        }

        /// <summary>
        /// always_decode: set while looking, so a spell object with no charges is never hidden.
        /// </summary>
        public static bool AlwaysDecode = false;

        public static MagicEnchantment GetSpellEnchantment(uwObject obj, uwObject[] objList)
        {
            uwObject _linkedSpell = null;
            if (obj.majorclass == 6)
            {
                return null;
            }
            else
            {                
                if ((obj.is_quant == 1) || (obj.is_quant == 0 && obj.link == 0))
                {
                    if (obj.is_quant == 0)
                    {
                        //check obj itself.
                        obj = null; //no match
                    }
                    else
                    {
                        if (obj.enchantment == 0)
                            {
                                obj = null; //no match
                            }
                        else
                            {//has enchantment set
                                if (obj.majorclass == 5)
                                {
                                    obj = null;
                                }
                                else
                                {
                                    //obj is the object to test.
                                }
                            }
                    }
                }
                else
                {//try and find a linked spell
                    obj = objectsearch.FindMatchInObjectChain(
                        ListHeadIndex: obj.link, 
                        majorclass: 4, 
                        minorclass: 2, 
                        classindex: 0, 
                        objList: objList,
                        SkipLinks: true);//decode_obj_spell: in the contents only, not inside them
                    if (obj!=null)
                    {
                        if ((obj.quality == 0) && !AlwaysDecode && ((Rng.r.Next(0x8000) * 10) / 0x8000 < 4))
                        {//a spell with no charges left is hidden 4 times in 10 (looking always sees it)
                            return null;
                        }
                        _linkedSpell = obj;
                    }
                }

                if (obj!=null)
                {
                    var major =-1; var minor=-1; 
                    bool flag2;
                    if (obj.flags2 ==1)
                    {
                        flag2 = true;
                        major = (obj.link & 0x1FF)>>6;
                        if (major == 0)
                        {
                            major = -1;
                        }
                        else
                        {
                            major += 0xC;
                        }
                        minor = obj.link & 0x3F;
                    }
                    else
                    {
                        flag2 = false;
                        major = (obj.link & 0x1FF)>>4; //note this >>4 is different from the above >> 6
                        minor = obj.link & 0xF;
                    }

                    return new MagicEnchantment(
                        _major: major,
                        _minor: minor, 
                        _isflag2set: flag2,
                        _linkedSpellObject: _linkedSpell
                        );
                }
                else
                {
                    return null;
                }
            }
        }     


        /// <summary>
        /// Casts a spell from a object. eg wand.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="spell"></param>
        /// <param name="fromTheObject">checkSpell in the world: the object itself is the source, at its tile, on its user (inanimate_spell).</param>
        public static bool CastObjectSpell(uwObject obj, MagicEnchantment spell, bool cooldown = true, bool fromTheObject = false)
        {//checkSpell: the player using the object is the caster. Casts are one per 0x2FD clock ticks (food and potions
         //are exempt, the original clears the timer around them); too soon, nothing is cast and the charge is kept.
            use.SpellHasBeenCast = true;
            if (cooldown)
            {
                if ((playerdat.ClockValue < NextSpellTime) && (NextSpellTime - playerdat.ClockValue <= 0x2FD))//(a clock gone back, an older save loaded, does not count)
                {
                    UWsoundeffects.PlaySoundEffectAtAvatar(0x15, 0x40, 0);
                    if (_RES == GAME_UW2)
                    {
                        uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0xB));//You are not yet ready to cast another spell.
                    }
                    return false;
                }
                NextSpellTime = playerdat.ClockValue + 0x2FD;
            }
            var spellno = spell.SpellMinorClass;
            if (spell.SpellMajorClass != -1)
            {
                spellno = spellno |= (spell.SpellMajorClass << 6);
            }
            if (fromTheObject)
            {
                SpellCasting.CastSpellFromObject(spellno, obj, playerdat.playerObject, obj.tileX, obj.tileY);
            }
            else
            {
                SpellCasting.CastSpellFromObject(spellno, playerdat.playerObject);
            }
            return true;
        }

        static long NextSpellTime = 0;

        /// <summary>A restored game may cast from objects at once (reset_game clears nextSpellTime).</summary>
        public static void ResetSpellTimer()
        {
            NextSpellTime = 0;
        }


        /// <summary>
        /// Reducees the quality of a linked spell object to reflect reducing spell charges.
        /// </summary>
        /// <param name="objList"></param>
        /// <param name="WorldObject"></param>
        /// <param name="parentObject"></param>
        /// <param name="spell"></param>
        /// <returns>True when spell has ran out.</returns>
        public static bool DecrementSpellQuality(uwObject[] objList, bool WorldObject, uwObject parentObject, MagicEnchantment spell)
        {
            //useNSpellCharges: only a cast-on-use spell (id bit 11) spends charges; with none left it is deleted 4 times in 10
            if (spell.LinkedSpellObject.flags2 == 0)
            {
                return false;
            }
            if (spell.LinkedSpellObject.quality > 0)
            {
                spell.LinkedSpellObject.quality--;
            }
            else if ((Rng.r.Next(0x8000) * 10) / 0x8000 < 4)
            {
                //spell has ran out. unlink it from the object's contents (the rest of the contents stay)
                if (WorldObject)
                {
                    ObjectRemover_OLD.RemoveObjectFromLinkedList(parentObject.link, spell.LinkedSpellObject.index, objList, parentObject.PTR + 6);
                    ObjectFreeLists.ReleaseFreeObject(spell.LinkedSpellObject);
                }
                else
                {
                    playerdat.RemoveFromInventory(spell.LinkedSpellObject.index, ClearLink: true, updateUI: false);
                }
                return true;//spell has ran out
            }
            return false;
        }

    }//end class
}//end namespace