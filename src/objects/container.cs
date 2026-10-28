using System.Collections.Generic;
using System.Diagnostics;

namespace Underworld
{
    public class container : objectInstance
    {
        public static bool Use(uwObject obj, bool WorldObject, bool toPlayer = true)
        {
            //UseCont: a locked container says so, in the world or in the inventory
            if (a_lock.CheckLock(playerdat.playerObject, obj, 0, WorldObject) == 0)
            {
                var msg = $"The {GameStrings.GetSimpleObjectNameUW(obj.item_id)} is locked";
                uimanager.AddToMessageScroll(msg);
                return true;
            }
            if (WorldObject)
            {
                DumpTheBag(obj, toPlayer);
                return true;
            }
            else
            {
                //container used in inventory. Browse into it.
                if (obj.classindex <= 0xB)
                {
                    //set to opened version by setting bit 0 to 1.
                    obj.item_id |= 0x1;
                }
                uimanager.OpenedContainerIndex = obj.index;
                uimanager.SetOpenedContainer(obj.index, uwObject.GetObjectSprite(obj));
                uimanager.BackPackStart = 0;
                DisplayContainerObjects(obj);
                return true;
            }
        }


        /// <summary>
        /// DumpTheBag: a container used in the world is emptied onto the floor, its contents given the container's owner
        /// (when it can be owned). UW2 tells the owners (player_did_bad) and says "You empty the X." or "The X is empty.";
        /// UW1 only says "The X is empty.". The messages are for the player using it.
        /// </summary>
        public static void DumpTheBag(uwObject bag, bool toPlayer)
        {
            var owner = commonObjDat.canhaveowner(bag.item_id) ? bag.owner : 0;
            var dumped = SpillWorldContainer(bag, owner);
            var name = GameStrings.GetSimpleObjectNameUW(bag.item_id);
            if (_RES == GAME_UW2)
            {
                a_do_trap_trespass.HackTrapTrespass(owner);
                if (toPlayer)
                {
                    uimanager.AddToMessageScroll(dumped ? $"You empty the {name}." : $"The {name} is empty.");
                }
            }
            else if (!dumped && toPlayer)
            {
                uimanager.AddToMessageScroll($"The {name} is empty.");
            }
        }

        /// <summary>
        /// drop_link_chain: empties a container (or a dead critter) onto the floor near where it is, each object put with
        /// put_at (range 6). UW2 first removes its locks, gives each object the owner (when nonzero and it can be owned),
        /// puts out lit lights, and sets off a trigger among the contents as it falls out (a use trigger). UW1 gives the
        /// owner to the container instead and does none of the rest. Returns true if anything was inside.
        /// </summary>
        public static bool SpillWorldContainer(uwObject containerObj, int owner = 0)
        {
            var objList = UWTileMap.current_tilemap.LevelObjects;
            if (_RES == GAME_UW2)
            {
                a_lock.RemoveAllLocks(containerObj, true);
            }
            if ((containerObj.is_quant != 0) || (containerObj.link == 0))
            {
                return false;
            }
            int nextobj = containerObj.link;
            containerObj.link = 0;//clear the container link
            int tx = containerObj.IsStatic ? containerObj.tileX : containerObj.npc_xhome;
            int ty = containerObj.IsStatic ? containerObj.tileY : containerObj.npc_yhome;
            var x = (tx << 3) + containerObj.xpos;
            var y = (ty << 3) + containerObj.ypos;
            var z = containerObj.zpos;
            while (nextobj != 0)
            {
                var objToSpill = objList[nextobj];
                nextobj = objToSpill.next;
                objToSpill.next = 0;
                if (_RES == GAME_UW2)
                {
                    if ((owner != 0) && commonObjDat.canhaveowner(objToSpill.item_id))
                    {
                        objToSpill.owner = (short)owner;
                    }
                    if ((objToSpill.item_id >= 0x94) && (objToSpill.item_id <= 0x97))
                    {//lit lights go out
                        objToSpill.item_id -= 4;
                    }
                }
                else if ((owner != 0) && commonObjDat.canhaveowner(containerObj.item_id))
                {//UW1: the container gets the owner, not the item
                    containerObj.owner = (short)owner;
                }
                Debug.Print($"Spilling {objToSpill.index} {objToSpill.a_name}");
                var placed = motion.PutAt(objToSpill, x, y, z, 6, false);
                if ((_RES == GAME_UW2) && (placed != null) && (placed.majorclass == 6) && (placed.minorclass >= 2))
                {//a trigger that falls out goes off, with the rest of the contents linked back while it runs
                    containerObj.link = (short)nextobj;
                    trigger.RunTrigger(
                        character: 1,
                        ObjectUsed: null,
                        TriggerObject: placed,
                        triggerType: (int)triggerObjectDat.triggertypes.USE,
                        objList: objList);
                    nextobj = containerObj.link;
                    containerObj.link = 0;
                }
            }
            return true;
        }




