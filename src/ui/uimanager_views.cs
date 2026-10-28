using System.Diagnostics;
using Godot;
using Godot.Collections;

namespace Underworld
{
    public partial class uimanager : Node2D
    {
        [ExportGroup("View")]
        [Export] public Camera3D cam;
        //[Export] public Node3D freelook;

        [Export] public TextureRect uwviewport;
        [Export] public SubViewport uwsubviewport;

        [Export] public SubViewport uwsubviewport_world;
        [Export] public SubViewport uwsubviewport_sprites;
        [Export] public SubViewport uwsubviewport_objectinfo;
        [Export] public TextureRect textureobjectinfo;

        //[Export] public TextureRect combinedview;

        [Export] public mouseCursor mousecursor;
        [Export] public CanvasLayer uw1UI;
        [Export] public CanvasLayer uw2UI;

        [Export] public TextureRect mainwindowUW1;
        [Export] public TextureRect mainwindowUW2;


        public static bool Fullscreen = false; //someday

        public static float ViewPortMouseXPos;
        public static float ViewPortMouseYPos;

        public static float Window3DLeftBorder
        {
            get
            {
                if (UWClass._RES == UWClass.GAME_UW2)
                {
                    return 16f;
                }
                else
                {
                    return 52f;
                }
            }
        }

        public static int Window3DMaxX
        {
            get
            {
                if (UWClass._RES == UWClass.GAME_UW2)
                {
                    return 208;
                }
                else
                {
                    return 172;
                }
            }
        }

        public static int Window3DMaxY
        {
            get
            {
                if (UWClass._RES == UWClass.GAME_UW2)
                {
                    return 128;
                }
                else
                {
                    return 113;
                }
            }
        }

        public static int Window3DHeadingAdjust
        {
            get
            {
                if (UWClass._RES == UWClass.GAME_UW2)
                {
                    return 104;
                }
                else
                {
                    return 86;
                }
            }
        }

        public static int Window3DPitchAdjust
        {
            get
            {
                if (UWClass._RES == UWClass.GAME_UW2)
                {
                    return 64;
                }
                else
                {
                    return 56;
                }
            }
        }

        public static int Window3DDropThreshold
        {
            get
            {
                if (UWClass._RES == UWClass.GAME_UW2)
                {
                    return 42;
                }
                else
                {
                    return 37;
                }
            }
        }

        private void InitViews()
        {
            switch (UWClass._RES)
            {
                case UWClass.GAME_UW2:
                    mainwindowUW2.Texture = bitmaps.LoadImageAt(BytLoader.UW2ThreeDWin_BYT, true);
                    if (!Fullscreen)
                    {
                        uwviewport.SetSize(new Vector2(840f, 512f));
                        uwviewport.Position = new Vector2(62f, 62f);
                        //uwsubviewport.Size = new Vector2I(840, 512);
                        uimanager.instance.uwsubviewport_world.Size = new Vector2I(840, 512);
                        uimanager.instance.uwsubviewport_sprites.Size = new Vector2I(840, 512);
                        uimanager.instance.uwsubviewport_objectinfo.Size = new Vector2I(840, 512);
                        uimanager.instance.textureobjectinfo.Position = uwviewport.Position;
                        uimanager.instance.textureobjectinfo.Size = uwviewport.Size;
                    }
                    break;
                default:

                    mainwindowUW1.Texture = bitmaps.LoadImageAt(BytLoader.MAIN_BYT, true);
                    if (!Fullscreen)
                    {
                        uwviewport.SetSize(new Vector2(700f, 456f));
                        uwviewport.Position = new Vector2(200f, 72f);
                        //.SetSize(new Vector2(700f, 456f));
                        //uwsubviewport.Size = new Vector2I(700, 456);
                        uimanager.instance.uwsubviewport_world.Size = new Vector2I(700, 456);
                        uimanager.instance.uwsubviewport_sprites.Size = new Vector2I(700, 456);
                        uimanager.instance.uwsubviewport_objectinfo.Size = new Vector2I(700, 456);
                        uimanager.instance.textureobjectinfo.Position = uwviewport.Position;
                        uimanager.instance.textureobjectinfo.Size = uwviewport.Size;
                    }
                    break;
            }
        }


