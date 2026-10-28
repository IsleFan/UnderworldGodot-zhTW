using System.Diagnostics;

namespace Underworld
{
    //Common data and code for item trading.
    public partial class ConversationVM : UWClass
    {

        /// <summary>
        /// The trade evaluation that has to be met to allow the NPC to trade. 
        /// Calculated using random offset and CritterDat[0xEh] bits 0-3
        /// RNG is seeded by critter item_id
        /// </summary>
        public static int TradeThreshold = 0;

        /// <summary>
        /// How much patience the NPC has for bad or insulting trade offers. 
        /// </summary>
        public static int TradePatience = 0;


        /// <summary>
        /// Measure of how good this NPC is at appraising the value of a trade item.
        /// </summary>
        public static int NPCAppraisalAccuracy = 0;

        /// <summary>
        /// Score of the previous trade evaluation. Used to determine if a better or worse offer has been made on repeated offers
        /// </summary>
        public static int PreviousEvaluation = 0;

        /// <summary>
		/// Pointer to Array in the stack that contains the items the npc likes
		/// </summary>
		public static int Likes = 0;

        /// <summary>
        /// Pointer to the array in the stack that contains the items the npc dislikes
        /// </summary>
        public static int Dislikes = 0;

        public static int TradeResult = 0;

        /// <summary>
        /// This is a bonus applied by a bablhack in uw2. 
        /// However it is bugged in vanilla since it makes the npc items more valuable and only applies on do_judgement and does not actually help the player make better trades!
        /// </summary>
        public static int BablTradeBonus = 0;

        /// <summary>
        /// Get the total value of the players trade area (selected items)
        /// </summary>
        /// <param name="ApplyLikeDislike"></param>
        /// <param name="appraise_accuracy"></param>
        /// <returns></returns>
        static int GetNPCTradeValue(bool ApplyLikeDislike = false, int appraise_accuracy = 0, bool applyAccuracy = false)
        {
            var result = 0;
            for (int i = 0; i < uimanager.NoOfTradeSlots; i++)
            {
                //if slot select get monetary value, possibly adjusted for likes dislike and player appraisal score
                var index = uimanager.GetNPCTradeSlot(i);
                if (index != -1)
                {
                    var obj = UWTileMap.current_tilemap.LevelObjects[index];
                    result += GetTradeValueOfItem(
                        ApplyLikeDislike: ApplyLikeDislike,
                        appraise_accuracy: appraise_accuracy,
                        applyAccuracy: applyAccuracy,
                        obj: obj);
                }
            }
            return (short)result;//a 16-bit sum, which wraps as DOS's does (total_offering)
        }


        /// <summary>
        /// Get the total value of the PC trade area (selected items)
        /// </summary>
        /// <param name="ApplyLikeDislike"></param>
        /// <param name="appraise_accuracy"></param>
        /// <param name="applyAccuracy"></param>
        /// <returns></returns>
        static int GetPCTradeValue(bool ApplyLikeDislike = false, int appraise_accuracy = 0, bool applyAccuracy = false)
        {
            var result = 0;
            for (int i = 0; i < uimanager.NoOfTradeSlots; i++)
            {
                //if slot select get monetary value, possibly adjusted for likes dislike and player appraisal score
                var index = uimanager.GetPlayerTradeSlot(i);
                if (index != -1)
                {
                    var obj = UWTileMap.current_tilemap.LevelObjects[index];
                    result += GetTradeValueOfItem(
                        ApplyLikeDislike: ApplyLikeDislike,
                        appraise_accuracy: appraise_accuracy,
                        applyAccuracy: applyAccuracy,
                        obj: obj);
                }
            }
            return (short)result;//a 16-bit sum, which wraps as DOS's does (total_offering)
        }

        // private static int GetTrueItemValue(bool ApplyLikeDislike, int appraise_accuracy, bool applyAccuracy, int index)
        // {
        //     if (index != -1)
        //     {
        //         var obj = UWTileMap.current_tilemap.LevelObjects[index];
        //         if (obj != null)
        //         {
        //             //obj.ObjectQuantity;
        //             var itemvalue = commonObjDat.monetaryvalue(obj.item_id);
        //             if (ApplyLikeDislike)
        //             {//todo
        //                 itemvalue = itemvalue * 1;
        //             }
        //             itemvalue = itemvalue * obj.ObjectQuantity;
        //             var quality = obj.quality;
        //             //if coin quality = 0x40
        //             if (
        //                 (obj.item_id == 160)
        //                 ||
        //                 ((_RES != GAME_UW2) && (obj.item_id == 161))
        //                 )
        //             {
        //                 quality = 0x3F;
        //             }

        //             itemvalue = (itemvalue * quality) >> 6;
        //             if (applyAccuracy)
        //             {
        //                 //Rng.r = new System.Random(obj.item_id);//RNG is always re-seeded with item_id before getting offset here
        //                 Rng.Seed = (uint)obj.item_id;
        //                 itemvalue = Rng.RandomOffset(itemvalue, -appraise_accuracy, +appraise_accuracy);
        //             }
        //             return itemvalue;
        //         }
        //     }

        //     return 0;
        // }


