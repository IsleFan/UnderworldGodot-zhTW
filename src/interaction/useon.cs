namespace Underworld
{
    /// <summary>
    /// Class for interactions involving the use verb when holding another object
    /// </summary>
    public class useon : UWClass
    {
        public static useon CurrentItemBeingUsed;
        public int index = 0;

        public bool WorldObject;

        public uwObject itemBeingUsed;

        public useon(uwObject obj, bool _worldobject)
        {
            index = obj.index;
            WorldObject = _worldobject;
            itemBeingUsed = obj;
            //change the mouse cursor
            uimanager.instance.mousecursor.SetCursorToObject(obj.item_id);
        }

        public string GeneralUseOnString
        {
            get
            {
                return $"Use {GameStrings.GetName(itemBeingUsed.item_id, article: false, plural: false)} on what?";//UseThing
            }
        }

        public static bool UseOn(uwObject ObjectUsed, useon srcObject, bool WorldObject)
        {

            var ObjInHand = srcObject.itemBeingUsed;
            if (ObjectUsed == null) { return false; }
            trap.ObjectThatStartedChain = ObjectUsed.index;
            bool result = false;
            // if (index <= targetobjList.GetUpperBound(0))
            // {
            //     var targetObj = targetobjList[index];
            switch (ObjInHand.majorclass)
            {
                case 2:
                    {
                        //result = UseOnMajorClass2(obj, objList, WorldObject);
                        break;
                    }
                case 3:
                    {
                        result = UseOnMajorClass3(ObjInHand, ObjectUsed, WorldObject);
                        break;
                    }
                case 4:
                    {
                        result = UseOnMajorClass4(ObjInHand, ObjectUsed, WorldObject);
                        break;
                    }
                case 5:
                    {
                        //result = UseOnMajorClass5(obj, objList, WorldObject);
                        break;
                    }
            }
            uwObject[] targetobjList;
            if (WorldObject)
            {
                targetobjList = UWTileMap.current_tilemap.LevelObjects;
            }
            else
            {
                targetobjList = playerdat.InventoryObjects;
            }
            //(the original's two-object uses fire no use triggers on the target; a pole on a switch uses the switch itself)

            //Clear the cursor icons
            CurrentItemBeingUsed = null;
            // playerdat.ObjectInHand = -1;
            uimanager.instance.mousecursor.SetCursorToCursor();

            return false;
        }

        public static bool UseOnMajorClass3(uwObject objInHand, uwObject targetObject, bool WorldObject)
        {
            switch (objInHand.minorclass)
            {
                case 0:
                    {
                        if ((objInHand.classindex >= 2) && (objInHand.classindex <= 6))
                        {//skulls and bones
                            return bones.UseOn(objInHand, targetObject, WorldObject);
                        }
                        break;
                    }
                case 1:
                    {//some misc items                    
                        switch (objInHand.classindex)
                        {
                            case 7://anvil
                                return anvil.UseOn(objInHand, targetObject, WorldObject);
                            case 8://pole
                                return pole.UseOn(objInHand, targetObject, WorldObject);
                        }
                        break;
                    }
                case 2:
                    {
                        if (_RES != GAME_UW2)
                        {
                            switch (objInHand.classindex)
                            {
                                case 7://key of infinity
                                    return key_of_infinity.UseOn(objInHand, targetObject, WorldObject);
                            }
                        }
                        break;
                    }
            }
            return false;
        }

        public static bool UseOnMajorClass4(uwObject objInHand, uwObject targetObject, bool WorldObject)
        {
            switch (objInHand.minorclass)
            {
                case 0: //UseKey: the lockpick (0x101) picks, every item below the lock (0x10F) is a key matched by its owner
                    {
                        switch (objInHand.classindex)
                        {
                            case 1:
                                return lockpick.UseOn(objInHand, targetObject, WorldObject);
                            case < 0xF:
                                return doorkey.UseOn(objInHand, targetObject, WorldObject);
                        }
                        break;
                    }
                case 1:
                    {
                        if (_RES == GAME_UW2)
                        {
                            switch (objInHand.classindex)
                            {
                                case >= 8 and <= 0xF:
                                    return smallblackrockgem.UseOn(objInHand, targetObject, WorldObject);
                            }
                        }
                        else
                        {
                            switch(objInHand.classindex)
                            {
                                case 2: //orbrock
                                    return OrbRock.UseOn(OrbObject: objInHand, targetObject: targetObject, WorldObject: WorldObject, playerUsing: true);
                            }
                        }
                        break;
                    }
                case 2:// misc usables
                    {
                        switch (objInHand.classindex)
                        {
                            case 0x7:   //spike in uw1
                                {
                                    if (_RES != GAME_UW2)
                                    {
                                        return spike.UseOn(objInHand, targetObject, WorldObject);
                                    }
                                    break;
                                }
                            case 0x8://rock hammer
                                {
                                    return rockhammer.UseOn(objInHand, targetObject, WorldObject);
                                }
                            case 0xD://oil flask
                                {
                                    return oilflask.UseOn(objInHand, targetObject, WorldObject);
                                }
                        }
                        break;
                    }
            }
            return false;
        }

    } //end class
} //end namespace