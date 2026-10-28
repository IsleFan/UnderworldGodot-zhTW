using System.Diagnostics;

namespace Underworld
{
    public partial class ConversationVM:UWClass
    {
        public static void take_from_npc(uwObject talker)
        {
            var arg1 = GetConvoStackValueAtPtr(stack + stackptr-1);
            Debug.Print($"take_from_npc({arg1})");


            result_register = 0;//none of that type, or the hand is full (npc_barter_give)
            if (playerdat.ObjectInHand != -1)
            {
                return;
            }

            if (talker.LootSpawnedFlag==0)
            {
                npc.spawnloot(talker);
            }

            var nextItem = talker.link;
            var previous = talker;
            while (nextItem!=0)
            {
                var obj = UWTileMap.current_tilemap.LevelObjects[nextItem];
                bool match = false;
                if (arg1>999)
                {
                    if (obj.OneF0Class == arg1 - 1000)
                    {
                        match = true;
                    }
                }
                else
                {
                    if (arg1 == obj.item_id)
                    {
                        match = true;
                    }
                }
                
                if (match && IsInNPCTradeSlot(obj.index))
                {//the original takes the slots' items out of the inventory, so they are not found here
                    match = false;
                }
                if(match)
                {   
                    //Unlink from npc
                    if (talker.link == obj.index)
                    {
                        talker.link = obj.next;
                        obj.next = 0;
                    }
                    else
                    {
                        previous.next = obj.next;
                        obj.next = 0;
                    }

                    result_register = HandOverFromNPC(talker, obj);
                    return;
                }
                previous = obj;
                nextItem = obj.next;
            }
        }

        static bool IsInNPCTradeSlot(int index)
        {
            for (int i = 0; i < uimanager.NoOfTradeSlots; i++)
            {
                if (uimanager.GetNPCTradeSlot(i, false) == index)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// An object the NPC has let go of goes to the player's hand if it can be carried, else into a
        /// free player trade slot, else at the NPC's feet (npc_barter_give). Returns 1, or 2 when dropped.
        /// </summary>
        static int HandOverFromNPC(uwObject talker, uwObject obj)
        {
            if (playerdat.CanCarryWeight(obj))
            {
                playerdat.ObjectInHand = obj.index;
                uimanager.instance.mousecursor.SetCursorToObject(obj.item_id);
                return 1;
            }
            for (int i = 0; i < uimanager.NoOfTradeSlots; i++)
            {
                if (uimanager.GetPlayerTradeSlot(i, false) == -1)
                {
                    uimanager.SetPlayerTradeSlot(slotno: i, item_index: obj.index, selected: false);
                    return 1;
                }
            }
            //no free trade slot: near_mob_put_at(the talker, it, 5), 2 when it was put down, else 3
            var placed = motion.PutAt(obj, (talker.tileX << 3) + talker.xpos, (talker.tileY << 3) + talker.ypos, talker.zpos, 5, false);
            if (placed != null)
            {
                objectInstance.RedrawFull(placed);
                return 2;
            }
            return 3;
        }
    }   //end class
}//end namespace