        /// <summary>
        /// Gets the value of the item trading, account for likes/dislikes and appraisal accuracy
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="ApplyLikeDislike"></param>
        /// <param name="appraise_accuracy"></param>
        /// <param name="applyAccuracy"></param>
        /// <returns></returns>
        static int GetTradeValueOfItem(uwObject obj, bool ApplyLikeDislike = false, int appraise_accuracy = 0, bool applyAccuracy = false)
        {//assess_value: the class value (1.5 times if liked, nothing if disliked) times the quantity times quality / 64,
         //at least 1 while the quality is above 0, blurred by up to the accuracy %, the same each time for the object
            int itemvalue = commonObjDat.monetaryvalue(obj.item_id);
            if (ApplyLikeDislike)
            {
                var disposition = DoesNPCLike(obj);
                if (disposition == -1)
                {
                    return 0;
                }
                if (disposition == 1)
                {
                    itemvalue = (short)(itemvalue * 3) >> 1;
                }
            }
            var quantity = ((obj.is_quant == 1) && ((obj.link & 0x200) == 0)) ? obj.link : 1;
            itemvalue = (short)(itemvalue * quantity);//16-bit, as Turbo C's
            var quality = obj.quality;
            if ((_RES == GAME_UW2) && (obj.item_id == 160))
            {//UW2 counts coins as quality 63
                quality = 63;
            }
            if (itemvalue > 0)
            {
                if (quality > 0)
                {
                    itemvalue = _RES == GAME_UW2 ? (int)(((long)itemvalue * quality) >> 6) : (short)(itemvalue * quality) >> 6;
                    if (itemvalue == 0)
                    {
                        itemvalue = 1;
                    }
                }
                else
                {
                    itemvalue = 0;
                }
            }
            if (applyAccuracy)
            {
                Rng.Seed = (uint)obj.index;
                itemvalue = Rng.RandomOffset(itemvalue, -appraise_accuracy, appraise_accuracy);
                Rng.InitRng();
            }
            return itemvalue;
        }

        /// <summary>
        /// 1 when the NPC likes the object, -1 when it dislikes it or it is worthless, else 0 (does_npc_like). An exact
        /// item on the likes list wins at once; a class entry (item >> 4) + 1000 only counts if no exact match comes, and a
        /// dislike, exact or by class, overrides a liked class.
        /// </summary>
        static int DoesNPCLike(uwObject obj)
        {
            if (commonObjDat.monetaryvalue(obj.item_id) == 0)
            {
                return -1;
            }
            var cls = (obj.item_id >> 4) + 1000;
            var liked = 0;
            if (Likes != 0)
            {
                for (var index = Likes; at(index) > -1; index++)
                {
                    var entry = at(index);
                    if (entry < 1000)
                    {
                        if (entry == obj.item_id)
                        {
                            return 1;
                        }
                    }
                    else if (entry == cls)
                    {
                        liked = 1;
                    }
                }
            }
            if (Dislikes != 0)
            {
                for (var index = Dislikes; at(index) > -1; index++)
                {
                    var entry = at(index);
                    if (entry < 1000)
                    {
                        if (entry == obj.item_id)
                        {
                            return -1;
                        }
                    }
                    else if (entry == cls)
                    {
                        liked = -1;
                    }
                }
            }
            return liked;
        }

