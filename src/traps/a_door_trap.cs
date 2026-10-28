using System.Diagnostics;

namespace Underworld
{
    public class a_door_trap : trap
    {
        /// <summary>
        /// Opens, closes or toggles the door on the square and replaces its lock by a copy of the trap's linked lock
        /// (TRAP_DOOR): UW1 always and first, UW2 afterwards and only when the owner is set. Returns true when
        /// the chain ends there: always in UW1, in UW2 when the lock was replaced.
        /// </summary>
        public static bool Activate(uwObject trapObj, int triggerX, int triggerY, uwObject[] objList)
        {
            if (trapObj == null)
            {
                Debug.Print("Null door trap");
                return false;
            }
            var tile = UWTileMap.current_tilemap.Tiles[triggerX, triggerY];
            var doorObj = objectsearch.FindMatchInObjectChain(
                ListHeadIndex: tile.indexObjectList,
                majorclass: 5,
                minorclass: 0,
                classindex: -1,
                objList: objList);
            if ((_RES != GAME_UW2) && (doorObj != null))
            {
                ReplaceLock(trapObj, doorObj, objList);
            }
            if (doorObj == null)
            {//check for a moving door
                doorObj = objectsearch.FindMatchInObjectChain(
                    ListHeadIndex: tile.indexObjectList,
                    majorclass: 7,
                    minorclass: 0,
                    classindex: 0xF,
                    objList: objList);
                if (doorObj != null)
                {
                    //found a moving door
                    if (doorObj.owner >= 8)
                    {
                        if ((trapObj.quality == 1) || (trapObj.quality == 3))
                        {
                            Debug.Print("Moving Door Trap Open (not implemented) This will glitch out doorways.");
                            door.OpenDoor(doorObj);
                        }
                    }
                    else
                    {
                        if ((trapObj.quality == 2) || (trapObj.quality == 3))
                        {
                            //try close
                            Debug.Print("Moving Door Trap Close (not implemented) This will glitch out doorways.");
                            door.CloseDoor(doorObj);
                        }
                    }
                }
            }
            else
            {
                //found a static door do the actions on it
                switch (trapObj.quality)
                {
                    case 1://Try Open
                        Debug.Print("Door Trap Open");
                        door.OpenDoor(doorObj);
                        break;
                    case 2://Try Close
                        Debug.Print("Door Trap Close");
                        door.CloseDoor(doorObj);
                        break;
                    case 3: //Try toggle
                        Debug.Print("Door Trap Toggle");
                        door.ToggleDoor(doorObj);
                        break;
                }
            }

            if ((_RES == GAME_UW2) && (doorObj != null) && (trapObj.owner != 0))
            {
                ReplaceLock(trapObj, doorObj, objList);
                return true;
            }
            return _RES != GAME_UW2;
            // if (doorObj == null)
            // {
            //     Debug.Print("No Door found for door trap");
            // }
            // if (doorObj.instance==null)
            // {
            //      Debug.Print("No Door instance found for door trap");
            // }
            // if(trapObj!=null)
            // {

            //}

        }
        /// <summary>
        /// Takes the lock off the door and gives it a copy of the trap's linked lock, if it has one.
        /// </summary>
        static void ReplaceLock(uwObject trapObj, uwObject doorObj, uwObject[] objList)
        {
            var lockObject = objectsearch.FindMatchInObjectChain(
                ListHeadIndex: doorObj.link,
                majorclass: 4, minorclass: 0, classindex: 0xF,
                objList: objList,
                SkipLinks: true);
            if (lockObject != null)
            {//Remove the current lock
                if (ObjectRemover_OLD.RemoveObjectFromLinkedList(doorObj.link, lockObject.index, objList, doorObj.PTR + 6))
                {
                    ObjectFreeLists.ReleaseFreeObject(lockObject);
                }
            }

            if ((trapObj.is_quant == 0) && (trapObj.link != 0))
            {//apply the template lock
                lockObject = objList[trapObj.link];
                var freeslot = ObjectFreeLists.GetAvailableObjectSlot(ObjectFreeLists.ObjectListType.StaticList);
                if (freeslot != 0)
                {
                    uwObject destObject = objList[freeslot];
                    //copy data
                    for (int i = 0; i < 8; i++)
                    {
                        destObject.DataBuffer[destObject.PTR + i] = lockObject.DataBuffer[lockObject.PTR + i];
                    }
                    //insert into object list as linked to the door
                    destObject.next = doorObj.link;
                    doorObj.link = destObject.index;
                }
            }
        }
    }//end class
}//end namespace