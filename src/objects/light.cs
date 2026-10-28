namespace Underworld
{
    public class light: objectInstance
    {
        /// <summary>
        /// UseLight: only from the inventory, and not a used-up light (UW2 quality 1 or less, UW1 quality 0). An unlit
        /// torch, candle, lantern or light sphere (UW1 taper) outside the four light slots, or one of a stack, is moved to
        /// the first free light slot first ("Your hands are full." with none). Then it is lit or put out where it is.
        /// </summary>
        public static bool Use(uwObject obj, bool WorldObject)
        {
            if (WorldObject)
            {//Lights may only be used if equipped.
                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, _RES == GAME_UW2 ? 0x89 : 0x7B));
                return true;
            }
            if ((_RES == GAME_UW2) ? (obj.quality <= 1) : (obj.quality == 0))
            {//That light is already used up.
                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, _RES == GAME_UW2 ? 0x8A : 0x7C));
                return true;
            }
            var qty = ((obj.is_quant == 1) && ((obj.link & 0x200) == 0)) ? obj.link : 1;
            var inLightSlot = (uimanager.CurrentSlot >= 5) && (uimanager.CurrentSlot <= 8) && (qty == 1);
            if (!inLightSlot && (obj.item_id >= 0x90) && (obj.item_id <= 0x93))
            {
                var freeslot = -1;
                for (int i = 5; i <= 8; i++)
                {
                    if (playerdat.GetInventorySlotListHead(i) == 0)
                    {
                        freeslot = i;
                        break;
                    }
                }
                if (freeslot == -1)
                {//Your hands are full.
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, _RES == GAME_UW2 ? 0x105 : 0xF6));
                    return true;
                }
                //move one into the light slot by picking it up and dropping it there
                if (qty == 1)
                {
                    uimanager.PickupObjectFromSlot(obj);
                }
                else
                {
                    obj.link--;
                    var newObjIndex = ObjectCreator.SpawnObjectInHand(obj.item_id);
                    if (newObjIndex == 0)
                    {//no free object on the level
                        return true;
                    }
                    var cloneObj = UWTileMap.current_tilemap.LevelObjects[newObjIndex];
                    cloneObj.link = 1;
                    cloneObj.quality = obj.quality;
                    cloneObj.owner = obj.owner;
                    playerdat.ObjectInHand = cloneObj.index;
                }
                var newObj = UWTileMap.current_tilemap.LevelObjects[playerdat.ObjectInHand];
                if (ToggleLight(newObj) && (_RES == GAME_UW2))
                {
                    UWsoundeffects.PlaySoundEffectAtAvatar(UWsoundeffects.SoundEffectLight, 0x40, 0);
                }
                uimanager.CurrentSlot = freeslot;
                uimanager.PickupToEmptySlot(playerdat.ObjectInHand);
            }
            else
            {
                if (ToggleLight(obj) && (_RES == GAME_UW2))
                {
                    UWsoundeffects.PlaySoundEffectAtAvatar(UWsoundeffects.SoundEffectLight, 0x40, 0);
                }
            }

            uimanager.UpdateInventoryDisplay();
            playerdat.PlayerStatusUpdate();
            return true;
        }

        /// <summary>
        /// Return true if light is now on.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static bool ToggleLight(uwObject obj)
        {
            //turn light on or off.
            if (obj.classindex <= 3)
            {
                LightOn(obj); 
                return true;               
            }
            else
            {
                LightOff(obj);
                return false;
            }
        }

        public static void LightOff(uwObject obj)
        {
            if (obj.classindex >= 4)
            {
                obj.item_id -= 4;
            }
        }

        public static void LightOn(uwObject obj)
        {
            if (obj.classindex <= 3)
            {
                if ((_RES == GAME_UW2) ? (obj.quality > 1) : (obj.quality > 0))
                {
                    obj.item_id += 4;
                }
                else
                {
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1,GameStrings.str_that_light_is_already_used_up_));
                }                
            }
        }


        
    }//end class
}//end namespace