using SerdesNet;

namespace Underworld
{
    public class a_lock : objectInstance
    {

        public static bool GetIsLocked(uwObject parentObject)
        {
            //if (isOpen) { return false; }
            var lockobj = LockObject(parentObject);
            if (lockobj == null)
            {
                return false;
            }
            else
            {
                return (lockobj.flags & 0x01) == 1;
            }
        }

        public static void SetIsLocked(uwObject parentObject, bool value, int character = 0)
        {
            var lockobj = LockObject(parentObject);
            if (lockobj == null) { return; }
            if (value)
            {//lock
                lockobj.flags |= 1;  //set flag bit 0
            }
            else
            {//unlock
                lockobj.flags &= 0xE;  //clear flag bit 0
                //run unlock trap (the traps are in the door's contents, not the lock's)
                trigger.TriggerObjectLink(
                    character: character,
                    ObjectUsed: parentObject,
                    triggerType: (int)triggerObjectDat.UNLOCK_TRIGGER_TYPE,
                    triggerX: parentObject.tileX,
                    triggerY: parentObject.tileY,
                    objList: UWTileMap.current_tilemap.LevelObjects);
            }
        }

        /// <summary>
        /// The lock rules (checkLock). key is 0 to only ask, a key's lock number (its owner) to use a key, or minus the
        /// lockpicking skill (UW2 plus one) to pick it. Returns 0 locked or the attempt failed, 1 no lock, 2 the key
        /// locked it, 3 unlocked, 4 not locked (or open), 5 a lockpick fumble (UW2 only).
        /// Unlocking sets off the door's unlock traps, then deletes the lock unless its flag bit 1 (id bit 10) says it
        /// can be locked again, in which case only its locked bit is cleared.
        /// </summary>
        public static int CheckLock(uwObject who, uwObject door, int key, bool WorldObject = true)
        {
            var objList = WorldObject ? UWTileMap.current_tilemap.LevelObjects : playerdat.InventoryObjects;
            if ((door.is_quant != 0) || (door.link <= 0))
            {
                return 1;
            }
            var lockobj = objectsearch.FindMatchInObjectChain(
                ListHeadIndex: door.link,
                majorclass: 4, minorclass: 0, classindex: 0xF,
                objList: objList,
                SkipLinks: true);
            if (lockobj == null)
            {
                return 1;
            }
            var locknumber = lockobj.link & 0x1FF;
            if ((lockobj.flags & 1) == 0)
            {//not locked
                if (key > 0)
                {
                    if (((door.OneF0Class == 0x14) && (door.classindex >= 8))
                        || ((door.OneF0Class == 0x8) && (door.classindex < 0xC) && ((door.item_id & 1) != 0)))
                    {//an open door or an open container
                        return 4;
                    }
                    if (locknumber == key)
                    {
                        lockobj.flags |= 1;
                        return 2;
                    }
                    return 0;
                }
                return 4;
            }
            else if (key < 0)
            {//picking
                if (_RES == GAME_UW2)
                {
                    var result = (int)playerdat.SkillCheck(-key, lockobj.zpos * 3);
                    if (((lockobj.zpos == 0xE) && (-key < 0x20)) || (lockobj.zpos == 0xF) || (result == 0))
                    {
                        return result == -1 ? 5 : 0;
                    }
                    if (result == -1)
                    {
                        return 5;
                    }
                }
                else
                {
                    if (((lockobj.zpos == 0xE) && (-key < 0x1F)) || (lockobj.zpos == 0xF) || ((int)playerdat.SkillCheck(-key, lockobj.zpos * 3) <= 0))
                    {
                        return 0;
                    }
                }
            }
            else if (key > 0)
            {
                if ((locknumber == 0) || (locknumber != key))
                {
                    return 0;
                }
            }
            else
            {
                return 0;
            }
            //unlocked: the traps in the door go off
            trigger.TriggerObjectLink(
                character: who == null ? 1 : who.index,
                ObjectUsed: door,
                triggerType: (int)triggerObjectDat.UNLOCK_TRIGGER_TYPE,
                triggerX: door.tileX,
                triggerY: door.tileY,
                objList: objList);
            if ((lockobj.flags & 2) == 0)
            {//the lock is gone
                ObjectRemover_OLD.RemoveObjectFromLinkedList(door.link, lockobj.index, objList, door.PTR + 6);
                ObjectFreeLists.ReleaseFreeObject(lockobj);
            }
            else
            {
                lockobj.flags &= 0xE;
            }
            return 3;
        }

        /// <summary>
        /// Gets the lock attached to the parent object(typicall a door)
        /// </summary>
        /// <param name="parentObject"></param>
        /// <returns></returns>
        public static uwObject LockObject(uwObject parentObject)
        {
            return objectsearch.FindMatchInObjectChain(
                ListHeadIndex: parentObject.link,
                majorclass: 4,
                minorclass: 0,
                classindex: 0xF,
                objList: UWTileMap.current_tilemap.LevelObjects,
                SkipLinks: true);
        }


        /// <summary>
        /// What index key will open the lock attacked to this object.
        /// </summary>
        public static int KeyIndex(uwObject parentObject)
        {
            var lockobj = LockObject(parentObject);
            if (lockobj == null)
            {
                return -1;
            }
            return lockobj.link & 0x1FF;
        }


        /// <summary>
        /// Removes all locks from a container or door. Called when a door is broken open or when a locked container is spilled.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="RemoveAll"></param>
        /// <returns></returns>
        public static bool RemoveAllLocks(uwObject obj, bool RemoveAll)
        {//remove_lock: removes the first lock in obj's contents, or every one with RemoveAll; true only when RemoveAll found one
            bool found = false;
            if ((obj.is_quant != 0) || (obj.link == 0))
            {
                return false;
            }
            while (obj.link != 0)
            {
                var lockObj = objectsearch.FindMatchInObjectChain(
                    ListHeadIndex: obj.link,
                    majorclass: 4, minorclass: 0, classindex: 0xF,
                    objList: UWTileMap.current_tilemap.LevelObjects,
                    SkipLinks: true);
                if (lockObj == null)
                {
                    break;
                }
                if (ObjectRemover_OLD.RemoveObjectFromLinkedList(listhead: obj.link, toRemove: lockObj.index, objlist: UWTileMap.current_tilemap.LevelObjects, OffsetToListHeadConnection: obj.PTR + 6))
                {
                    ObjectFreeLists.ReleaseFreeObject(lockObj);
                }
                else
                {
                    break;
                }
                if (!RemoveAll)
                {
                    break;
                }
                found = true;
            }
            return found;
        }

    }//end class
}//end namespace