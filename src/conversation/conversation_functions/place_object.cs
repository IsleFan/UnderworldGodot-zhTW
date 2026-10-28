namespace Underworld
{
    public partial class ConversationVM : UWClass
    {
        /// <summary>
        /// place_object(object, x, y): takes the object out of the talker's inventory and puts it on tile (x, y),
        /// or at the player's feet when x is negative. Returns 0 when (x, y) is off the map or there is no room there.
        /// (The original also places an object that was not in the talker's inventory; here that returns 0,
        /// as such an object is still linked elsewhere.)
        /// </summary>
        public static void place_object(uwObject talker)
        {
            var index = at(at(stack + stackptr - 3));
            var tilex = at(at(stack + stackptr - 2));
            var tiley = at(at(stack + stackptr - 1));

            //remove from the npcs inventory
            var previous = 0; var next = talker.link;
            while (next != 0)
            {
                var objToPlace = UWTileMap.current_tilemap.LevelObjects[next];
                if (objToPlace.index == index)
                {
                    if (tilex >= 0 && (tilex < 1 || tilex >= 64 || tiley < 1 || tiley >= 64))
                    {//off the map. (The original has already unlinked it and leaves it nowhere; here it stays with the talker.)
                        result_register = 0;
                        return;
                    }
                    if (tilex >= 0)
                    {//can_place at the corner of the square on the floor, with 4 to spare round its radius
                        var floor = UWTileMap.current_tilemap.Tiles[tilex, tiley].floorHeight << 3;
                        if (!motion.CanPlace(objToPlace.item_id, objToPlace.index, tilex << 3, tiley << 3, floor, true, commonObjDat.radius(objToPlace.item_id) + 4))
                        {//no room. (Here too the original has already unlinked it; here it stays with the talker.)
                            result_register = 0;
                            return;
                        }
                    }
                    //unlink from npc inventory
                    if (previous == 0)
                    {//head of npc chain
                        talker.link = objToPlace.next;
                    }
                    else
                    {//inside the chain
                        var PrevObject = UWTileMap.current_tilemap.LevelObjects[previous];
                        PrevObject.next = objToPlace.next;
                    }
                    objToPlace.next = 0;
                    if (tilex < 0)
                    {//near the player: put_at the corner of the player's square (no fine offset), there first, within 6, never culled
                        var player = playerdat.playerObject;
                        var placed = motion.PutAt(objToPlace, player.tileX << 3, player.tileY << 3, player.zpos, 6, nocull: true, stayCentered: true);
                        if (placed != null)
                        {
                            objectInstance.RedrawFull(placed);
                        }
                        result_register = 1;
                    }
                    else
                    {//the object keeps its fine position and rests on the floor
                        var tile = UWTileMap.current_tilemap.Tiles[tilex, tiley];
                        moveobject(objToPlace, tilex, tiley, objToPlace.xpos, objToPlace.ypos, tile.floorHeight << 3);
                        //obj_deal settles it there (it may fall, sink or be destroyed)
                        var settled = motion.PlacedObjectCollision_seg030_2BB7_10BC(objToPlace, tilex, tiley, 1);
                        if (settled != null)
                        {
                            objectInstance.RedrawFull(settled);
                        }
                        result_register = 1;
                    }
                    return;
                }
                previous = objToPlace.index;
                next = objToPlace.next;
            }
            result_register = 0;
        }


        static void moveobject(uwObject objToPlace, int tilex, int tiley, int xpos, int ypos, int zpos)
        {
            var tile = UWTileMap.current_tilemap.Tiles[tilex, tiley];
            objToPlace.tileX = tilex; objToPlace.tileY = tiley;
            objToPlace.xpos = (short)xpos; objToPlace.ypos = (short)ypos; objToPlace.zpos = (short)zpos;

            //link at the end of the tile chain.
            objToPlace.next = 0;
            if (tile.indexObjectList == 0)
            {
                tile.indexObjectList = objToPlace.index;
            }
            else
            {
                var last = UWTileMap.current_tilemap.LevelObjects[tile.indexObjectList];
                while (last.next != 0)
                {
                    last = UWTileMap.current_tilemap.LevelObjects[last.next];
                }
                last.next = objToPlace.index;
            }

            if (objToPlace.instance != null)
            {
                objToPlace.instance.uwnode.QueueFree();
            }
            ObjectCreator.RenderObject(objToPlace, UWTileMap.current_tilemap);
        }
    }//end class
}//end namespace