using System.Diagnostics;

namespace Underworld
{
    public partial class ConversationVM : UWClass
    {
        /// <summary>
        /// The NPC hands over the object with this index from its inventory (npc_barter_give_id): nothing
        /// (0) if the player's hand is full or the NPC does not carry it, else as take_from_npc.
        /// </summary>
        public static void take_id_from_npc(uwObject talker)
        {
            var id = GetConvoStackValueAtPtr(stack + stackptr-1);
            Debug.Print($"Take ID {id}");
            result_register = 0;
            if (playerdat.ObjectInHand != -1)
            {
                return;
            }
            var next = talker.link;
            while (next != 0 && next != id)
            {
                next = UWTileMap.current_tilemap.LevelObjects[next].next;
            }
            if (next == 0 || IsInNPCTradeSlot(id))
            {//(the original takes the slots' items out of the inventory)
                return;
            }
            var ObjectToGive = UWTileMap.current_tilemap.LevelObjects[id];
            ObjectRemover_OLD.RemoveObjectFromLinkedList(talker.link, id, UWTileMap.current_tilemap.LevelObjects, talker.PTR+6);
            result_register = HandOverFromNPC(talker, ObjectToGive);
        }

    }//end class
}//end namespace
