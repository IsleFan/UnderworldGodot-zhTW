
namespace Underworld
{
    public class djinnbottle : objectInstance
    {
        /// <summary>
        /// Breaking the djinn bottle (remove_object, UW2 0x116): before the djinn is captured (xclock 3 below 5) it is
        /// released and kills the player (0xFF damage); at the sigil (level 0x45, 0x15..0x16, 0x34..0x35) once captured it
        /// is absorbed: xclock 3 to 6, the castle clock from 0xD to 0xE, quest 26 bit 1, and the bottle goes. Anywhere else
        /// the screen flashes. Short of the sigil the bottle is left to become debris.
        /// </summary>
        /// <returns>Whether the bottle has gone.</returns>
        public static bool DestroyDjinnBottle(uwObject obj)
        {
            if (playerdat.GetXClock(3) < 5)
            {//player has not coated in treated filanium, baked in lava and cast ironflesh
                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x171));
                damage.DamagePlayer(0xFF, 0, 0);
                return false;
            }
            uimanager.FlashColour(2, uimanager.CutsSmall);
            if ((playerdat.dungeon_level == 0x45) && (obj.tileX >= 0x15) && (obj.tileX <= 0x16) && (obj.tileY >= 0x34) && (obj.tileY <= 0x35))
            {//at the sigil
                playerdat.SetXClock(3, 6);//djinn has been captured
                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x150));
                if (playerdat.GetXClock(1) == 0xD)
                {
                    playerdat.SetXClock(1, 0xE);
                }
                playerdat.SetQuest(105, 1);
                ObjectRemover_OLD.DeleteObjectFromTile_DEPRECIATED(obj.tileX, obj.tileY, obj.index, true);
                return true;
            }
            return false;
        }
    }//end class
}//end namespace