using System.Diagnostics;

namespace Underworld
{
    public partial class ConversationVM : UWClass
    {
        /// <summary>
        /// give_ptr_npc(arg2 object, arg1 qty): gives the NPC an object from the player's selected trade items, or else
        /// from the player's inventory (an index find_inv gave). UW1 gives all of a traded object; UW2 needs a quantity
        /// above 0 and splits off that many from a larger stack.
        /// </summary>
        public static void give_ptr_npc(uwObject talker)
        {
            var qty = at(at(stack + stackptr-1));
            var ObjectIndex = at(at(stack + stackptr-2));
            Debug.Print($"{qty} of {ObjectIndex}");
            result_register = 0;
            if ((_RES == GAME_UW2) && (qty <= 0))
            {
                return;
            }
            var itemcount = GetPlayerSelectedTradeItems(out int[] itemIds, out int[] itemIndices);
            for (int i = 0; i < itemcount; i++)
            {
                if (ObjectIndex != itemIndices[i])
                {
                    continue;
                }
                var obj = UWTileMap.current_tilemap.LevelObjects[ObjectIndex];
                if ((_RES != GAME_UW2) || (obj.is_quant == 0) || ((obj.link & 0x200) != 0) || (obj.link <= qty))
                {//all of it
                    GiveItemIndexToNPC(talker, ObjectIndex);
                }
                else
                {//UW2 splits off qty of the stack for the NPC
                    var copy = ObjectCreator.spawnObjectInTile(
                        itemid: obj.item_id,
                        tileX: 99, tileY: 99,
                        xpos: obj.xpos, ypos: obj.ypos, zpos: obj.zpos,
                        WhichList: ObjectFreeLists.ObjectListType.StaticList,
                        RenderImmediately: false);
                    if (copy == null)
                    {
                        return;
                    }
                    for (int b = 0; b < 8; b++)
                    {
                        copy.DataBuffer[copy.PTR + b] = obj.DataBuffer[obj.PTR + b];
                    }
                    copy.link = (short)qty;
                    obj.link = (short)(obj.link - qty);
                    NPCInventoryAdd(talker, copy);
                }
                result_register = 1;
                return;
            }
            //Otherwise qty of it comes out of the player's inventory (invRemoveObject), all of it unless it is an ordinary stack
            GiveFromInventory(talker, ObjectIndex, qty);
        }

        static void GiveFromInventory(uwObject talker, int index, int qty)
        {
            if ((index <= 0) || (index > playerdat.InventoryObjects.GetUpperBound(0)) || (playerdat.InventoryObjects[index] == null))
            {//not carried
                return;
            }
            var obj = playerdat.InventoryObjects[index];
            int worldIndex;
            if ((qty > 0) && (obj.is_quant == 1) && ((obj.link & 0x200) == 0) && (obj.link > 1) && (qty < obj.link))
            {//part of a stack: qty goes, the rest stays carried
                worldIndex = playerdat.AddInventoryObjectToWorld(objIndex: index, updateUI: false, RemoveNext: false, DestroyInventoryObject: false);
                if (worldIndex == 0)
                {//no free objects on the level: nothing is given
                    return;
                }
                UWTileMap.current_tilemap.LevelObjects[worldIndex].link = (short)qty;
                obj.link = (short)(obj.link - qty);
                playerdat.WeightCarried -= (short)(commonObjDat.mass(obj.item_id) * qty);
                uimanager.RefreshWeightDisplay();
                uimanager.UpdateInventoryDisplay();
            }
            else
            {
                worldIndex = playerdat.AddInventoryObjectToWorld(objIndex: index, updateUI: true, RemoveNext: false);
                if (worldIndex == 0)
                {
                    return;
                }
            }
            var given = UWTileMap.current_tilemap.LevelObjects[worldIndex];
            given.next = 0;
            NPCInventoryAdd(talker, given);
            result_register = 1;
        }
    }//end class
}//end namespace