        /// <summary>
        /// Check if at least one item in the player trade is selected
        /// </summary>
        /// <returns></returns>
        static bool IsPlayerOfferingItems()
        {
            for (int i = 0; i < uimanager.NoOfTradeSlots; i++)
            {
                if (uimanager.GetPlayerTradeSlot(i) != -1)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Check if at least one item in the npc trade is selected
        /// </summary>
        /// <returns></returns>
        static bool IsNPCOfferingItems()
        {
            for (int i = 0; i < uimanager.NoOfTradeSlots; i++)
            {
                if (uimanager.GetNPCTradeSlot(i) != -1)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Creates arrays of the selected items and their item indices
        /// </summary>
        /// <param name="itemIds"></param>
        /// <param name="itemdIndices"></param>
        /// <returns>No of items</returns>
        static int GetPlayerSelectedTradeItems(out int[] itemIds, out int[] itemdIndices)
        {
            int count = 0;
            itemIds = new int[uimanager.NoOfTradeSlots];
            itemdIndices = new int[uimanager.NoOfTradeSlots];

            for (int i = 0; i < uimanager.NoOfTradeSlots; i++)
            {
                var item = uimanager.GetPlayerTradeSlot(i);
                if (item != -1)
                {
                    var obj = UWTileMap.current_tilemap.LevelObjects[item];
                    itemIds[count] = obj.item_id;
                    itemdIndices[count] = item;
                    count++;
                }
            }
            return count;
        }


        /// <summary>
        /// Gives the item at the specified index from the trade area to the npc.
        /// </summary>
        /// <param name="talker"></param>
        /// <param name="itemindex"></param>
        public static void GiveItemIndexToNPC(uwObject talker, int itemindex)
        {
            for (int i = 0; i < uimanager.NoOfTradeSlots; i++)
            {
                var x = uimanager.GetPlayerTradeSlot(i, false);
                if (x != -1)
                {
                    var obj = UWTileMap.current_tilemap.LevelObjects[x];
                    if (obj.index == itemindex)
                    {
                        NPCInventoryAdd(talker, obj);//player_barter_give
                        uimanager.SetPlayerTradeSlot(i, -1, false);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Gives the NPC an object (npc_inv_add): coins join a pile of coins it already has while the two come to less
        /// than 999, otherwise the object goes to the head of its inventory.
        /// </summary>
        static void NPCInventoryAdd(uwObject talker, uwObject obj)
        {
            if ((obj.item_id == 160) && (obj.is_quant == 1) && ((obj.link & 0x200) == 0))
            {
                var next = talker.link;
                while (next != 0)
                {
                    var other = UWTileMap.current_tilemap.LevelObjects[next];
                    if ((other.item_id == obj.item_id) && (other.is_quant == 1) && ((other.link & 0x200) == 0) && (obj.link + other.link < 0x3E7))
                    {
                        other.link = (short)(other.link + obj.link);
                        ObjectFreeLists.ReleaseFreeObject(obj);
                        return;
                    }
                    next = other.next;
                }
            }
            obj.next = talker.link;
            talker.link = obj.index;
        }

        /// <summary>
        /// Completes the trade between player and NPC and moves objects between their inventory
        /// </summary>
        /// <param name="talker"></param>
        private static void SwapTradedObjects(uwObject talker)
        {
            //transfer trade item
            //player(world) to Npc
            for (int i = 0; i < uimanager.NoOfTradeSlots; i++)
            {//probablyTradeObjects: the player's selected items the NPC does not dislike go to it; the others stay in the slots
                var objindex = uimanager.GetPlayerTradeSlot(i);
                if (objindex != -1)
                {
                    var tradedobject = UWTileMap.current_tilemap.LevelObjects[objindex];
                    if (DoesNPCLike(tradedobject) == -1)
                    {
                        continue;
                    }
                    NPCInventoryAdd(talker, tradedobject);
                    uimanager.SetPlayerTradeSlot(i, -1, false);
                }
            }

            //npc to player. use free backpack slots or else drop at player position
            //Some deliberate jank here. close opened containers
            if (uimanager.OpenedContainerIndex != -1)
            {//keep closing until we reach the top
                while (uimanager.OpenedContainerIndex != -1)
                {
                    container.Close(uimanager.OpenedContainerIndex, playerdat.InventoryObjects);
                }
            }

            for (int i = 0; i < uimanager.NoOfTradeSlots; i++)
            {//remove from the NPCs chain first
                var objindex = uimanager.GetNPCTradeSlot(i);
                if (objindex != -1)
                {
                    ObjectRemover_OLD.RemoveObjectFromLinkedList(talker.link, objindex, UWTileMap.current_tilemap.LevelObjects, talker.PTR + 6);
                }
            }

            for (int i = 0; i < uimanager.NoOfTradeSlots; i++)
            {
                var objindex = uimanager.GetNPCTradeSlot(i);
                if (objindex != -1)
                {
                    var tradedobject = UWTileMap.current_tilemap.LevelObjects[objindex];

                    uimanager.CurrentSlot = uimanager.FreeGeneralUseSlot;
                    if (uimanager.CurrentSlot != -1)
                    {
                        playerdat.ObjectInHand = tradedobject.index;
                        uimanager.PickupToEmptySlot(tradedobject.index);
                        uimanager.CurrentSlot = -1;
                    }
                    else
                    {
                        var desttile = UWTileMap.current_tilemap.Tiles[playerdat.playerObject.tileX, playerdat.playerObject.tileY];
                        UWTileMap.GetRandomXYZForTile(desttile, out int newxpos, out int newypos, out int newzpos);
                        //var dropcoordinate = uwObject.XYZToVector3(x: (newxpos << 5) + (playerdat.playerObject.tileX << 8), y: tile.floorHeight << 3, z: (newypos << 5) + (playerdat.playerObject.tileY << 8));//uwObject.GetCoordinate_OBSOLETE(playerdat.playerObject.tileX, playerdat.playerObject.tileY, newxpos, newypos, newzpos);
                        //move object to tile
                        tradedobject.tileX = desttile.tileX; tradedobject.tileY = desttile.tileY;
                        tradedobject.xpos = (short)newxpos; tradedobject.ypos = (short)newypos; tradedobject.zpos = (short)newzpos;
                        //link to tile
                        tradedobject.next = desttile.indexObjectList;
                        desttile.indexObjectList = tradedobject.index;
                        objectInstance.RedrawFull(tradedobject);
                        //drop at players location
                        // pickup.Drop_old(
                        //     index: objindex,
                        //     objList: UWTileMap.current_tilemap.LevelObjects,
                        //     dropPosition: dropcoordinate,
                        //     tileX: playerdat.playerObject.tileX,
                        //     tileY: playerdat.playerObject.tileY);
                    }
                    uimanager.SetNPCTradeSlot(i, -1, false);
                }
            }
            uimanager.UpdateInventoryDisplay();
        }

    } //end class
}//end namespace