        /// <summary>
        /// Displays the range (start to start+count) of container objects on the paper doll backback slots
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="start"></param>
        /// <param name="count"></param>
        public static void DisplayContainerObjects(uwObject obj, int start = 0, int count = 8)
        {
            int occupiedslots = 0;
            var objects = GetObjects(
                ContainerIndex: obj.index,
                objList: playerdat.InventoryObjects,
                OccupiedSlots: out occupiedslots,
                start: start,
                count: count
                );

            if (objects == null)
            {
                //empty container
                for (int o = 0; o < 8; o++)
                {
                    uimanager.SetBackPackIndex(o, null);
                }
            }
            else
            {
                for (int o = 0; o <= objects.GetUpperBound(0); o++)
                {
                    if (objects[o] != -1)
                    {
                        //render object at this slot
                        var objFound = playerdat.InventoryObjects[objects[o]];
                        uimanager.SetBackPackIndex(o, objFound);
                    }
                    else
                    {
                        uimanager.SetBackPackIndex(o, null);
                    }
                }
            }

            uimanager.EnableDisable(uimanager.instance.ArrowUp, occupiedslots == 8);
            uimanager.EnableDisable(uimanager.instance.ArrowDown, start != 0);

            uimanager.UpdateInventoryDisplay();
        }

