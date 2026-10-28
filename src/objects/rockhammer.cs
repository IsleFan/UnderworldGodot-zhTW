namespace Underworld
{
    public class rockhammer : objectInstance
    {

        public static bool Use(uwObject obj, bool WorldObject)
        {
            if (!WorldObject)
            {
                //flag we are using the pole
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

        public static bool UseOn(uwObject hammerobject, uwObject itemTargeted, bool WorldObject)
        {
            if (!WorldObject){return false;}
            if ((itemTargeted.item_id>=0x153) && (itemTargeted.item_id<=0x156))
            {//UseRockHammerOn: the boulder breaks into 1 to 4 pieces (more from bigger ones), each one or two sizes smaller,
             //below the small boulder a stack of 3 to 8 sling stones, put near where it lay
                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1,GameStrings.str_the_rock_breaks_into_smaller_pieces_));
                var id = itemTargeted.item_id;
                var x = (itemTargeted.tileX << 3) + itemTargeted.xpos;
                var y = (itemTargeted.tileY << 3) + itemTargeted.ypos;
                var z = itemTargeted.zpos;
                if (_RES == GAME_UW2)
                {//UW2 removes the boulder first
                    ObjectRemover_OLD.DeleteObjectFromTile_DEPRECIATED(itemTargeted.tileX, itemTargeted.tileY, itemTargeted.index);
                }
                for (var count = Rng.r.NextScaled(2) + (0x155 - id) + 1; count > 0; count--)
                {
                    var newItemID = id + Rng.r.NextScaled(2) + 1;
                    if (newItemID > 0x156)
                    {
                        newItemID = 0x10;
                    }
                    var slot = ObjectCreator.PrepareNewObject(newItemID);
                    if (slot == 0)
                    {
                        break;
                    }
                    var rock = UWTileMap.current_tilemap.LevelObjects[slot];
                    if (_RES != GAME_UW2)
                    {//UW1 copies the boulder's record, then changes the item
                        for (int i = 0; i < 8; i++)
                        {
                            rock.DataBuffer[rock.PTR + i] = itemTargeted.DataBuffer[itemTargeted.PTR + i];
                        }
                        rock.next = 0;
                        rock.item_id = newItemID;
                    }
                    if (newItemID == 0x10)
                    {//when a sling stone. 
                        rock.is_quant = 1;
                        rock.link = (short)(3 + Rng.r.Next(6));
                    }
                    motion.PutAt(rock, x, y, z, 6, false);
                }
                if (_RES != GAME_UW2)
                {
                    ObjectRemover_OLD.DeleteObjectFromTile_DEPRECIATED(itemTargeted.tileX, itemTargeted.tileY, itemTargeted.index);
                }
                return true;
            }
            else
            {
                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1,GameStrings.str_it_seems_to_have_no_effect_));
                return true;
            }
        }
    }//end class
}//end namespace