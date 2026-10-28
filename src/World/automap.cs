namespace Underworld
{
    public class automap : Loader
    {

        public static int currentautomap;
        public static int currentworld = 0;

        /// <summary>
        /// Array of all cached automaps
        /// </summary>
        public static automap[] automaps;


        //The raw data for this automap.
        public byte[] buffer;

        public automaptileinfo[,] tiles = new automaptileinfo[64, 64];


        /// <summary>
        /// Initialises an automap for the specified level no and loads the automap data from the lev.ark file
        /// </summary>
        /// <param name="LevelNo"></param>
        public automap(int LevelNo)
        {
            //load buffer. then init tiles with their offsets
            int blockno;
            if (_RES == GAME_UW2)
            {
                blockno = 160 + LevelNo;
            }
            else
            {
                blockno = LevelNo + 27;
            }
            DataLoader.LoadUWBlock(LevArkLoader.lev_ark_file_data, blockno, 64 * 64, out UWBlock block);
            if (block.Data == null)
            { //init a blank map
                buffer = new byte[64 * 64];
            }
            else
            {
                buffer = block.Data;
            }

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    tiles[x, y] = new automaptileinfo((y * 64) + x, ref buffer);
                }
            }
        }

        public static void MarkTileVisited(int level, int tileX, int tileY, int tiletype, int displaytype = automaptileinfo.DisplayTypeClear)
        {
            var tileToChange = automaps[level].tiles[tileX, tileY];
            // if (
            //     (tiletype != UWTileMap.TILE_SOLID) 
            //     && 
            //     ((automaps[level].tiles[tileX, tileY].tileType == UWTileMap.TILE_SOLID) || (automaps[level].tiles[tileX, tileY].tileType == 11))
            //     )
            // {
            //     if (tiletype != 11)
            //     {
            //         playerdat.NoOfTilesDiscovered++;//for exp gain on discovering new tiles. Skipping over solid tiles and undiscovered.
            //     }                
            // }    

            if ((tileToChange.tileType == UWTileMap.TILE_SOLID) || (tileToChange.tileType == 11))
            {
                //tile was undiscovered.
                if ((tiletype != UWTileMap.TILE_SOLID) && (tiletype!=11))
                {
                    //and not changing it to an undiscovered or solid type.
                    playerdat.NoOfTilesDiscovered++; //TODO: this may need to be updated to allow for marking of solid tiles. how are these tracked as been discovered? need to do some file research...
                }
            }


            tileToChange.tileType = (short)tiletype;
            tileToChange.DisplayType = (short)displaytype;
        }


        /// <summary>
        /// Checks if automapping is allowed in this map
        /// </summary>
        /// <param name="dungeon"></param>
        /// <returns></returns>
        public static bool CanMap(int dungeon)
        {
            if (_RES == GAME_UW2)
            {
                if (worlds.GetWorldNo(dungeon) == 8)
                {
                    return false;
                }
            }
            else
            {
                return dungeon != 9;
            }
            return true;
        }

        /// <summary>
        /// Marks a range of tiles around the center X,Y as visited.
        /// </summary>
        /// <param name="cX"></param>
        /// <param name="cY"></param>
        /// <param name="range"></param>
        /// <returns></returns>
        /// <summary>
        /// Records every square within range of cX, cY, by distance and not by sight, inside the map's border
        /// squares (automap_area with clip_circle, the Map Area spell).
        /// </summary>
        public static void MarkCircleOfTilesVisited(int cX, int cY, int range, int dungeon_level)
        {
            var buffer = automaps[dungeon_level - 1].buffer;
            for (var y = cY - range; y <= cY + range; y++)
            {
                for (var x = cX - range; x <= cX + range; x++)
                {
                    var dx = x - cX; var dy = y - cY;
                    if (dx * dx + dy * dy > range * range)
                    {
                        continue;
                    }
                    //automap_area clips as it goes: a square left of the border is moved onto column 1, and the rest
                    //of a row past the right border, above the top or below the bottom is given up
                    if (x < 1)
                    {
                        x = 1;
                    }
                    if ((x >= 0x3F) || (y < 1) || (y >= 0x3F))
                    {
                        break;
                    }
                    //only the tile type and the floor's terrain class: the doors, bridges and stairs on it are not recorded
                    buffer[y * 64 + x] = automaptileinfo.TileCode(UWTileMap.current_tilemap.Tiles[x, y]);
                }
            }
        }

        /// <summary>
        /// Marks the squares x..x+w, y..y+h of the current level's map as known but not seen: types 0-5
        /// become 0xA-0xF and slopes stay as they are (make_terrain_unseen, hack trap 45).
        /// </summary>
        public static void MakeTerrainUnseen(int x, int y, int w, int h)
        {
            var buffer = automaps[playerdat.dungeon_level - 1].buffer;
            for (int i = x; i <= x + w; i++)
            {
                for (int j = y; j <= y + h; j++)
                {
                    var index = j * 64 + i;//a column past the edge runs on into the next row, as in DOS
                    if (index < 0 || index >= buffer.Length)
                    {
                        continue;
                    }
                    var t = buffer[index] & 0xF;
                    if (t <= 5)
                    {
                        t += 0xA;
                    }
                    buffer[index] = (byte)((buffer[index] & 0xF0) | t);
                }
            }
        }

        public static int MarkRangeOfTilesVisited(int cX, int cY, int range, int dungeon_level)
        {

            var FillArea = FloodFillTile.FloodFillSubArea(cX, cY, range + 1);
            for (var aX = 0; aX <= FillArea.GetUpperBound(0); aX++)
            {
                for (var aY = 0; aY <= FillArea.GetUpperBound(1); aY++)
                {
                    bool literaledgecase =false;
                    if ((aY==0) || (aX == 0) || (aX== FillArea.GetUpperBound(0)) || (aY== FillArea.GetUpperBound(1)))
                    {
                        literaledgecase = true;
                    }
                    var x = FillArea[aX, aY].ActualX;
                    var y = FillArea[aX, aY].ActualY;
                    if ((FillArea[aX, aY].Tested) && (FillArea[aX, aY].Accessible) && (!literaledgecase))
                    {
                        //mark visited fileArea[aX,aY].ActualX/Y based on existing rules
                        MarkTileVisited(
                            level: dungeon_level - 1,
                            tileX: x, tileY: y,
                            tiletype: UWTileMap.current_tilemap.Tiles[x, y].tileType,
                            displaytype: automaptileinfo.TileCode(UWTileMap.current_tilemap.Tiles[x, y]) >> 4);
                    }
                    else
                    {
                        if (UWTileMap.ValidTile(x, y))
                        {//mark the open tiles outside of vision range as undiscovered open tile if not already visited
                            var tile = UWTileMap.current_tilemap.Tiles[x, y];
                            if (automaps[dungeon_level - 1].tiles[x, y].visited == false)
                            {
                                if (UWTileMap.IsOpen(UWTileMap.current_tilemap.Tiles[x, y].tileType))
                                {
                                    var displaytype = automaptileinfo.TileCode(tile) >> 4;
                                    //automap.automaps[dungeon_level - 1].tiles[northaxis, aY].DisplayType;
                                    //mark as undiscovered, open tile. 
                                    MarkTileVisited(
                                        level: dungeon_level - 1,
                                        tileX: x, tileY: y,
                                        tiletype: 11,
                                        displaytype: displaytype);
                                }
                            }
                        }
                    }
                }
            }
            return range;
        }

        // for (int aX = cX - range; aX <= cX + range; aX++)
        // {
        //     for (int aY = cY - range; aY <= cY + range; aY++)
        //     {
        //         if (UWTileMap.ValidTile(aX, aY))
        //         {//TODO figure out how to do line of sight checking of solid walls.
        //             automap.MarkTileVisited(
        //                     level: dungeon_level - 1,
        //                     tileX: aX, tileY: aY,
        //                     tiletype: UWTileMap.current_tilemap.Tiles[aX, aY].tileType,
        //                     displaytype: automaptileinfo.GetDisplayType(UWTileMap.current_tilemap.Tiles[aX, aY]));
        //         }
        //     }
        // }
        // range++;
        // //make outside range as unvisited open tiles if not already visited
        // //along the north
        // var northaxis = cX + range;
        // var southaxis = cX - range;
        // var westaxis = cY - range;
        // var eastaxis = cY + range;
        // for (int aY = cY - range; aY <= cY + range; aY++)
        // {
        //     if (UWTileMap.ValidTile(northaxis, aY))
        //     {
        //         var tile = UWTileMap.current_tilemap.Tiles[northaxis, aY];
        //         if (automap.automaps[dungeon_level - 1].tiles[northaxis, aY].visited == false)
        //         {
        //             if (UWTileMap.IsOpen(UWTileMap.current_tilemap.Tiles[northaxis, aY].tileType))
        //             {
        //                 var displaytype = automaptileinfo.GetDisplayType(tile);
        //                 //automap.automaps[dungeon_level - 1].tiles[northaxis, aY].DisplayType;
        //                 //mark as undiscovered, open tile. (there probably needs to be other rules for diagonals)
        //                 automap.MarkTileVisited(
        //                     level: dungeon_level - 1,
        //                     tileX: northaxis, tileY: aY,
        //                     tiletype: 11,
        //                     displaytype: displaytype);
        //             }
        //         }
        //     }
        //     if (UWTileMap.ValidTile(southaxis, aY))
        //     {
        //         var tile = UWTileMap.current_tilemap.Tiles[southaxis, aY];
        //         if (automap.automaps[dungeon_level - 1].tiles[southaxis, aY].visited == false)
        //         {
        //             if (UWTileMap.IsOpen(UWTileMap.current_tilemap.Tiles[southaxis, aY].tileType))
        //             {
        //                 var displaytype = automaptileinfo.GetDisplayType(tile);
        //                 automap.MarkTileVisited(
        //                     level: dungeon_level - 1,
        //                     tileX: southaxis, tileY: aY,
        //                     tiletype: 11,
        //                     displaytype: displaytype);
        //             }
        //         }
        //     }
        // }

        // for (int aX = cX - range; aX <= cX + range; aX++)
        // {
        //     if (UWTileMap.ValidTile(aX, westaxis))
        //     {
        //         var tile = UWTileMap.current_tilemap.Tiles[aX, westaxis];
        //         if (automap.automaps[dungeon_level - 1].tiles[aX, westaxis].visited == false)
        //         {
        //             if (UWTileMap.IsOpen(UWTileMap.current_tilemap.Tiles[aX, westaxis].tileType))
        //             {
        //                 var displaytype = automaptileinfo.GetDisplayType(tile);
        //                 automap.MarkTileVisited(
        //                     level: dungeon_level - 1,
        //                     tileX: aX, tileY: westaxis,
        //                     tiletype: 11,
        //                     displaytype: displaytype);
        //             }
        //         }
        //     }
        //     if (UWTileMap.ValidTile(aX, eastaxis))
        //     {
        //         var tile = UWTileMap.current_tilemap.Tiles[aX, eastaxis];
        //         if (automap.automaps[dungeon_level - 1].tiles[aX, eastaxis].visited == false)
        //         {
        //             if (UWTileMap.IsOpen(UWTileMap.current_tilemap.Tiles[aX, eastaxis].tileType))
        //             {
        //                 var displaytype = automaptileinfo.GetDisplayType(tile);
        //                 automap.MarkTileVisited(
        //                     level: dungeon_level - 1,
        //                     tileX: aX, tileY: eastaxis,
        //                     tiletype: 11,
        //                     displaytype: displaytype);
        //             }
        //         }
        //     }
        // }
        // return range;
        //}


    }//end class
}//end namespace