        /// <summary>
        /// Closes the container on the paperdoll
        /// </summary>
        /// <param name="obj"></param>
        public static int Close(int index, uwObject[] objList)
        {
            var obj = objList[index];
            if (obj == null) { return -1; }
            if (obj.classindex <= 0xB)
            {//return to closed version of the container.
                obj.item_id &= 0x1fe;
            }
            //Check the paperdoll
            for (int p = 0; p < 19; p++)
            {
                if (playerdat.GetInventorySlotListHead(p) == obj.index)
                { //object is on the paperdoll. I can close and return to the top level
                    uimanager.RefreshSlot(p);
                    uimanager.OpenedContainerIndex = -1;//clear slot graphics
                    uimanager.SetOpenedContainer(obj.index, -1);
                    //Draw the paperdoll inventory.
                    for (int i = 0; i < 8; i++)
                    {
                        uimanager.SetBackPackArt(i, uwObject.GetObjectSprite(playerdat.BackPackObject(i)), uwObject.GetObjectQuantity(playerdat.BackPackObject(i)));
                        uimanager.SetBackPackIndex(i, playerdat.BackPackObject(i));
                    }
                    uimanager.EnableDisable(uimanager.instance.ArrowUp, false);
                    uimanager.EnableDisable(uimanager.instance.ArrowDown, false);
                    uimanager.EnableDisable(uimanager.instance.OpenedContainer, false);
                    return -1;
                }
            }
            foreach (var objToCheck in playerdat.InventoryObjects)
            {//if this far down then I need to find the container that the closing container sits in
                if (objToCheck != null)
                {
                    var result = objectsearch.GetContainingObject(
                        ListHead: objToCheck.index,
                        ToFind: uimanager.OpenedContainerIndex,
                        objList: playerdat.InventoryObjects);
                    if (result != -1)
                    {//container found. Browse into it by using it
                        Use(objList[result], false);
                        return result;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// Returns an array of object indices at the specified offset and length
        /// </summary>
        /// <param name="ContainerIndex"></param>
        /// <param name="objList"></param>
        /// <param name="OccupiedSlots">No of objects still in this list after start+count</param>
        /// <param name="start"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static int[] GetObjects(int ContainerIndex, uwObject[] objList, out int OccupiedSlots, int start = 0, int count = 8)
        {
            var Objects = ListObjects(ContainerIndex, objList);
            OccupiedSlots = 0;
            if (Objects == null)
            {
                //return an empty list
                var emptylist = new int[count];
                for (int o = start; o < start + count; o++)
                {
                    emptylist[o] = -1;
                }
                return emptylist;
            }
            var output = new int[count];
            int i = 0;
            for (int o = start; o < start + count; o++)
            {
                if (o <= Objects.GetUpperBound(0))
                {
                    if (i < count)
                    {
                        output[i++] = Objects[o];
                        OccupiedSlots++;
                    }
                    else
                    {
                        output[i++] = -1;
                    }
                }
                else
                {
                    output[i++] = -1;
                }
            }
            return output;
        }

        /// <summary>
        /// Returns a list of all objects in the container main level
        /// </summary>
        /// <param name="Container"></param>
        /// <returns></returns>
        public static int[] ListObjects(int ContainerIndex, uwObject[] objList)
        {
            var Container = objList[ContainerIndex];
            if (Container == null)
            {
                return null;
            }
            if (Container.link != 0)
            {
                var OutputList = new List<int>();
                var nextObj = objList[Container.link];
                while (nextObj != null)
                {
                    if (nextObj.item_id != 288)
                    {//Do not display spells. Handles the box of create food.
                        OutputList.Add(nextObj.index);
                    }                    
                    if (nextObj.next != 0)
                    {
                        nextObj = objList[nextObj.next];
                    }
                    else
                    {
                        nextObj = null;
                    }
                }
                return OutputList.ToArray();
            }
            return null;
        }



        /// <summary>
        /// The inventory container that holds the object at index, or -1 at the top level.
        /// </summary>
        static int EnclosingContainer(int index)
        {
            for (int i = 0; i < 19; i++)
            {
                if (playerdat.GetInventorySlotListHead(i) == index)
                {
                    return -1;
                }
            }
            foreach (var objToCheck in playerdat.InventoryObjects)
            {
                if (objToCheck != null)
                {
                    var result = objectsearch.GetContainingObject(ListHead: objToCheck.index, ToFind: index, objList: playerdat.InventoryObjects);
                    if (result != -1)
                    {
                        return result;
                    }
                }
            }
            return -1;
        }

        public static bool TestContainerCanHold(uwObject containerobject, uwObject objectToAdd, bool printReason = true)
        {//assume containerobject = in player object list, objecttoadd in level objects list
            MessageDisplay.MessageDisplayMode printmode = MessageDisplay.MessageDisplayMode.NormalMode;
            if (uimanager.InConversation)
            {
                printmode = MessageDisplay.MessageDisplayMode.TemporaryMessage;
            }
            //ItemFitsSlot: first the weight. Only the contents count, plus the new object; capacity 0 means no limit.
            //Every open bag around it must also have room (the open bags from the opened one outwards).
            var capacity = containerObjectDat.capacity(containerobject.item_id);
            var contentsMass = GetTotalMass(containerobject, playerdat.InventoryObjects, true) - commonObjDat.mass(containerobject.item_id);
            var itemToAddMass = GetTotalMass(objectToAdd, UWTileMap.current_tilemap.LevelObjects, true);
            var fits = (capacity == 0) || (contentsMass + itemToAddMass <= capacity);
            var openBag = uimanager.OpenedContainerIndex;
            while (fits && (openBag > 0))
            {
                var bag = playerdat.InventoryObjects[openBag];
                var bagCapacity = containerObjectDat.capacity(bag.item_id);
                var bagContents = GetTotalMass(bag, playerdat.InventoryObjects, true) - commonObjDat.mass(bag.item_id);
                fits = (bagCapacity == 0) || (bagContents + itemToAddMass <= bagCapacity);
                openBag = EnclosingContainer(openBag);
            }
            if (!fits)
            {
                if (printReason)
                {
                    uimanager.AddToMessageScroll(
                        stringToAdd: ZhText.Enabled
                            ? ZhText.Compose(ZhText.ObjectName(containerobject.item_id), "已經滿了。")
                            : $"The {GameStrings.GetSimpleObjectNameUW(containerobject.item_id)} is too full.",
                        mode: printmode);
                }
                return false;
            }
            //then the kind of object the container takes
            var mask = containerObjectDat.objectmask(containerobject.item_id);
            if (mask < 0)
            {
                return true;
            }
            var id = objectToAdd.item_id;
            var major = objectToAdd.majorclass; var minor = objectToAdd.minorclass; var sub = objectToAdd.classindex;
            bool ContainerCanHold;
            if (mask < 512)
            {//only that exact item
                ContainerCanHold = id == mask;
            }
            else
            {
                switch (mask)
                {
                    case 512://rune bag: runestones (and the 0xE8-0xEF stones)
                        if (!((major == 3) && ((minor == 3) || ((minor == 2) && (sub > 7)))))
                        {
                            if (printReason)
                            {
                                uimanager.AddToMessageScroll(
                                    stringToAdd: GameStrings.GetDisplayString(1, GameStrings.str_you_can_only_put_runes_in_the_rune_bag_),
                                    mode: printmode);
                            }
                            return false;
                        }
                        return true;
                    case 513://quivers: sling stones, bolts and arrows (UW2 also wands)
                        ContainerCanHold = ((major == 0) && (minor == 1) && (sub < 3))
                            || ((_RES == GAME_UW2) && (major == 2) && (minor == 1) && (sub >= 8));
                        break;
                    case 514://scroll and map cases: UW1 0x138-0x13F, UW2 0x134-0x137, 0x139 and 0x13A
                        ContainerCanHold = (major == 4) && (minor == 3) &&
                            ((_RES == GAME_UW2) ? (((sub >= 4) && (sub < 8)) || (sub == 9) || (sub == 10)) : (sub >= 8));
                        break;
                    case 515://bowl: food (UW2 not drinks) and a few reagents
                        if (_RES == GAME_UW2)
                        {
                            ContainerCanHold = ((major == 2) && (minor == 3) && (id != 0xBB) && (id != 0xBC) && (id != 0xBD))
                                || (id == 0xCE) || (id == 0xCF) || (id == 0x114) || (id == 0xD2) || (id == 0x13E) || (id == 0xC5);
                        }
                        else
                        {
                            ContainerCanHold = ((major == 2) && (minor == 3))
                                || (id == 0xCE) || (id == 0xCF) || (id == 0x92) || (id == 0x125) || (id == 0x11B) || (id == 0xD9);
                        }
                        break;
                    case 516://key ring (UW2): keys (not 0x100), or another key ring
                        ContainerCanHold = ((major == 4) && (minor == 0) && (sub != 0))
                            || ((objectToAdd.OneF0Class == 8) && (containerObjectDat.objectmask(id) == 516));
                        break;
                    default:
                        ContainerCanHold = false;
                        break;
                }
            }
            if (!ContainerCanHold && printReason)
            {
                uimanager.AddToMessageScroll(
                    stringToAdd: GameStrings.GetDisplayString(1, GameStrings.str_that_item_does_not_fit_),
                    mode: printmode);
            }
            return ContainerCanHold;
        }


        /// <summary>
        /// Gets the total mass of the object chain.
        /// </summary>
        /// <param name="objectToWeight"></param>
        /// <param name="objList"></param>
        /// <param name="IgnoreNext">tells the loop to not look at the next object.</param>
        /// <returns></returns>
        public static int GetTotalMass(uwObject objectToWeight, uwObject[] objList, bool IgnoreNext)
        {
            var selfweight = objectToWeight.ObjectQuantity * commonObjDat.mass(objectToWeight.item_id);

            if (!IgnoreNext)
            {
                var nextIndex = objectToWeight.next;
                while (nextIndex != 0)
                {
                    var nextObject = objList[nextIndex];
                    selfweight += GetTotalMass(nextObject, objList, true);
                    nextIndex = nextObject.next;
                }
            }

            if (objectToWeight.is_quant == 0)
            {
                if (objectToWeight.link > 0)
                {
                    var linked = objList[objectToWeight.link];
                    selfweight += GetTotalMass(linked, objList, false);
                }
            }
            return selfweight;
        }

    }//end class
}//end namespace