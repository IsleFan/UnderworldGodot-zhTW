namespace Underworld
{

    public  partial class SpellCasting : UWClass
    {
        /// <summary>
        /// Healing spells: minor d8 hit points, 15 all of them, to a creature (healing, get_hp_back). UW2 heals the
        /// caster, saying so to the player; UW1 heals the target (do_spell).
        /// </summary>
        /// <param name="minorclass"></param>
        public static void CastClass4_Heal(int minorclass, uwObject caster, uwObject target)
        {
            var who = _RES == GAME_UW2 ? caster : (target ?? caster);
            if (who == null || who.majorclass != 1)
            {
                return;
            }
            var healamount = minorclass == 0xF ? 0xFF : Rng.DiceRoll(minorclass, 8);
            if (who.index == playerdat.playerObject.index)
            {
                playerdat.play_hp = System.Math.Min(playerdat.max_hp, playerdat.play_hp + healamount);
                if (_RES == GAME_UW2)
                {
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x124));//You are healed.
                }
            }
            else
            {
                who.npc_hp = (byte)System.Math.Min(critterObjectDat.avghit(who.item_id), who.npc_hp + healamount);
            }
        }
    }//end namespace
}//end class
