namespace Underworld
{
    public partial class ConversationVM : UWClass
    {
        /// <summary>
        /// add_to_npc_inv(arg1 object): links the object onto the end of the talker's inventory (Obj_AddEnd).
        /// </summary>
        public static void add_to_npc_inv(uwObject talker)
        {
            var index = GetConvoStackValueAtPtr(stack + stackptr - 1);
            if (index <= 0)
            {
                return;
            }
            var obj = UWTileMap.current_tilemap.LevelObjects[index];
            obj.next = 0;
            if (talker.link == 0)
            {
                talker.link = (short)index;
                return;
            }
            var last = UWTileMap.current_tilemap.LevelObjects[talker.link];
            while (last.next != 0)
            {
                last = UWTileMap.current_tilemap.LevelObjects[last.next];
            }
            last.next = (short)index;
        }
    }//end class
}//end namespace
