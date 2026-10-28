namespace Underworld
{
    public class key_of_infinity : objectInstance
    {

        public static bool Use(uwObject obj, bool WorldObject)
        {
            if (!WorldObject)
            {
                //flag we are using the key of infinity
                useon.CurrentItemBeingUsed = new useon(obj, WorldObject);
                //print use message
                uimanager.AddToMessageScroll(useon.CurrentItemBeingUsed.GeneralUseOnString);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// UW1 key of infinity (seg040_352B_AFF): used on a texture map object (0x16E) showing the textured door, it is used
        /// up and the object's traps go off (how 7); anything else "It seems to have no effect."
        /// </summary>
        public static bool UseOn(uwObject keyobject, uwObject itemTargeted, bool WorldObject)
        {
            useon.CurrentItemBeingUsed = null;
            if ((itemTargeted.item_id == 0x16E)
                && ((TerrainDatLoader.Terrain[UWTileMap.current_tilemap.texture_map[itemTargeted.owner]] & 0xFF) == 0xB))
            {
                ObjectCreator.Consume(keyobject, true);
                trigger.TriggerObjectLink(character:1, 
                    ObjectUsed: itemTargeted, 
                    triggerType: (int)triggerObjectDat.triggertypes.OPEN_UW1,
                    triggerX: itemTargeted.tileX,
                    triggerY: itemTargeted.tileY, 
                    objList: UWTileMap.current_tilemap.LevelObjects);
            }
            else
            {
                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x84));
            }
            return true;
        }
    }//end class
}//end namespace