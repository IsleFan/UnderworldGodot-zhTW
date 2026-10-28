using System.Diagnostics;

namespace Underworld
{
    public  partial class SpellCasting : UWClass
    {

        /// <summary>
        /// Casts spells from enchanted equipment. Not all spells will cast from here.
        /// </summary>
        /// <param name="majorclass"></param>
        /// <param name="minorclass"></param>
        /// <param name="TriggeredByInventoryEvent">First time trigger from adding to inventory, used for curse objects initial message in UW2</param>
        public static void CastEnchantedItemSpell(int majorclass, int minorclass, bool TriggeredByInventoryEvent, ref int DamageResistance, ref int StealthBonus, int PaperDollSlot)
        {
            switch (majorclass)
            {
                case 0://lighting spells
                    {
                        if (playerdat.lightlevel < minorclass)
                        {
                            playerdat.lightlevel = minorclass;
                        }
                        break;
                    }
                case 1: //Motion abilities
                    {//set relevant bit
                        playerdat.MagicalMotionAbilities = (byte)(playerdat.MagicalMotionAbilities | (1 << (minorclass - 1)));
                        break;
                    }
                case 2: // resistances
                    {//these apply a blanket damage resistance to each body part.
                        if (minorclass > DamageResistance)
                        {
                            DamageResistance = minorclass;
                        }
                        break;
                    }
                case 3://bonuses
                    {
                        CastClass3Enchantment(minorclass, ref StealthBonus);
                        break;
                    }
                case 9://curses
                    {
                        CastClass9_Curse(minorclass, TriggeredByInventoryEvent);
                        break;
                    }
                case 0xB:
                    {
                        CastClassBEnchantment(minorclass); break;
                    }
                case 0xC: //toughness or protection.
                    {   //toughness adds armour, protection makes the body part harder to hit (player_affected_by class 12).
                        //An armour slot covers its location, anything else the body and the hands; the original adds
                        //the toughness to the first of those locations each time (a slip kept here).
                        if (PaperDollSlot < 0)
                        {
                            break;
                        }
                        int[] defenceSlotIndex = { 3, 0, 1, 2, 2 };
                        int[] locations = PaperDollSlot > 4 ? new[] { 0, 1 } : new[] { defenceSlotIndex[PaperDollSlot] };
                        foreach (var location in locations)
                        {
                            var toughness = 0;
                            if ((minorclass & 0x8) != 0)
                            {
                                toughness = (minorclass & 0x7) + 1;
                            }
                            else
                            {
                                playerdat.LocationalProtectionValues[location] += (minorclass & 0x7) + 1;
                            }
                            playerdat.LocationalArmourValues[locations[0]] += toughness;
                        }
                        break;
                    }
                case 0xD:
                    {
                        if (_RES!=GAME_UW2)
                        {
                            switch(minorclass)
                            {
                                case 4://maze navigation
                                {
                                    playerdat.MazeNavigation = true;
                                    break;
                                }
                            }
                        }
                        break;
                    }
                default:
                    Debug.Print($"Unhandled enchantment {majorclass} {minorclass}");
                    break;
            }
        }

        /// <summary>
        /// Casts appropiate spells from objects
        /// </summary>
        /// <param name="spellno"></param>
        /// <param name="caster"></param>
        /// <param name="target">The spell's target, the player when null (a world object's spell is cast on its user).</param>
        /// <param name="tileX">Where it is cast, the player's tile when -1.</param>
        public static void CastSpellFromObject(int spellno, uwObject caster, uwObject target = null, int tileX = -1, int tileY = -1)
        {
            if (((spellno & 0xC0) == 0) && (spellno <= RunicMagic.SpellList.GetUpperBound(0)))
            {
                //do a runic table lookup
                var spell = RunicMagic.SpellList[spellno];
                RunicMagic.CastRunicSpellWithoutRules(spell, caster);
            }
            else
            {
                var major = 12 + ((spellno & 0xC0) >> 6);
                var minor = spellno & 0x3F;
                CastSpell(
                    majorclass: major,
                    minorclass: minor,
                    caster: caster,
                    target: target,
                    tileX: tileX == -1 ? playerdat.playerObject.tileX : tileX,
                    tileY: tileY == -1 ? playerdat.playerObject.tileY : tileY,
                    CastOnEquip: false);
            }
        }

    }//end class
}//end namespace