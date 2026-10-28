namespace Underworld
{
    /// <summary>
    /// Skulls, bones and piles of bones (0xC2 to 0xC6): used from the inventory they ask for a target (UseUtil, UseBonesOn).
    /// </summary>
    public class bones : objectInstance
    {
        public static bool Use(uwObject obj, bool WorldObject)
        {
            if (!WorldObject)
            {
                useon.CurrentItemBeingUsed = new useon(obj, WorldObject);
                uimanager.AddToMessageScroll(useon.CurrentItemBeingUsed.GeneralUseOnString);
            }
            return true;
        }

        /// <summary>
        /// Bones used on something. On a gravestone UW2 buries them (0x94). In UW1 Garamon's bones (owner 0x3E) on his
        /// grave (special link 0x21) bury him: the talisman and Garamon bits are set, the grave's link becomes 0x222, his
        /// conversation (whoami 0x1B) runs and the trigger at tile (0x36, 0x34) goes off; his bones on another grave are kept
        /// (0x103), and other bones on a grave are buried (0x86). Anything else has no effect (UW1 0x84, UW2 0x92).
        /// </summary>
        public static bool UseOn(uwObject bonesObject, uwObject targetObject, bool WorldObject)
        {
            useon.CurrentItemBeingUsed = null;
            var used = false;
            if (targetObject.item_id == 0x165)
            {//a gravestone
                if (_RES == GAME_UW2)
                {
                    used = true;
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x94));
                }
                else if (bonesObject.owner == 0x3E)
                {
                    if ((targetObject.is_quant == 1) && ((targetObject.link & 0x200) != 0) && ((targetObject.link & 0x1FF) == 0x21))
                    {
                        playerdat.CanTalismansBeDestroyed = true;
                        playerdat.IsGaramonBuried = true;
                        targetObject.link = 0x222;
                        used = true;
                        ObjectCreator.Consume(bonesObject, true);//bones are used from the inventory
                        var garamon = talk.SpawnTemporaryTalker(whoami: 0x1B, itemid: 0x7E, tileX: playerdat.playerObject.tileX, tileY: playerdat.playerObject.tileY, attitude: 3, goal: 7);
                        if (garamon != null)
                        {
                            talk.Talk(garamon, true);
                        }
                        var tileTrigger = UWTileMap.current_tilemap.Tiles[0x36, 0x34].indexObjectList;
                        if (tileTrigger != 0)
                        {
                            trigger.RunTrigger(
                                character: 1,
                                ObjectUsed: null,
                                TriggerObject: UWTileMap.current_tilemap.LevelObjects[tileTrigger],
                                triggerType: (int)triggerObjectDat.triggertypes.MOVE,
                                objList: UWTileMap.current_tilemap.LevelObjects);
                        }
                        return true;
                    }
                    else
                    {
                        uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x103));
                        return true;
                    }
                }
                else
                {
                    used = true;
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x86));
                }
            }
            if (!used)
            {
                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, _RES == GAME_UW2 ? 0x92 : 0x84));
            }
            else
            {
                ObjectCreator.Consume(bonesObject, true);//bones are used from the inventory
            }
            return true;
        }
    }//end class
}//end namespace
