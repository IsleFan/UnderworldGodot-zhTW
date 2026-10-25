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

        public static bool IsPotion(uwObject obj, bool UW2Only = true)
        {
            if (_RES==GAME_UW2)
            {
                return (obj.item_id>=224 && obj.item_id<=231);
            }
            else
            {
                if (UW2Only)
                {
                    return false;// hack for checking enchantments in uw2
                }
                else
                {
                    return (obj.item_id>=187 && obj.item_id<=188);
                }
            }
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
            if (IsPotion(obj, false))
            {
                if (obj.is_quant == 0)
                {//check for poisoned potions
                    var damagetrap = objectsearch.FindMatchInObjectChain(obj.link, 6,0,0,objList);
                    if (damagetrap  != null)
                    {
                        return new Description(Poison: true, Cursed: false, StringNo: -1, Charges: -2);
                    }
                }
            }
            //if not a poisoned potion check for other causes.
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
            if (d.Poison)
            {
                return " of poison";
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
                        objList: objList);
                    if (obj!=null)
                    {
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
        public static void CastObjectSpell(uwObject obj, MagicEnchantment spell)
        {
            use.SpellHasBeenCast = true;
            var spellno = spell.SpellMinorClass;
            if (spell.SpellMajorClass != -1)
            {
                spellno = spellno |= (spell.SpellMajorClass << 6);
            }
            SpellCasting.CastSpellFromObject(spellno, obj);
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
            if (spell.LinkedSpellObject.quality > 0)
            {
                spell.LinkedSpellObject.quality--;
            }
            else
            {
                //spell has ran out. unlink it
                var next = parentObject.link;
                var previous = 0;
                while (next != 0)
                {
                    var nextObj = objList[next];
                    if (next == spell.LinkedSpellObject.index)
                    {
                        if (WorldObject)
                        {
                            if (previous == 0)
                            {
                                parentObject.link = 0;
                            }
                            else
                            {
                                var previousObject = objList[previous];
                                previousObject.link = 0;
                            }
                            ObjectFreeLists.ReleaseFreeObject(spell.LinkedSpellObject);                            
                        }
                        else
                        {
                            playerdat.RemoveFromInventory(spell.LinkedSpellObject.index, ClearLink: true, updateUI: false);
                        }
                        return true;//spell has ran out
                    }
                    next = nextObj.next;
                    previous = nextObj.index;
                }
            }
            return false;
        }

    }//end class
}//end namespace