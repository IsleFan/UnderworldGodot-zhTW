namespace Underworld
{

    /// <summary>
    /// Trap that moves player to another tile or to another level
    /// </summary>
    public class a_teleport_trap : trap
    {

        public static int Activate(int character, uwObject trapObj, uwObject[] objList)
        {
            if (character != playerdat.playerObject.index)
            {//only the player is teleported; for anyone else the trap does nothing (do_teleport)
                return trapObj.link;
            }
            int NewHeadingHeightTeleportFlag = 0;
            if (_RES == GAME_UW2)
            {
                NewHeadingHeightTeleportFlag = trapObj.xpos & 0x1;
                if (((trapObj.xpos & 0x2)>>1) == 1)
                {
                    NewHeadingHeightTeleportFlag = NewHeadingHeightTeleportFlag | ((trapObj.heading + 8)<<2);
                }
            }
            if ((_RES == GAME_UW2) && ((trapObj.xpos & 0x4) != 0))
            {//fine x bit 2 gets the player lost: the automap goes off and map_scrap remembers the level (TRAP_TELEPORT)
                if (playerdat.AutomapEnabled)
                {
                    playerdat.map_scrap = (byte)playerdat.dungeon_level;
                }
                playerdat.AutomapEnabled = false;
            }
                Teleportation.Teleport(
                    character: 0, 
                    tileX: trapObj.quality, 
                    tileY: trapObj.owner, 
                    newLevel: trapObj.zpos, 
                    HeadingHeightFlag: NewHeadingHeightTeleportFlag);            

            uimanager.FlashColour(1, uimanager.Cuts3DWin, 0.1f);
            return trapObj.link;
        }

    }//end class
}//end namespace