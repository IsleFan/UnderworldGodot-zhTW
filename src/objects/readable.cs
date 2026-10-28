using System.Diagnostics;

namespace Underworld
{
    public class Readable : objectInstance
    {

        /// <summary>
        /// UseBook (the maps are map.Use and a_bit_of_a_map.Use): only from the inventory. A plain one (or a decal's)
        /// plays cutscene 0x100 + its text with id bit 10, reads a text below 0x100 after 'You read the <name>...', and
        /// above that is UW1's stew recipe (UW2's make_stew does nothing). An enchanted one casts its spell, used up when it casts.
        /// </summary>
        public static bool Use(uwObject ObjectUsed, bool WorldObject)
        {
            if (WorldObject)
            {
                return true;
            }
            if ((ObjectUsed.enchantment == 0) || (ObjectUsed.majorclass == 5))
            {
                var text = ObjectUsed.link & 0x1FF;
                if (ObjectUsed.flags1 == 1)
                {
                    uimanager.EnableDisable(uimanager.instance.uwviewport, false);
                    cutsplayer.PlayCutscene(CutsceneNo: 0x100 + text, callBackMethod: cutsplayer.RestoreViewPort, useSingleRedChannel: true);
                }
                else if (text < 0x100)
                {
                    ReadWithName(ObjectUsed, text);
                }
                else if (_RES != GAME_UW2)
                {
                    return rotwormstew.MixRotwormStew();
                }
                return true;
            }
            var magicenchantment = MagicEnchantment.GetSpellEnchantment(ObjectUsed, playerdat.InventoryObjects);
            if ((magicenchantment != null) && MagicEnchantment.CastObjectSpell(ObjectUsed, magicenchantment))
            {//a scroll is used up only when it casts
                ObjectCreator.Consume(ObjectUsed, true);
            }
            return true;
        }

        /// <summary>
        /// 'You read the <name>...' (UW1 'UNNAMED' without a name) and text from block 3; UW2 text 6 sets quest 26 bit 2.
        /// </summary>
        static void ReadWithName(uwObject obj, int text)
        {
            if (ZhText.Enabled)
            {
                look.Print(ZhText.YouRead(obj.item_id));
            }
            else
            {
                var name = GameStrings.GetName(obj.item_id, article: false, plural: false);
                look.Print($"You read the {((name == "") && (_RES != GAME_UW2) ? "UNNAMED" : name)}...");
            }
            if ((_RES == GAME_UW2) && (text == 6))
            {
                playerdat.SetQuest(106, 1);//You have looked at Mors Gothris spellbook.
            }
            look.Print(GameStrings.GetDisplayString(3, text));
        }

        /// <summary>
        /// BookLook, the second line of a look at a book or scroll (print 1 or more): the map says 'Enscribed upon the
        /// scroll is your map.' (UW1 0x97, UW2 0xA5), a UW2 bit of a map and an enchanted one nothing, one with id bit 10
        /// plays cutscene 0x100 + its text; a text number above 0xFF is printed alone, a lower one after 'You read the
        /// <name>...'. UW2 text 6 sets quest 26 bit 2.
        /// </summary>
        public static void BookLook(uwObject obj, int print)
        {
            if (print < 1)
            {
                return;
            }
            if (obj.item_id == (_RES == GAME_UW2 ? 0x13A : 0x13B))
            {
                look.Print(GameStrings.GetDisplayString(1, _RES == GAME_UW2 ? 0xA5 : 0x97));
                return;
            }
            if ((_RES == GAME_UW2) && (obj.item_id == 0x139))
            {
                return;
            }
            if ((obj.enchantment == 1) && (obj.majorclass != 5))
            {
                return;
            }
            var text = obj.link & 0x1FF;
            if (obj.flags1 == 1)
            {
                uimanager.EnableDisable(uimanager.instance.uwviewport, false);
                cutsplayer.PlayCutscene(CutsceneNo: 0x100 + text, callBackMethod: cutsplayer.RestoreViewPort, useSingleRedChannel: true);
                return;
            }
            if (text > 0xFF)
            {
                look.Print(GameStrings.GetDisplayString(3, text));
                return;
            }
            ReadWithName(obj, text);
        }

    }//end class
}//end namespace