        /// <summary>
		/// Checks if the mouse cursor is over the viewport
		/// </summary>
		/// <returns></returns>
		public static bool IsMouseInViewPort()
        {
            if (Controls.MouseLookActive)
            {
                return true;//aiming at the middle of the view
            }
            var viewportmouspos = instance.uwviewport.GetLocalMousePosition();
            if (
                (viewportmouspos.X >= 0) && (viewportmouspos.Y >= 0)
                &&
                (viewportmouspos.X <= instance.uwviewport.Size.X) && (viewportmouspos.Y <= instance.uwviewport.Size.Y)
                )
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        //A look click in the default mode waiting to become a drag that picks the object up (mouse_dragged).
        static int LookDragIndex = -1;
        static MouseButton LookDragButton;
        static Vector2 LookDragStart;

        /// <summary>
        /// Follows the mouse after a look click in the default mode: once the pointer has moved more than six pixels of
        /// the original 320x200 screen with the button still down, the object is picked up (player_3dlook's
        /// mouse_dragged, then player_3dget); releasing the button first ends it.
        /// </summary>
        public static void ContinueLookDrag(InputEvent @event)
        {
            if (LookDragIndex < 0)
            {
                return;
            }
            if (@event is InputEventMouseButton release && !release.Pressed && release.ButtonIndex == LookDragButton)
            {
                LookDragIndex = -1;
            }
            else if (@event is InputEventMouseMotion motion)
            {
                var moved = motion.Position - LookDragStart;
                if (Mathf.Abs(moved.X) + Mathf.Abs(moved.Y) > 6 * 4)//the screen is drawn at four times 320x200
                {
                    var index = LookDragIndex;
                    LookDragIndex = -1;
                    if (InGame && !blockinput && InteractionMode == InteractionModes.ModeNone && playerdat.ObjectInHand == -1)
                    {//player_3dget in the default mode: what cannot be taken is talked to (a critter, UW2's wisp) or used
                        var dragged = UWTileMap.current_tilemap.LevelObjects[index];
                        var releaseable = pickup.CanBePickedUpOverrides(dragged.item_id) || (commonObjDat.CanBePickedUp(dragged.item_id) && dragged.IsStatic);
                        var mode = InteractionModes.ModePickup;
                        if (!releaseable)
                        {
                            mode = ((!dragged.IsStatic && dragged.majorclass == 1) || ((UWClass._RES == UWClass.GAME_UW2) && (dragged.item_id == 0x1CD)))
                                ? InteractionModes.ModeTalk
                                : InteractionModes.ModeUse;
                        }
                        InteractWithObjectCollider(index: index, LeftClick: LookDragButton == MouseButton.Left, mode: mode);
                    }
                }
            }
        }

        /// <summary>A pick buffer pixel that names an object (red 0, the object index in blue and green).</summary>
        static bool IsObjectPixel(Color pixel)
        {
            var index = (pixel.B8 << 8) | pixel.G8;
            return (pixel.R8 == 0) && (index > 0) && (index <= 1024);
        }

        public static void ClickOnViewPort(InputEventMouseButton eventMouseButton)
        {
            if (!InGame) { return; }
            if (playerdat.ParalyseTimer > 0) { return; }//UW2 mous_in_3d: nothing at all while paralysed, not even a drop (UW1 blocks the input elsewhere, to the same effect)
            //store these values for use in throw and spell casting events
            var screenPoint = Controls.ScreenPoint(eventMouseButton.Position);
            ViewPortMouseXPos = screenPoint.X;
            ViewPortMouseYPos = screenPoint.Y;
            bool LeftClick = (eventMouseButton.ButtonIndex == MouseButton.Left);
            //classic with no icon selected: the original's default mode, a click looks and a drag after it picks up
            //(mous_in_3d, player_3dlook)
            bool classicDefault = !Controls.Modern && InteractionMode == InteractionModes.ModeNone;
            //modern controls: right click looks, whatever the icon
            bool look = (Controls.Modern && !LeftClick) || (classicDefault && UsageMode == 0);
            if (uimanager.InteractionMode == InteractionModes.ModeAttack && !look)
            {
                return;//no more needs to be done.
            }

            //Debug.Print($"{eventMouseButton.Position.X},{eventMouseButton.Position.Y}");
            //Dictionary result = DoRayCast(eventMouseButton.Position, RayDistance, out Vector3 rayOrigin);
            var text = (Texture2D)uimanager.instance.uwsubviewport_objectinfo.GetTexture();
            //text.GetImage().SavePng("c:\\temp\\testobjectinfo.png");
            //var vi = new Vector2I((int)(ViewPortMouseXPos / text.GetWidth()), (int)(ViewPortMouseYPos / text.GetHeight()));
            var mouse = Controls.ViewportPoint();
            var pickImage = text.GetImage();
            var pixel = pickImage.GetPixel((int)mouse.X, (int)mouse.Y);//
            if ((UWClass._RES == UWClass.GAME_UW2) && !IsObjectPixel(pixel))
            {//check_around: a click on a wall, floor or nothing looks outward in a square spiral (radius 9 in the original's pixels) for an object
                var step = System.Math.Max(1, pickImage.GetWidth() / 0xD0);
                int x = (int)mouse.X, y = (int)mouse.Y, dx = 0, dy = 1;
                for (int r = 1; r < 10 && !IsObjectPixel(pixel); r++)
                {
                    for (int k = 0; k < r; k++)
                    {
                        x += dx * step;
                        y += dy * step;
                        if ((x >= 0) && (x < pickImage.GetWidth()) && (y >= 0) && (y < pickImage.GetHeight()) && IsObjectPixel(pickImage.GetPixel(x, y)))
                        {
                            pixel = pickImage.GetPixel(x, y);
                            break;
                        }
                    }
                    if (dx != 0) { dy = -dx; dx = 0; } else { dx = dy; dy = 0; }
                }
            }


            //do projectiles first.
            if (SpellCasting.currentSpell != null)
            {
                //try can cast the current spell if class 5
                if (SpellCasting.currentSpell.SpellMajorClass == 5)
                {
                    SpellCasting.CastMagicProjectile(playerdat.playerObject, SpellCasting.currentSpell.SpellMinorClass);
                    return;
                }
            }

            //then try and drop/throw
            if (playerdat.ObjectInHand != -1)
            {
                //something is held. try and drop or throw it
                var objToThrow = UWTileMap.current_tilemap.LevelObjects[playerdat.ObjectInHand];
                var hadMoonstone = pickup.HasOrIsMoonstone(objToThrow);
                if (pickup.DropObjectByPlayer(objToThrow, true))
                {
                    playerdat.ObjectInHand = -1;
                    instance.mousecursor.SetCursorToCursor();
                    pickup.DropSpecialCases(hadMoonstone);//primarily handle moonstones                    
                }
                return;
            }

            if (pixel.R8 == 0)
            {
                var pitemindex = (pixel.B8 << 8) | pixel.G8;
                if ((pitemindex > 0) && (pitemindex <= 1024))
                {                    
                    //clicked on a game object
                    var obj = UWTileMap.current_tilemap.LevelObjects[pitemindex];
                    var itemname = GameStrings.GetSimpleObjectNameUW(UWTileMap.current_tilemap.LevelObjects[pitemindex].item_id);
                    Debug.Print($"{pixel} , {pitemindex}, {itemname} Object position is x:{obj.xpos}, y:{obj.ypos}, z:{obj.zpos}, h:{obj.heading}");
                    if (SpellCasting.currentSpell == null)
                    {
                        var mode = InteractionMode;
                        if (look)
                        {
                            mode = InteractionModes.ModeLook;
                        }
                        else if (Controls.ContextClicks)
                        {
                            mode = UsageMode == 1 ? InteractionModes.ModeUse : Controls.ContextMode(obj);
                        }
                        else if (classicDefault)
                        {//using an object on another
                            mode = InteractionModes.ModeUse;
                        }
                        InteractWithObjectCollider(index: obj.index, LeftClick: LeftClick, mode: mode);
                        if (classicDefault && mode == InteractionModes.ModeLook && eventMouseButton.ButtonIndex == Controls.DragButton)
                        {//only the drag button's look turns into a drag
                            LookDragIndex = obj.index;
                            LookDragButton = eventMouseButton.ButtonIndex;
                            LookDragStart = eventMouseButton.Position;
                        }
                        if (mode == InteractionModes.ModePickup && Input.IsKeyPressed(Key.Shift) && Controls.Modern)
                        {
                            PutHeldObjectInBackpack();
                        }
                    }
                    else
                    {
                        SpellCasting.CastCurrentSpellOnRayCastTarget(
                                            index: pitemindex,
                                            objList: UWTileMap.current_tilemap.LevelObjects,
                                            WorldObject: true);
                    }
                }
                else
                {
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, GameStrings.str_you_see_nothing_));
                }
            }
            else
            {
                //clicked on a tile. r8 = tileface, g8 = tilex, b8 = tiley
                var tileX = pixel.G8;
                var tileY = pixel.B8;
                if (UWTileMap.ValidTile(tileX, tileY))
                {
                    var tile = UWTileMap.current_tilemap.Tiles[tileX, tileY];
                    if (InteractionMode == InteractionModes.ModeLook || look)
                    {
                        LookAtTile(pixel.R8, tileX, tileY);//there needs to be a distance check to see if the player sees something or nothing. I think it should be based on the shading.
                    }
                    else if (InteractionMode == InteractionModes.ModeUse)
                    {
                        uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, GameStrings.str_you_cannot_use_that_));
                    }
                    else if ((InteractionMode == InteractionModes.ModePickup) || (InteractionMode == InteractionModes.ModeTalk))
                    {//look_nothing: 'Nothing to get.', 'You cannot talk to that.' (how 3 and 4 after 'You cannot use that.')
                        var nothing = UWClass._RES == UWClass.GAME_UW2 ? 0xA6 : 0x98;
                        uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, nothing + (InteractionMode == InteractionModes.ModePickup ? 3 : 4)));
                    }
                }
            }

            // return;
            // if (result != null)
            // {
            //     if (result.ContainsKey("collider") && result.ContainsKey("normal") && result.ContainsKey("position"))
            //     {
            //         var obj = (StaticBody3D)result["collider"];
            //         var normal = (Vector3)result["normal"];
            //         var hitCoordinateEnd = (Vector3)result["position"];
            //         var hitCoordinate = rayOrigin.Lerp(hitCoordinateEnd, 0.9f);
            //         Debug.Print(obj.Name);
            //         string[] vals = obj.Name.ToString().Split("_");

            //         switch (vals[0].ToUpper())
            //         {
            //             case "TILE":
            //             case "WALL":
            //                 {
            //                     if (SpellCasting.currentSpell != null)
            //                     {
            //                         if (SpellCasting.currentSpell.SpellMajorClass == 5)
            //                         {
            //                             SpellCasting.CastCurrentSpellOnRayCastTarget(
            //                                 index: 0,
            //                                 objList: null,
            //                                 WorldObject: true);//not enough room to cast
            //                             return;
            //                         }
            //                     }
            //                     switch (InteractionMode)
            //                     {
            //                         case InteractionModes.ModePickup:
            //                             {
            //                                 //interaction removed from here to support new drop/throw methods.
            //                                 break;
            //                             }
            //                         case InteractionModes.ModeLook:
            //                             {//Look at tile
            //                                 //int tileX = int.Parse(vals[1]); int tileY = int.Parse(vals[2]);
            //                                 //LookAtTile(normal, tileX, tileY);
            //                                 break;
            //                             }
            //                     }
            //                     break;
            //                 }
            //             case "CEILING":
            //                 {
            //                     if (SpellCasting.currentSpell != null)
            //                     {
            //                         if (SpellCasting.currentSpell.SpellMajorClass == 5)
            //                         {
            //                             SpellCasting.CastCurrentSpellOnRayCastTarget(
            //                                 index: 0,
            //                                 objList: null,
            //                                 WorldObject: true);//not enough room to cast
            //                             return;
            //                         }
            //                     }
            //                     switch (InteractionMode)
            //                     {
            //                         case InteractionModes.ModeLook:
            //                             {
            //                                 LookAtCeiling();
            //                                 break;
            //                             }
            //                     }
            //                     break;
            //                 }

            //             default: //check for regular collider with an obj.
            //                 {
            //                     if (int.TryParse(vals[0], out int index))
            //                     {
            //                         if (SpellCasting.currentSpell == null)
            //                         {
            //                             if (!(InteractionMode == InteractionModes.ModePickup && playerdat.ObjectInHand != -1))//temp to allow drop/throw below to work
            //                             {
            //                                 InteractWithObjectCollider(
            //                                     index: index, LeftClick: LeftClick);
            //                                 return;
            //                             }

            //                         }
            //                         else
            //                         {
            //                             SpellCasting.CastCurrentSpellOnRayCastTarget(
            //                                 index: index,
            //                                 objList: UWTileMap.current_tilemap.LevelObjects,
            //                                 WorldObject: true);
            //                         }
            //                     }
            //                 }
            //                 break;
            //         }
            //     }
            //     else
            //     {
            //         //along the ray
            //         if (SpellCasting.currentSpell != null)
            //         {
            //             //try can cast the current spell if class 5
            //             if (SpellCasting.currentSpell.SpellMajorClass == 5)
            //             {
            //                 SpellCasting.CastMagicProjectile(playerdat.playerObject, SpellCasting.currentSpell.SpellMinorClass);
            //                 return;
            //             }
            //         }
            //         else
            //         {
            //             //no match on the raycast. 
            //             //nothing found.
            //             switch (InteractionMode)
            //             {
            //                 case InteractionModes.ModeLook:
            //                     uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, GameStrings.str_you_see_nothing_));
            //                     break;
            //             }
            //         }
            //     }
            // }
            // else
            // {

            // }


            // //New methods that ignore the raycast
            // switch (InteractionMode)
            // {
            //     case InteractionModes.ModePickup:
            //         {
            //             if (playerdat.ObjectInHand != -1)
            //             {
            //                 //something is held. try and drop or throw it
            //                 var objToThrow = UWTileMap.current_tilemap.LevelObjects[playerdat.ObjectInHand];
            //                 var itemid = objToThrow.item_id;
            //                 //Remove weight from player
            //                 var ObjectMass = container.GetTotalMass(objToThrow, UWTileMap.current_tilemap.LevelObjects, false);
            //                 if (pickup.DropObjectByPlayer(objToThrow, true))
            //                 {
            //                     playerdat.ObjectInHand = -1;
            //                     instance.mousecursor.SetCursorToCursor();
            //                     pickup.DropSpecialCases(itemid);//primarily handle moonstones
            //                 }
            //             }
            //             break;
            //         }
            // }
        }

        /// <summary>
        /// DOS's colour for the encumbrance figure, into the gameplay palette.
        ///
        /// UW1 writes 0xE0 through its font colour global in UpdateWeightDisplay_ovr121_1827
        /// (UW.EXE 0x7B7FA, C4 1E D4 23 26 C6 07 E0). UW2 writes 0x88 in
        /// UpdateWeightCapacityOnScreen_ovr125_1935 (UW2.EXE 0x90518). The label had no
        /// colour at all and fell back to Godot's white. See issue #82.
        /// </summary>
        private static byte WeightCapacityColourIndex =>
            UWClass._RES == UWClass.GAME_UW2 ? (byte)0x88 : (byte)0xE0;

        public static void RefreshWeightDisplay()
        {
            // Called from playerdat inventory paths that also run headlessly.
            if (uimanager.instance == null) { return; }
            uimanager.instance.WeightCapacity.Text = (playerdat.WeightCapacity / 0xA).ToString();
            uimanager.instance.WeightCapacity.Set(
                "theme_override_colors/font_color",
                PaletteLoader.Palettes[0].ColorAtIndex(WeightCapacityColourIndex, false, false));
            Debug.Print($"player weight capacity is now {playerdat.WeightCapacity}");
        }


        /// <summary>
        /// Drops an object at the clicked on tile position.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="tileX"></param>
        /// <param name="tileY"></param>
        // private static void DropToTileAtPosition_OLD(Vector3 pos, int tileX, int tileY)
        // {
        //     Debug.Print("To Remove DropToTileAtPosition_OLD()");
        //     if (
        //         pickup.Drop_old(
        //         index: playerdat.ObjectInHand,
        //         objList: UWTileMap.current_tilemap.LevelObjects,
        //         dropPosition: pos,
        //         tileX: tileX, tileY: tileY)
        //     )
        //     {
        //         playerdat.ObjectInHand = -1;
        //         instance.mousecursor.SetCursorToCursor();
        //     }
        // }


        /// <summary>
        /// Handles looking at the ceiling
        /// </summary>
        private static void LookAtCeiling()
        {
            AddToMessageScroll(GameStrings.TextureDescription(511));
        }


        /// <summary>
        /// Handles looking at a tile on a surface determined from the surface normal;
        /// </summary>
        /// <param name="normal"></param>
        /// <param name="tileX"></param>
        /// <param name="tileY"></param>
        private static void LookAtTile(int face, int tileX, int tileY)
        {
            var surface = TileInfo.GetTileSurfaceDescription(face, tileX, tileY);
            AddToMessageScroll(ZhText.Enabled
                ? ZhText.YouSee(surface)
                : GameStrings.GetString(1, GameStrings.str_you_see_) + surface);
        }

    }//end class
}//end namespace