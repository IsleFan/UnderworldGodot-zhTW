using Peaky.Coroutines;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Underworld
{

    public class Teleportation : UWClass
    {
        //Constants for level change events
        const int EnterLevelMode = 0;
        const int ExitLevelMode = 1;
        const int LoadSaveMode = 3;
        /// <summary>
        /// Function to call when teleporting.
        /// </summary>
        public delegate void TeleportCallBack();
        static int TeleportLevel = -1;//make these private.
        static int TeleportTileX = -1;
        static int TeleportTileY = -1;
        static int HeadingHeightTeleportFlag = -1;//used in uw2 to control teleporting in to levels at the ceiling.


        /// <summary>
        /// A function to call once teleported.
        /// </summary>
        public static TeleportCallBack CodeToRunOnTeleport;

        /// <summary>
        /// npp_func: runs once the new level is loaded and before the landing square is looked for, so it may move the
        /// target (moveto). The arrival's music and messages stay in CodeToRunOnTeleport, which runs last.
        /// </summary>
        public static TeleportCallBack CodeToRunBeforeLanding;

        /// <summary>
        /// NewPlyFade: whether the view goes dark while the player moves (bit 0 fading out, bit 1 back in). A death's
        /// resurrection clears it, the death having faded the view already; it is 3 again after each teleport.
        /// </summary>
        public static int NewPlyFade = 3;


        /// <summary>
        /// Processes a pending teleportation. Run only on game loop update and not during a trap chain.
        /// </summary>
        public static void HandleTeleportation()
        {
            var pending = (TeleportLevel != -1) || ((TeleportTileX != -1) && (TeleportTileY != -1));
            if (Fading)
            {
                return;
            }
            if (pending && ((NewPlyFade & 1) != 0) && !fadedOut)
            {//new_player_pos: the view fades out first (fadeout3d), the game held meanwhile
                _ = Peaky.Coroutines.Coroutine.Run(Fade3D(fadeIn: false), main.instance);
                return;
            }
            fadedOut = false;
            var fadeIn = pending && ((NewPlyFade & 2) != 0);
            if (TeleportLevel != -1)
            {
                uimanager.WaitingForFade = true;

                //ChangeLevel: fighting ends and an object waiting to be used on something is let go
                if ((combat.stage != combat.CombatStages.OutOfCombat) && (combat.stage != combat.CombatStages.Ready))
                {
                    combat.EndCombatLoop();
                }
                if (useon.CurrentItemBeingUsed != null)
                {
                    useon.CurrentItemBeingUsed = null;
                    uimanager.instance.mousecursor.SetCursorToCursor();
                }

                //handle leave level events (before the level is saved, with the cursor object still held: do_level_hacks(from, 1))
                LevelChangeEvents(ExitLevelMode, playerdat.dungeon_level);

                //The object on the cursor goes with the player, kept with the inventory while the levels change (INVSAVE.C).
                short carriedObject = 0;
                short carriedWeight = 0;
                if (playerdat.ObjectInHand != -1)
                {
                    var obj = UWTileMap.current_tilemap.LevelObjects[playerdat.ObjectInHand];
                    playerdat.ObjectInHand = -1;
                    var weight = playerdat.WeightCarried;
                    obj.next = 0;
                    carriedObject = playerdat.AddObjectToPlayerInventory(obj.index, IncludeNext: false);
                    carriedWeight = (short)(playerdat.WeightCarried - weight);
                    playerdat.WeightCarried = weight;
                }

                //remove player from the level
                playerdat.PlacePlayerInTile(newTileX: -1, newTileY: -1, previousTileX: playerdat.playerObject.tileX, previousTileY: playerdat.playerObject.tileY);//removes the player from the tile map data


                //move player object to the save file and clear the data out.
                for (int i = 0; i <= 0x1A; i++)
                {
                    playerdat.pdat[playerdat.PlayerObjectStoragePTR + i] = playerdat.playerObject.DataBuffer[playerdat.playerObject.PTR + i];
                    playerdat.playerObject.DataBuffer[playerdat.playerObject.PTR + i] = 0;
                }

                playerdat.dungeon_level = TeleportLevel;

                //switch level
                UWTileMap.LoadTileMap(
                        newLevelNo: playerdat.dungeon_level - 1,
                        datafolder: playerdat.currentfolder,
                        newGameSession: false);

                //copy back the player object
                //copy player object from the save file back into the tilemap objects
                for (int i = 0; i <= 0x1A; i++)
                {
                    playerdat.playerObject.DataBuffer[playerdat.playerObject.PTR + i] = playerdat.pdat[playerdat.PlayerObjectStoragePTR + i];
                }
                
                //Reset the camera reference as the old reference belonged to a previous map
                playerdat.CameraReference = playerdat.playerObject;

                RunCodeBeforeLanding();
                if ((TeleportTileX != -1) && (TeleportTileY != -1))
                {
                    FindLandingSquare();
                    if (_RES == GAME_UW2)
                    {
                        //move to new tile on different level
                        InitialisePlayerOnLevelOrPositionChange(tileX: TeleportTileX, tileY: TeleportTileY, CeilingSpawn: (HeadingHeightTeleportFlag & 0x1) == 1);
                        if ((HeadingHeightTeleportFlag & 0x20) != 0)
                        {
                            HeadingHeightTeleportFlag &= 0xFFDC;
                            playerdat.playerObject.heading = (short)((HeadingHeightTeleportFlag >> 2) & 0x7);
                            playerdat.PlayerCameraYaw_dseg_8294 = (short)(HeadingHeightTeleportFlag << 0xB);
                            motion.PlayerMotionYaw_dseg_67d6_8296 = (short)(HeadingHeightTeleportFlag << 0xB);
                        }
                    }
                    else
                    {
                        //move to new tile on different level
                        InitialisePlayerOnLevelOrPositionChange(tileX: TeleportTileX, tileY: TeleportTileY, CeilingSpawn: false);
                    }
                }

                if (carriedObject != 0)
                {//back on the cursor in the new level
                    var weight = playerdat.WeightCarried;
                    var held = playerdat.AddInventoryObjectToWorld(objIndex: carriedObject, updateUI: false, RemoveNext: false, DestroyInventoryObject: true, ClearLink: true);
                    if (held != 0)
                    {
                        playerdat.WeightCarried = weight;
                        UWTileMap.current_tilemap.LevelObjects[held].next = 0;
                        playerdat.ObjectInHand = held;
                        uimanager.instance.mousecursor.SetCursorToObject(UWTileMap.current_tilemap.LevelObjects[held].item_id);
                    }
                    else
                    {//no free objects on the new level (the original does not check): keep it in an empty inventory slot, else it is lost
                        var slot = uimanager.FreeGeneralUseSlot;
                        if (slot != -1)
                        {//a held object carries no weight, a carried one does
                            playerdat.SetInventorySlotListHead(slot, carriedObject);
                            playerdat.WeightCarried += carriedWeight;
                        }
                        else
                        {//lost, with everything inside it
                            void RemoveWithContents(int index)
                            {
                                var lost = playerdat.InventoryObjects[index];
                                if ((lost.is_quant == 0) && (lost.link != 0))
                                {
                                    var content = (int)lost.link;
                                    while (content != 0)
                                    {
                                        var next = playerdat.InventoryObjects[content].next;
                                        RemoveWithContents(content);
                                        content = next;
                                    }
                                }
                                playerdat.RemoveFromInventory(index: index, updateUI: false, ClearLink: false);
                            }
                            RemoveWithContents(carriedObject);
                            playerdat.WeightCarried = weight;
                        }
                        uimanager.RefreshWeightDisplay();
                        uimanager.UpdateInventoryDisplay();
                    }
                }

                //Handle enter level events
                LevelChangeEvents(EnterLevelMode, playerdat.dungeon_level);
                if (_RES == GAME_UW2)
                {//restore_mana(ThePlayer, 0): the mana no more than its maximum, outside the Academy's no-mana levels
                    var level = (playerdat.dungeon_level - 1) % 8 + 1;
                    if ((worlds.GetWorldNo(playerdat.dungeon_level) != (int)worlds.world_ids.Academy) || (level <= 1)
                        || ((level >= 8) && (playerdat.playerObject.npc_xhome >= 25)))
                    {
                        playerdat.play_mana = System.Math.Min(playerdat.play_mana, playerdat.max_mana);
                    }
                }

                if (_RES == GAME_UW2)
                {//SCD.ARK then needs to be processed in UW2
                    Debug.Print("Processing SCD due to level transition");
                    scd.ProcessSCDArk(1);
                }
            }
            else
            {
                //teleport within a map
                RunCodeBeforeLanding();
                if ((TeleportTileX != -1) && (TeleportTileY != -1))
                {
                    uimanager.WaitingForFade = true;
                    FindLandingSquare();
                    //move to new tile
                    if (_RES == GAME_UW2)
                    {
                        InitialisePlayerOnLevelOrPositionChange(tileX: TeleportTileX, tileY: TeleportTileY, CeilingSpawn: (HeadingHeightTeleportFlag & 0x1) == 1, removefromtile: true);
                        if ((HeadingHeightTeleportFlag & 0x20) != 0)
                        {
                            HeadingHeightTeleportFlag &= 0xFFDC;
                            playerdat.playerObject.heading = (short)((HeadingHeightTeleportFlag >> 2) & 0x7);
                            playerdat.PlayerCameraYaw_dseg_8294 = (short)(HeadingHeightTeleportFlag << 0xB);
                            motion.PlayerMotionYaw_dseg_67d6_8296 = (short)(HeadingHeightTeleportFlag << 0xB);
                        }
                    }
                    else
                    {
                        InitialisePlayerOnLevelOrPositionChange(tileX: TeleportTileX, tileY: TeleportTileY, CeilingSpawn: false, removefromtile: true);
                    }
                }
            }


            TeleportLevel = -1;
            TeleportTileX = -1;
            TeleportTileY = -1;
            HeadingHeightTeleportFlag = 0;
            NewPlyFade = 3;
            if (fadeIn)
            {//and back in at the new place (fadein3d)
                _ = Peaky.Coroutines.Coroutine.Run(Fade3D(fadeIn: true), main.instance);
            }
            else
            {
                SetViewBrightness(uimanager.instance.uwviewport, 1f);
            }
            if (CodeToRunOnTeleport != null)
            {//handle a callback.
                CodeToRunOnTeleport();
                CodeToRunOnTeleport = null;//although this should be handled by the method itself
            }
            uimanager.WaitingForFade = false;
        }



        /// <summary>A fade of the 3D view is running; the game is held (blockinput).</summary>
        public static bool Fading { get; private set; }
        static bool fadedOut;

        /// <summary>
        /// fadeout3d / fadein3d (out3d, in3d): steps 0 to 12 darken the view through the rows of the light table,
        /// LIGHT.DAT (cDimFB remaps each pixel's colour through row i), one frame each, then black (colour 1); fading
        /// in goes back up from black. The view here is in colour, not palette indices, so each row is applied as the
        /// brightness it gives the game palette on average.
        /// </summary>
        static System.Collections.IEnumerator Fade3D(bool fadeIn)
        {
            Fading = true;
            var view = uimanager.instance.uwviewport;
            for (int i = fadeIn ? 12 : 0; fadeIn ? i >= 0 : i <= 12; i += fadeIn ? -1 : 1)
            {
                var level = LightRowBrightness(i);
                SetViewBrightness(view, level);
                yield return new Peaky.Coroutines.WaitOneFrame();
            }
            SetViewBrightness(view, fadeIn ? 1f : 0f);
            if (!fadeIn)
            {
                fadedOut = true;
            }
            Fading = false;
        }

        /// <summary>
        /// gruesome_door_hack (UW2): the door in square (x, y) is unlocked and opened, so the player never arrives behind
        /// a shut door (arriving in level 1, and the jail cell).
        /// </summary>
        public static void GruesomeDoorHack(int x, int y)
        {
            if (!UWTileMap.ValidTile(x, y))
            {
                return;
            }
            var arrivalDoor = objectsearch.FindMatchInObjectChain(
                ListHeadIndex: UWTileMap.current_tilemap.Tiles[x, y].indexObjectList,
                majorclass: 5, minorclass: 0, classindex: -1,
                objList: UWTileMap.current_tilemap.LevelObjects);
            if (arrivalDoor != null)
            {
                if (a_lock.GetIsLocked(arrivalDoor))
                {
                    a_lock.SetIsLocked(parentObject: arrivalDoor, value: false, character: 0);
                }
                door.OpenDoor(arrivalDoor);
            }
        }

        /// <summary>The 3D view's shader writes its colour itself, so the fade goes through its brightness.</summary>
        static void SetViewBrightness(Godot.TextureRect view, float level)
        {
            (view.Material as Godot.ShaderMaterial)?.SetShaderParameter("brightness", level);
        }

        static float[] lightRowBrightness;

        /// <summary>How bright row row of LIGHT.DAT leaves the game palette, on average (1 for row 0).</summary>
        static float LightRowBrightness(int row)
        {
            if (lightRowBrightness == null)
            {
                lightRowBrightness = new float[16];
                var pal = PaletteLoader.Palettes?[0];
                var light = PaletteLoader.light;
                for (int r = 0; r < 16; r++)
                {
                    lightRowBrightness[r] = 1f - r / 13f;
                    if ((pal == null) || (light == null) || (r > light.GetUpperBound(0)) || (light[r] == null))
                    {
                        continue;
                    }
                    float full = 0, dimmed = 0;
                    for (int c = 0; c < 256; c++)
                    {
                        var a = pal.ColorAtIndex((byte)c, false, false);
                        var b = pal.ColorAtIndex(light[r].red[c], false, false);
                        full += a.R + a.G + a.B;
                        dimmed += b.R + b.G + b.B;
                    }
                    if (full > 0)
                    {
                        lightRowBrightness[r] = System.Math.Min(1f, dimmed / full);
                    }
                }
            }
            return lightRowBrightness[System.Math.Clamp(row, 0, 15)];
        }

        static void RunCodeBeforeLanding()
        {
            var code = CodeToRunBeforeLanding;
            CodeToRunBeforeLanding = null;
            code?.Invoke();
        }

        /// <summary>
        /// moveto: the target becomes the square of the first object of that class on the level. (The original then
        /// aims at square 0, 0 when there is none, and the player cannot land; here the target stays as it was.)
        /// </summary>
        public static void MoveTo(int majorclass, int minorclass, int classindex)
        {
            var obj = objectsearch.FindMatchInFullObjectList(majorclass, minorclass, classindex, UWTileMap.current_tilemap.LevelObjects);
            if ((obj != null) && UWTileMap.ValidTile(obj.tileX, obj.tileY))
            {
                TeleportTileX = obj.tileX;
                TeleportTileY = obj.tileY;
            }
        }

        /// <summary>
        /// The pending teleport's target square, for a CodeToRunBeforeLanding to move.
        /// </summary>
        public static int TargetX { get => TeleportTileX; set => TeleportTileX = value; }
        public static int TargetY { get => TeleportTileY; set => TeleportTileY = value; }

        /// <summary>
        /// do_mstone: arrive at the moonstone on its level.
        /// </summary>
        public static void MoveToMoonstone()
        {
            MoveTo(4, 2, 6);
        }

        /// <summary>
        /// move_along: moves (x, y) dist along a byte heading (256 to the turn); each nonzero step is rounded away from
        /// zero.
        /// </summary>
        public static void MoveAlong(int heading, int dist, ref int x, ref int y)
        {
            var angle = ((0x140 - heading) & 0xFF) * System.Math.PI / 128;
            int dy = (short)(System.Math.Sin(angle) * 0x7FFF) / 0x80 * dist / 0x100;
            int dx = (short)(System.Math.Cos(angle) * 0x7FFF) / 0x80 * dist / 0x100;
            y += dy + System.Math.Sign(dy);
            x += dx + System.Math.Sign(dx);
        }

        /// <summary>
        /// Finds where the player fits near tile (x, y) (find_good_x_and_y): a breadth first search outward through the
        /// open sides of each tile, at most 20 tiles a ring, within the box of 10 by 10 tiles round (x, y). With clear set,
        /// each tile searched first loses the objects with a height and the mobile objects that culling allows.
        /// </summary>
        static bool FindGoodXAndY(int x, int y, bool clear, out int nx, out int ny)
        {
            nx = x; ny = y;
            var xmin = Math.Min(Math.Max(1, x - 4), 0x3A);
            var ymin = Math.Min(Math.Max(1, y - 4), 0x3A);
            var xmax = Math.Min(xmin + 9, 0x3E);
            var ymax = Math.Min(ymin + 9, 0x3E);
            if ((x < xmin) || (x > xmax) || (y < ymin) || (y > ymax))
            {
                return false;
            }
            var visited = new int[10];//(the original's 9 entries are one short of the box, so x - xmin = 9 writes past it)
            var cur = new List<(int, int)> { (x, y) };
            visited[x - xmin] |= 1 << (y - ymin);
            while (cur.Count > 0)
            {
                var next = new List<(int, int)>();
                void Visit(int vx, int vy)
                {
                    if ((next.Count < 20) && (vx >= xmin) && (vx <= xmax) && (vy >= ymin) && (vy <= ymax)
                        && ((visited[vx - xmin] & (1 << (vy - ymin))) == 0))
                    {
                        next.Add((vx, vy));
                        visited[vx - xmin] |= 1 << (vy - ymin);
                    }
                }
                foreach (var (tx, ty) in cur)
                {
                    var tile = UWTileMap.current_tilemap.Tiles[tx, ty];
                    if (clear)
                    {
                        var index = tile.indexObjectList;
                        while (index != 0)
                        {
                            var o = UWTileMap.current_tilemap.LevelObjects[index];
                            index = o.next;
                            if ((o.index != 1) && ((commonObjDat.height(o.item_id) != 0) || !o.IsStatic))
                            {//Obj_Punt: only what culling allows
                                if (ObjectRemover_OLD.ObjectCullingTest(o, 0xA))
                                {
                                    ObjectRemover_OLD.ClearContents(o);
                                    ObjectRemover_OLD.DeleteObjectFromTile_DEPRECIATED(tx, ty, o.index);
                                }
                            }
                        }
                    }
                    var z = (tile.floorHeight << 3) + (((motion.TileTraversalFlags_dseg_67d6_1BA6[tile.tileType] & 0x20) != 0) ? 4 : 0);
                    if (motion.CanPlace(127, 1, (tx << 3) + 3, (ty << 3) + 3, z, false, 8))
                    {
                        nx = tx; ny = ty;
                        return true;
                    }
                    switch (tile.tileType)
                    {
                        case UWTileMap.TILE_SOLID:
                            break;
                        case UWTileMap.TILE_DIAG_SE:
                            Visit(tx + 1, ty); Visit(tx, ty - 1);
                            break;
                        case UWTileMap.TILE_DIAG_SW:
                            Visit(tx - 1, ty); Visit(tx, ty - 1);
                            break;
                        case UWTileMap.TILE_DIAG_NE:
                            Visit(tx + 1, ty); Visit(tx, ty + 1);
                            break;
                        case UWTileMap.TILE_DIAG_NW:
                            Visit(tx - 1, ty); Visit(tx, ty + 1);
                            break;
                        default:
                            Visit(tx - 1, ty); Visit(tx, ty - 1); Visit(tx + 1, ty); Visit(tx, ty + 1);
                            break;
                    }
                }
                cur = next;
            }
            return false;
        }

        /// <summary>
        /// Where the player lands (new_player_pos): a free square near the target, strictly then clearing objects away.
        /// With none the player dies. (The original then leaves the player unplaced; here the player still lands on the target.)
        /// </summary>
        static void FindLandingSquare()
        {
            if (FindGoodXAndY(TeleportTileX, TeleportTileY, false, out int nx, out int ny)
                || FindGoodXAndY(TeleportTileX, TeleportTileY, true, out nx, out ny))
            {
                TeleportTileX = nx; TeleportTileY = ny;
            }
            else
            {
                playerdat.play_hp = 0;
            }
        }

        /// <summary>
        /// Handles the reseting of the player motion params when teleporting to a new tile, starting a new game or changing levels
        /// </summary>
        /// <param name="tileX"></param>
        /// <param name="tileY"></param>
        /// <param name="CeilingSpawn"></param>
        /// <param name="removefromtile"></param>
        public static void InitialisePlayerOnLevelOrPositionChange(int tileX, int tileY, bool CeilingSpawn, bool removefromtile = false)
        {
            var tile = UWTileMap.current_tilemap.Tiles[tileX, tileY];//place object in tile will refresh player object on next game tick.

            if (removefromtile)
            {
                playerdat.PlacePlayerInTile(-1, -1, -1, -1);
            }

            Loader.setAt(MotionHandler.PlayerMotionHandler.handlerdata, 0, 16, 0);//Loader.setAt(UWMotionParamArray.PlayerMotionHandler_dseg_67d6_26AA, 0, 16, 0);
            motion.playerMotionParams.momentum_14 = 0;
            motion.playerMotionParams.gravity_10_Z = 0;
            motion.playerMotionParams.unk_e_Y = 0;

            motion.playerMotionParams.unk_c_X = 0;
            motion.playerMotionParams.unk_a_pitch = 0;
            motion.playerMotionParams.unk_8_y = 0;

            motion.playerMotionParams.unk_6_x = 0;

            motion.playerMotionParams.x_0 = (short)((tileX << 8) + 0x80);

            motion.playerMotionParams.y_2 = (short)((tileY << 8) + 0x80);
            motion.playerMotionParams.z_4 = (short)(tile.floorHeight * 0x40);

            motion.playerMotionParams.unk_24 = 8;
            motion.playerMotionParams.index_20 = 1;



            
            if ((motion.TileTraversalFlags_dseg_67d6_1BA6[tile.tileType] & 0x20) != 0)
            {
                motion.playerMotionParams.z_4 += 0x20;
            }

            if (CeilingSpawn)
            {
                if (1000 - commonObjDat.height(127) > motion.playerMotionParams.z_4)
                {
                    motion.playerMotionParams.z_4 = (short)(1000 - (commonObjDat.height(127) << 3));
                    motion.playerMotionParams.gravity_10_Z = -4;
                }
            }
            playerdat.playerObject.zpos = (short)(motion.playerMotionParams.z_4 >> 3);
            playerdat.playerObject.xpos = 3;//player_setup: fine 3, 3, the walking sequence (UW1 0x2C) and no quality
            playerdat.playerObject.ypos = 3;
            playerdat.playerObject.npc_animation = (short)(_RES == GAME_UW2 ? 1 : 0x2C);
            playerdat.playerObject.quality = 0;
            playerdat.playerObject.npc_xhome = (short)tileX;
            playerdat.playerObject.npc_yhome = (short)tileY;
            playerdat.playerObject.tileX = tileX;
            playerdat.playerObject.tileY = tileY;
            playerdat.playerObject.next = 0;

            MotionCalcArray.PtrToMotionCalc = new byte[0x20];

            MotionCalcArray.MotionArrayObjectIndexA = 1;
            MotionCalcArray.Radius8 = (byte)commonObjDat.radius(127);
            MotionCalcArray.Height9 = (byte)commonObjDat.height(127);
            MotionCalcArray.x0 = (ushort)(3 + (tileX << 3));
            MotionCalcArray.y2 = (ushort)(3 + (tileY << 3));
            MotionCalcArray.z4 = (ushort)(motion.playerMotionParams.z_4 >> 3);

            motion.ProcessMotionTileHeights_seg028_2941_385(motion.playerMotionParams.unk_24);
            motion.playerMotionParams.tilestate25 = motion.GetTileState(MotionCalcArray.UnkC_terrain | MotionCalcArray.UnkE);

            motion.ProcessPlayerTileState(motion.playerMotionParams.tilestate25, 0);
            motion.WalkOnSurfaceType();
            motion.PlayerMotionUpdateRequired_dseg_D3 = true;

            playerdat.PlacePlayerInTile(newTileX: tileX, newTileY: tileY, previousTileX: -1, previousTileY: -1);
            


            // playerdat.playerObject.zpos = (short)(tile.floorHeight << 3);
            // playerdat.playerObject.xpos = 3; playerdat.playerObject.ypos = 3;
            // playerdat.tileX = tileX; playerdat.tileY = tileY;

            playerdat.PositionPlayerCamera();

            // main.gamecam.Position = uwObject.GetCoordinate(
            //     tileX: playerdat.tileX,
            //     tileY: playerdat.tileY,
            //     _xpos: playerdat.xpos,
            //     _ypos: playerdat.ypos,
            //     _zpos: playerdat.camerazpos);
        }



        /// <summary>
        /// Handles a request to teleport a charcter. Does not do the final teleport but queues it up for the HandleTeleport function to process.
        /// </summary>
        /// <param name="character"></param>
        /// <param name="tileX"></param>
        /// <param name="tileY"></param>
        /// <param name="newLevel"></param>
        public static int Teleport(int character, int tileX, int tileY, int newLevel, int HeadingHeightFlag)
        {
            if (_RES == GAME_UW2)
            {
                return TeleportUW2(character: character, tileX: tileX, tileY: tileY, newLevel: newLevel, NewHeadingHeightFlag: HeadingHeightFlag);
            }
            else
            {
                return TeleportUW1(character: character, tileX: tileX, tileY: tileY, newLevel: newLevel);
            }
        }

        /// <summary>
        /// Applies specific UW2 rules for teleportation.
        /// </summary>
        /// <param name="character"></param>
        /// <param name="tileX"></param>
        /// <param name="tileY"></param>
        /// <param name="newLevel"></param>
        /// <param name="NewHeadingHeightFlag"></param>
        static int TeleportUW2(int character, int tileX, int tileY, int newLevel, int NewHeadingHeightFlag)
        {
            if (character == 0)
            {
                if (playerdat.DreamPlantCounter > 0)
                {
                    if (playerdat.DreamingInVoid)
                    {
                        if (newLevel != 0)
                        {
                            if (worlds.GetWorldNo(newLevel) != 8)
                            {
                                return 2;
                            }
                        }
                    }
                }
                if (playerdat.GetQuest(112) != 0)//avatar fighting in the castle
                {
                    if (newLevel != 0)
                    {
                        if (newLevel != 1)
                        {
                            ChangeUW2Quests();
                            goto ProcessTeleport;
                        }
                    }
                    if ((tileY != 38) || (tileX < 42) || (tileX > 43))
                    {//when not in tile 38,43?
                        ChangeUW2Quests();
                        goto ProcessTeleport;
                    }
                }

            ProcessTeleport:
                if (newLevel != playerdat.dungeon_level)
                {
                    if (character != 0)
                    {
                        //non PC teleport to a new level. Should not happen.
                        return 2;
                    }
                }
                if (!SameLevelSquareFound(ref tileX, ref tileY, newLevel))
                {
                    return 2;
                }

                //queue up the teleport.
                if (newLevel == 0)
                {
                    TeleportLevel = -1;
                }
                else
                {
                    TeleportLevel = newLevel;
                }
                TeleportTileX = tileX;
                TeleportTileY = tileY;
                HeadingHeightTeleportFlag = NewHeadingHeightFlag;
                //

                if (newLevel != 0)
                {
                    if (newLevel == playerdat.dungeon_level)
                    {
                        TeleportLevel = -1;//same level.
                    }
                }

                bool coward = false;
                if (playerdat.IsFightingInPit)
                {//the avatar runs from the fight by leaving the level, landing outside the arenas or in another of them (do_teleport)
                    var dest = newLevel == 0 ? playerdat.dungeon_level : newLevel;
                    var oldX = playerdat.playerObject.tileX; var oldY = playerdat.playerObject.tileY;
                    if (
                        (dest != playerdat.dungeon_level)
                        || !InArena(tileX, tileY)
                        || ((Mpos(tileX - 0x1F, tileY - 0x1F) >> 1) != (Mpos(oldX - 0x1F, oldY - 0x1F) >> 1))
                        )
                    {
                        coward = true;
                    }
                }
                if (coward)
                {
                    pitsofcarnage.AvatarIsACoward(skipConversation: true);
                }

                return 0x10;
            }
            else
            {
                return 2;
            }
        }



        static void ChangeUW2Quests()
        {
            playerdat.SetQuest(112, 0);//checked when talking to LB. You have been fighting with the others
            playerdat.SetQuest(124, 1);//is referenced in teleport_trap in relation to quest 112 fighting in the castle. 
        }

        /// <summary>
        /// Whether square x, y is inside one of the four arenas of the Pits of Carnage, around the square at 0x1F, 0x1F (in_arena).
        /// </summary>
        static bool InArena(int x, int y)
        {
            int[] limits = { 0xF, 0x10, 0xE, 0xF };
            x -= 0x1F;
            y -= 0x1F;
            var arena = Mpos(x, y) >> 1;
            x = System.Math.Abs(x);
            y = System.Math.Abs(y);
            if (x < 3 || y < 3)
            {
                return false;
            }
            return limits[arena] >= x && limits[arena] >= y;
        }

        /// <summary>
        /// The direction of the vector x, y, 0 to 7: 0 is +y, 2 +x, 4 -y, 6 -x and the odd values the diagonals
        /// between; a component counts as zero when it is less than half the other (mpos).
        /// </summary>
        static int Mpos(int x, int y)
        {
            x = (sbyte)x; y = (sbyte)y;
            if (System.Math.Abs(x) / 2 > System.Math.Abs(y))
            {
                return x > 0 ? 2 : 6;
            }
            if (System.Math.Abs(y) / 2 > System.Math.Abs(x))
            {
                return y > 0 ? 0 : 4;
            }
            if (x < 0)
            {
                return (y > 0 ? 2 : 0) + 5;
            }
            return (y < 0 ? 2 : 0) + 1;
        }

        /// <summary>
        /// do_teleport: within this level (level 0 or this one, and not square 63) a free square must be found near the target.
        /// </summary>
        static bool SameLevelSquareFound(ref int tileX, ref int tileY, int newLevel)
        {
            if (((newLevel == 0) || (newLevel == playerdat.dungeon_level)) && (tileX != 0x3F) && (tileY != 0x3F))
            {
                if (!FindGoodXAndY(tileX, tileY, false, out int nx, out int ny))
                {
                    return false;
                }
                tileX = nx; tileY = ny;
            }
            return true;
        }

        static int TeleportUW1(int character, int tileX, int tileY, int newLevel)
        {
            if (!SameLevelSquareFound(ref tileX, ref tileY, newLevel))
            {
                return 2;
            }
            if (newLevel != 0)
            {
                if (newLevel == playerdat.dungeon_level)
                {
                    TeleportLevel = -1;//same level.
                }
                else
                {
                    TeleportLevel = newLevel;
                }
            }
            else
            {
                TeleportLevel = -1;
            }
            TeleportTileX = tileX;
            TeleportTileY = tileY;
            return 2;
        }

        /// <summary>
        /// Moves the player to the position of the moonstone after teleportation using the Gate Travel Spell
        /// </summary>
        public static void JumpToMoonStoneOnLevel()
        {
            //find the moonstone
            var moonstone = objectsearch.FindMatchInFullObjectList(4, 2, 6, UWTileMap.current_tilemap.LevelObjects);
            if (moonstone != null)
            {
                if (UWTileMap.ValidTile(moonstone.tileX, moonstone.tileY))
                {
                    InitialisePlayerOnLevelOrPositionChange(moonstone.tileX, moonstone.tileY, CeilingSpawn: false);
                }
            }
        }


        /// <summary>
        /// The level's special cases when a game starts (do_level_hacks): a new game arrives on its level (MAINMENU.C, mode
        /// 0, the arrival square the player's), a restored one runs the after-restore case (RestoreGame, mode 3: UW1 turns
        /// the automap off in the Ethereal Void).
        /// </summary>
        public static void LevelEventsOnGameStart(bool newGame)
        {
            //reset_game's reset_times, for a new game and a restored one alike: lstime, nextSpellTime, nextstep, watertime
            RunicMagic.ResetSpellCastInterval();
            MagicEnchantment.ResetSpellTimer();
            playerdat.ResetFootStepTimers();
            if (newGame)
            {
                TeleportTileX = playerdat.playerObject.tileX;
                TeleportTileY = playerdat.playerObject.tileY;
                LevelChangeEvents(EnterLevelMode, playerdat.dungeon_level);
                TeleportTileX = -1;
                TeleportTileY = -1;
            }
            else
            {//RestoreGame: reset_game (UW1 turns the automap back on, the object spell timer is cleared), GetLevel's
             //init_level_creature_stuff (the last critter hit and the critters' paths forgotten), then mode 3
                if (_RES != GAME_UW2)
                {
                    playerdat.AutomapEnabled = true;
                }
                playerdat.LastDamagedNPCIndex = 0; playerdat.LastDamagedNPCType = -1;
                CallBacks.RunCodeOnAllMobiles(ClearUnkBit_0x15_7_OnMobiles, null); npc.ClearPaths();
                LevelChangeEvents(LoadSaveMode, playerdat.dungeon_level);
            }
        }

        /// <summary>
        /// Handle events that happen when player leaves and enters dungeon levels
        /// </summary>
        /// <param name="mode">0 = new game/enter level, 1 = leave level, 3 = load save game</param>
        /// <param name="dungeon">Either the new or previous dungeon.</param>
        static void LevelChangeEvents(int mode, int dungeon)
        {
            if (_RES == GAME_UW2)
            {
                LevelChangeEventsUW2(mode, dungeon);
            }
            else
            {
                LevelChangeEventsUW1(mode, dungeon);
            }
        }

        /// <summary>
        /// Handle events that happen when player leaves and enters dungeon levels in UW2
        /// </summary>
        /// <param name="mode">0 = new game/enter level, 1 = leave level, 3 = load save game</param>
        /// <param name="dungeon">Either the new or previous dungeon.</param>
        static void LevelChangeEventsUW2(int mode, int dungeon)
        {
            if (playerdat.armageddon && mode == EnterLevelMode)
            {//entering a level under the influence of armageddon
                Debug.Print("Armageddon flag has been set. Resetting map!");
                UWTileMap.ResetMap(0);
            }
            else
            {
                if (mode == EnterLevelMode)
                {
                    playerdat.LastDamagedNPCIndex = 0; playerdat.LastDamagedNPCType = -1;
                    CallBacks.RunCodeOnAllMobiles(ClearUnkBit_0x15_7_OnMobiles, null); npc.ClearPaths();
                    if (playerdat.play_drawn == 0)
                    {//the walking music of the world, if it has changed (set_random_walking_music(-2))
                        XMIMusic.PickLevelThemeMusic(-2);
                    }
                    if (dungeon == 1)
                    {
                        playerdat.playerObject.ProjectileSourceID = 0;
                        GruesomeDoorHack(TeleportTileX, TeleportTileY);
                    }
                }
                else
                {
                    if (mode == ExitLevelMode)
                    {
                        //Calm NPCs?
                        //Debug.Print("'Calm NPCS'");
                        uwObject.ResetMobileObjects();
                    }
                }

                //World specific events
                switch ((worlds.world_ids)worlds.GetWorldNo(dungeon))
                {
                    case worlds.world_ids.PrisonTower:
                        {
                            if (mode == ExitLevelMode)
                            {
                                //do a check to see if player has been fighting goblins and set quest 60 is that is the case.
                                prisontower.PrisonTowerQuest60();

                                if (dungeon == 10)
                                {
                                    trigger.TriggerTrapInTile(0x1F, 0x24);
                                    trigger.TriggerTrapInTile(0x23, 0x22);
                                }
                            }
                            break;
                        }
                    case worlds.world_ids.Killorn:
                        //handle killorn is crashing
                        if ((playerdat.GetQuest(50) == 1) && (mode == ExitLevelMode))
                        {
                            if (dungeon == 17)
                            {
                                killorn.KilornIsCrashing(isEnteringLevel: true);
                            }
                        }
                        //the original's case falls into the Academy's: leaving Killorn's level 3 takes the wand away too
                        if ((dungeon == 19) && (mode == ExitLevelMode))
                        {
                            academy.RemoveAcademyWand();
                        }
                        break;
                    case worlds.world_ids.Academy:
                        //Handle removing the puzzle wand from inventory on level 3 and returning it to it's home position
                        if ((dungeon == 43) && (mode == ExitLevelMode))
                        {
                            academy.RemoveAcademyWand();
                        }
                        break;
                    case worlds.world_ids.Tomb:
                        //Handle killing undead when Loth has been put to rest.
                        if ((playerdat.GetQuest(7) == 1) && (mode == EnterLevelMode))
                        {
                            //loth is dead
                            tomb.KillLothsLiches(-1);
                        }
                        break;
                    case worlds.world_ids.Ethereal:
                        //Handle automap enabled settings
                        if (mode == EnterLevelMode)
                        {
                            playerdat.AutomapEnabled_backup = playerdat.AutomapEnabled;// needs to be backedup due to rarely seen mechanics involving getting lost
                            playerdat.AutomapEnabled = false;
                        }
                        else
                        {
                            if (mode == ExitLevelMode)
                            {
                                playerdat.AutomapEnabled = playerdat.AutomapEnabled_backup;
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Handle events that happen when player leaves and enters dungeon levels in UW1
        /// </summary>
        /// <param name="mode">0 = new game/enter level, 1 = leave level, 3 = load save game</param>
        /// <param name="dungeon">Either the new or previous dungeon.</param>
        static void LevelChangeEventsUW1(int mode, int dungeon)
        {
            if (playerdat.armageddon && mode == EnterLevelMode)
            {//entering a level under the influence of armageddon
                UWTileMap.ResetMap(0);
            }
            else
            {
                if (mode == EnterLevelMode)
                {
                    playerdat.LastDamagedNPCIndex = 0; playerdat.LastDamagedNPCType = -1;
                    CallBacks.RunCodeOnAllMobiles(ClearUnkBit_0x15_7_OnMobiles, null); npc.ClearPaths();
                }
                else
                {
                    if (mode == ExitLevelMode)
                    {
                        //Calm NPCs?
                        //Debug.Print("'Calm NPCS'");
                        uwObject.ResetMobileObjects();
                    }
                }
                switch ((worlds.UW1_Dungeons)dungeon)
                {
                    case worlds.UW1_Dungeons.Tybal:
                        {
                            if (!playerdat.isOrbDestroyed)
                            {
                                switch (mode)
                                {
                                    case EnterLevelMode:
                                        //entering lair while orb is not yet destroyed
                                        playerdat.backup_mana = playerdat.max_mana;
                                        playerdat.max_mana = 0;
                                        playerdat.play_mana = 0;
                                        break;
                                    case ExitLevelMode://restore drained mana.
                                        playerdat.max_mana = playerdat.backup_mana;
                                        playerdat.play_mana = (playerdat.max_mana >> 2);
                                        break;
                                }
                            }
                            break;
                        }
                    case worlds.UW1_Dungeons.Ethereal:
                        {
                            switch (mode)
                            {
                                case EnterLevelMode:
                                    if (playerdat.AutomapEnabled)
                                    {//yes this is correct. Backup mana is repurposed for this use on the void.
                                        playerdat.backup_mana = 1;
                                    }
                                    else
                                    {
                                        playerdat.backup_mana = 0;
                                    }
                                    break;
                                case ExitLevelMode:
                                    playerdat.AutomapEnabled = (playerdat.backup_mana > 0);
                                    break;
                                case LoadSaveMode:
                                    playerdat.AutomapEnabled = false;
                                    break;
                            }
                            break;
                        }
                }
            }
        }


        static void ClearUnkBit_0x15_7_OnMobiles(uwObject obj, int[] paramsarray)
        {
            obj.UnkBit_0X15_Bit7 = 0;
        }

    }//end class
}//end namespace