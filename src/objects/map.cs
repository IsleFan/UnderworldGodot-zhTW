namespace Underworld
{

    /// <summary>
    /// The map scroll
    /// </summary>
    public class map : objectInstance
    {
        public static bool Use(uwObject obj, bool WorldObject)
        {
            if (WorldObject) { return false; }
            if (_RES == GAME_UW2)
            {
                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x8E));//You unroll your map.
                //AutoMap: a lost player sees the level of the last map shown instead of this one,
                //and a map scrap just copied shows its level
                var level = playerdat.dungeon_level;
                if (!playerdat.AutomapEnabled && playerdat.map_scrap != 0)
                {
                    level = playerdat.map_scrap;
                }
                if ((playerdat.map_scrap & 0x80) != 0)
                {
                    playerdat.map_scrap = (byte)~playerdat.map_scrap;
                    level = playerdat.map_scrap;
                }
                uimanager.DrawAutoMap(level - 1, worlds.GetWorldNo(level));
                if (XMIMusic.CurrentlyPlayingThemeNo < 8 || XMIMusic.CurrentlyPlayingThemeNo > 15)
                {//AutoMap: unless a walking theme is playing, the world's walking theme takes over
                    XMIMusic.PickLevelThemeMusic(0);
                    XMIMusic.RefreshMusic();
                }
            }
            else
            {
                XMIMusic.ChangeThemeMusic(XMIMusic.MapsAndLegends);//play maps and legends in UW1 only.
                XMIMusic.RefreshMusic();
                uimanager.DrawAutoMap(playerdat.dungeon_level - 1, 0);

            }

            return false;
        }
    }//end class
}//end namespace