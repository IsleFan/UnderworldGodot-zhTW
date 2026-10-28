namespace Underworld
{
    /// <summary>
    /// UW1 hack 0x32 (level 7): the first active critter with conversation 0xD8 comes to talk to the player
    /// (gronk_whoami, gronkify_talkto_player). One standing still (goal 7) is sent home first.
    /// </summary>
    public class a_hack_trap_talktoplayer : hack_trap
    {
        public static void Activate()
        {
            for (int i = 0; i < UWTileMap.current_tilemap.NoOfActiveMobiles; i++)
            {
                var critter = UWTileMap.current_tilemap.LevelObjects[UWTileMap.current_tilemap.GetActiveMobileAtIndex(i)];
                if (critter.npc_whoami == 0xD8)
                {
                    if (critter.npc_goal == (byte)npc.npc_goals.npc_goal_stand_still_7)
                    {
                        critter.npc_goal = (byte)npc.npc_goals.npc_goal_goto_1;
                    }
                    talk.Talk(critter, true);
                    return;
                }
            }
        }
    }//end class
}//end namespace
