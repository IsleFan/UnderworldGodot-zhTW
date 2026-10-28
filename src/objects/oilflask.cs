using System;

namespace Underworld
{
    public class oilflask : objectInstance
    {
        public static bool Use(uwObject obj, bool WorldObject)
        {
            if (!WorldObject)
            {
                //flag we are using the oil flask         
                useon.CurrentItemBeingUsed = new useon(obj, WorldObject);
                //print use message
                uimanager.AddToMessageScroll(useon.CurrentItemBeingUsed.GeneralUseOnString);
            }
            return true;
        }

        /// <summary>
        /// UseOilOn: only on something in the inventory. Wood becomes a torch (UW2: one of a stack, with quality 0x28;
        /// UW1: the whole object as it is); an unlit lantern or torch that is not full gets 0x20 more quality (UW2 takes one
        /// from a stack); either way the flask is used up. A lit one gets a warning, anything else "You cannot use oil on that."
        /// </summary>
        public static bool UseOn(uwObject OilFlaskObject, uwObject targetObject, bool WorldObject = false)
        {
            useon.CurrentItemBeingUsed = null;
            if (WorldObject)
            {
                return true;
            }
            int stringno;
            if ((targetObject.item_id == 0x90) || (targetObject.item_id == 0x94))
            {//target is a lantern
                stringno = 0;
            }
            else
            {//target is not a lantern
                stringno = 4;
            }
            var isStack = (_RES == GAME_UW2) && (targetObject.is_quant == 1) && ((targetObject.link & 0x200) == 0) && (targetObject.link > 1);

            switch (targetObject.item_id)
            {
                case 0xCC://use on pieces of wood
                case 0xCD:
                    {
                        uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, GameStrings.str_dousing_a_cloth_with_oil_and_applying_it_to_the_wood_you_make_a_torch_));
                        ObjectCreator.Consume(OilFlaskObject, true);
                        if (isStack)
                        {//one piece of wood comes off the stack into the hand as the torch
                            targetObject.link--;
                            playerdat.WeightCarried -= commonObjDat.mass(targetObject.item_id);
                            ObjectCreator.SpawnObjectInHand(0x91);
                            if (playerdat.ObjectInHand > 0)
                            {
                                UWTileMap.current_tilemap.LevelObjects[playerdat.ObjectInHand].quality = 0x28;
                            }
                        }
                        else
                        {
                            //turn the target into a torch
                            playerdat.WeightCarried -= commonObjDat.mass(targetObject.item_id);
                            targetObject.item_id = 0x91;
                            playerdat.WeightCarried += commonObjDat.mass(targetObject.item_id);
                            if (_RES == GAME_UW2)
                            {
                                targetObject.quality = 0x28;
                            }
                        }
                        uimanager.RefreshWeightDisplay();
                        uimanager.UpdateInventoryDisplay();
                        return true;
                    }
                case 0x90:
                case 0x91://lantern or torch
                    {
                        if (targetObject.quality == 0x3F)
                        {
                            ApplyOilToUnlit(targetObject, stringno);//already full
                            return true;
                        }
                        if (isStack)
                        {
                            targetObject.link--;
                            playerdat.WeightCarried -= commonObjDat.mass(targetObject.item_id); //remove the weight of one instance.
                            if (ObjectCreator.SpawnObjectInHand(targetObject.item_id) == 0)
                            {//no free object on the level
                                return true;
                            }
                            var obj = UWTileMap.current_tilemap.LevelObjects[playerdat.ObjectInHand];
                            obj.quality = targetObject.quality;
                            ApplyOilToUnlit(obj, stringno);
                        }
                        else
                        {
                            ApplyOilToUnlit(targetObject, stringno);
                        }
                        ObjectCreator.Consume(OilFlaskObject, true);
                        uimanager.UpdateInventoryDisplay();
                        return true;
                    }
                case 0x94:
                case 0x95:
                    {
                        uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, stringno + (_RES == GAME_UW2 ? 193 : 178)));//oil on flame warning
                        return true;
                    }
            }
            uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, _RES == GAME_UW2 ? 0xC0 : 0xB1));//You cannot use oil on that.
            return true;
        }

        /// <summary>
        /// Uses oil to increase the quality of the lantern or torch.
        /// </summary>
        /// <param name="targetObject"></param>
        /// <param name="stringno"></param>
        private static void ApplyOilToUnlit(uwObject targetObject, int stringno)
        {
            if (targetObject.quality == 63)
            {
                if (_RES == GAME_UW2)
                {
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, stringno + 195));//already full message
                }
                else
                {
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, stringno + 180));//already full message
                }
            }
            else
            {
                targetObject.quality = (short)Math.Min(targetObject.quality + 32, 63);
                if (_RES == GAME_UW2)
                {
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, stringno + 194));//refresh light message
                }
                else
                {
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, stringno + 179));//refresh message
                }
            }
        }
    }//end class
}//end namespace