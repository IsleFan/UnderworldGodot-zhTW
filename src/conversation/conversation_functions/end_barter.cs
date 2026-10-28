namespace Underworld
{
    public partial class ConversationVM : UWClass
    {
        /// <summary>
        /// Gives back what is still in the trade slots (end_barter): every item in the player's slots, selected or not,
        /// is put down near the player. The NPC's items never left its inventory here, so its slots are only cleared.
        /// </summary>
        public static void end_barter()
        {
            for (int i = 0; i < uimanager.NoOfTradeSlots; i++)
            {
                var objindex = uimanager.GetPlayerTradeSlot(i, false);
                if (objindex != -1)
                {//near_mob_put_at(the player, it, 5): a free spot within 5 of the player (put_at)
                    var tradeObject = UWTileMap.current_tilemap.LevelObjects[objindex];
                    var player = playerdat.playerObject;
                    var placed = motion.PutAt(tradeObject, (player.tileX << 3) + player.xpos, (player.tileY << 3) + player.ypos, player.zpos, 5, false);
                    if (placed != null)
                    {
                        objectInstance.RedrawFull(placed);
                    }
                    uimanager.SetPlayerTradeSlot(i, -1, false);
                }
                uimanager.SetNPCTradeSlot(i, -1, false);
            }
        }
    }//end class
}//end namespace
