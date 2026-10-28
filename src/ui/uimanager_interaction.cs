using System;
using System.Diagnostics;
using System.IO.Compression;
using System.Security;
using Godot;

namespace Underworld
{

    public partial class uimanager : Node2D
    {
        /// <summary>
        /// Identifies what mode of object usage is currently being used.
        /// 0 = default interactions
        /// 1 = Player has selected an object and it has changed to prompt to be used on something. Eg rockhammer, doorkey, oil flask
        /// 2 = Player is casting a spell and the spell needs to be clicked on an object.
        /// </summary>
        public static int UsageMode
        {
            get
            {
                if (useon.CurrentItemBeingUsed != null)
                {
                    return 1;
                }
                if (SpellCasting.currentSpell != null)
                {
                    return 2;
                }
                return 0;
            }
        }

        public enum InteractionModes
        {
            ModeNone = -1,
            ModeOptions = 0,
            ModeTalk = 1,
            ModePickup = 2,
            ModeLook = 3,
            ModeAttack = 4,
            ModeUse = 5
        };

        public static InteractionModes InteractionMode = InteractionModes.ModeNone;
        [ExportGroup("InteractionModes")]
        //Array to store the interaction mode mo
        [Export] public Godot.TextureRect[] InteractionButtonsUW1 = new Godot.TextureRect[6];
        [Export] public Godot.TextureRect[] InteractionButtonsUW2 = new Godot.TextureRect[6];

        public ImageTexture[] UW2InteractionBtnsOff;
        public ImageTexture[] UW2InteractionButtonsOn;

        private void InitInteraction()
        {

            if (UWClass._RES == UWClass.GAME_UW2)
            {//load interaction button art for uw2. the images need to be cropped out of a master image.

                UW2InteractionBtnsOff = new ImageTexture[6];
                UW2InteractionButtonsOn = new ImageTexture[6];

                //CropTexture, as the rects are in original pixels and HD art is several times larger
                var Off = grOptBtns.LoadImageAt(0);
                var On = grOptBtns.LoadImageAt(1);
                UW2InteractionBtnsOff[4] = ArtLoader.CropTexture(Off, new Rect2I(0, 0, 25, 14)); //attack button off
                UW2InteractionButtonsOn[4] = ArtLoader.CropTexture(On, new Rect2I(0, 0, 25, 14)); //attack button on

                UW2InteractionBtnsOff[5] = ArtLoader.CropTexture(Off, new Rect2I(26, 0, 25, 14)); //use button off
                UW2InteractionButtonsOn[5] = ArtLoader.CropTexture(On, new Rect2I(26, 0, 25, 14)); //use button on

                UW2InteractionBtnsOff[2] = ArtLoader.CropTexture(Off, new Rect2I(52, 0, 25, 14)); //pickup button off
                UW2InteractionButtonsOn[2] = ArtLoader.CropTexture(On, new Rect2I(52, 0, 25, 14)); //pickup button on

                UW2InteractionBtnsOff[1] = ArtLoader.CropTexture(Off, new Rect2I(0, 15, 25, 14)); //talk button off
                UW2InteractionButtonsOn[1] = ArtLoader.CropTexture(On, new Rect2I(0, 15, 25, 14)); //talk button on

                UW2InteractionBtnsOff[3] = ArtLoader.CropTexture(Off, new Rect2I(26, 15, 25, 14)); //look button off
                UW2InteractionButtonsOn[3] = ArtLoader.CropTexture(On, new Rect2I(26, 15, 25, 14)); //look button on

                UW2InteractionBtnsOff[0] = ArtLoader.CropTexture(Off, new Rect2I(52, 15, 25, 14)); //options button off
                UW2InteractionButtonsOn[0] = ArtLoader.CropTexture(On, new Rect2I(52, 15, 25, 14)); //option button on
            }


            switch (UWClass._RES)
            {
                case UWClass.GAME_UW2:
                    for (int i = 0; i <= InteractionButtonsUW2.GetUpperBound(0); i++)
                    {
                        if (i != (int)InteractionMode)
                        {
                            InteractionButtonsUW2[i].Texture = UW2InteractionBtnsOff[i];
                        }
                        else
                        {
                            InteractionButtonsUW2[i].Texture = UW2InteractionButtonsOn[i];
                        }
                    }
                    break;
                default:
                    for (int i = 0; i <= InteractionButtonsUW1.GetUpperBound(0); i++)
                    {
                        if (i != (int)InteractionMode)
                        {
                            InteractionButtonsUW1[i].Texture = grLfti.LoadImageAt(i * 2, false); //off button
                        }
                        else
                        {
                            InteractionButtonsUW1[i].Texture = grLfti.LoadImageAt(i * 2 + 1, false);//on button
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Handles interacting with objects in the world
        /// </summary>
        /// <param name="index"></param>
        /// <param name="mode">The interaction to do, when not the selected icon's (modern controls' context clicks).</param>
        public static void InteractWithObjectCollider(int index, bool LeftClick, InteractionModes? mode = null)
        {
            if (playerdat.ParalyseTimer > 0)
            {
                return;//stop interaction while player is paralysed
            }
            switch (mode ?? InteractionMode)
            {
                case InteractionModes.ModeTalk:
                    talk.Talk(
                        ConversationNPC: UWTileMap.current_tilemap.LevelObjects[index],
                        WorldObject: true);
                    break;
                case InteractionModes.ModeLook:
                    {//player_3dlook: lore 1 for an object in close reach (0x48) that cannot be picked up, else 0
                        var looked = UWTileMap.current_tilemap.LevelObjects[index];
                        var releaseable = pickup.CanBePickedUpOverrides(looked.item_id) || (commonObjDat.CanBePickedUp(looked.item_id) && looked.IsStatic);
                        look.LookAtWorld(
                            index: index,
                            lore: CanReach(looked, 0x48) && !releaseable ? 1 : 0,
                            lookMode: (InteractionMode == InteractionModes.ModeLook) || Controls.Modern);
                    }
                    break;
                case InteractionModes.ModeUse:
                    //do a use interaction with the object.
                    if (CanReach(UWTileMap.current_tilemap.LevelObjects[index], playerdat.UseDistance)
                        && !BlockingTerrain(playerdat.UseDistance, UWTileMap.current_tilemap.LevelObjects[index]))
                    {
                        if (useon.CurrentItemBeingUsed != null)
                        {
                            //handle using an object on another.       
                            useon.UseOn(
                                ObjectUsed: UWTileMap.current_tilemap.LevelObjects[index],
                                srcObject: useon.CurrentItemBeingUsed,
                                WorldObject: true);
                        }
                        else
                        {
                            use.Use(
                                ObjectUsed: UWTileMap.current_tilemap.LevelObjects[index],
                                UsingObjectOrCharacter: playerdat.playerObject,
                                objList: UWTileMap.current_tilemap.LevelObjects,
                                WorldObject: true);
                        }
                    }
                    else
                    {
                        uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, GameStrings.str_you_are_unable_to_use_that_from_here_));
                    }
                    break;
                case InteractionModes.ModePickup:
                    if (playerdat.ObjectInHand == -1)//don't do pickup if holding something already.
                    {//player_3dget: in reach and not blocked by the terrain
                        var picked = UWTileMap.current_tilemap.LevelObjects[index];
                        var inrange = CanReach(picked, playerdat.PickupDistance);
                        var clear = !BlockingTerrain(playerdat.PickupDistance, picked);
                        var releaseable = pickup.CanBePickedUpOverrides(picked.item_id) || (commonObjDat.CanBePickedUp(picked.item_id) && picked.IsStatic);
                        if ((useon.CurrentItemBeingUsed != null) || (releaseable && inrange && clear))
                        {
                            if (inrange && clear)
                            {
                                pickup.PickUp(
                                    index: index,
                                    objList: UWTileMap.current_tilemap.LevelObjects,
                                    WorldObject: true);
                            }
                            else
                            {//You cannot reach that.
                                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, UWClass._RES == UWClass.GAME_UW2 ? 0x6B : 0x5E));
                            }
                        }
                        else if (releaseable)
                        {//That is too far away to take. / You cannot reach that.
                            uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, (UWClass._RES == UWClass.GAME_UW2 ? 0x6A : 0x5D) + (inrange ? 1 : 0)));
                        }
                        else if ((UWClass._RES == UWClass.GAME_UW2) && (picked.item_id == 0x1CA))
                        {//frost is used when in reach
                            if (inrange && clear)
                            {
                                use.Use(picked, playerdat.playerObject, UWTileMap.current_tilemap.LevelObjects, true);
                            }
                        }
                        else
                        {//You cannot pick that up.
                            uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, UWClass._RES == UWClass.GAME_UW2 ? 0x6D : 0x60));
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Modern controls' shift + click: the object just picked up goes from the hand into the first
        /// free slot of the backpack (or of the container open in it), as if dropped there. Stays in the
        /// hand when there is none.
        /// </summary>
        static void PutHeldObjectInBackpack()
        {
            if (playerdat.ObjectInHand == -1)
            {
                return;//nothing picked up, or a quantity is being asked for
            }
            for (int slot = 11; slot <= 18; slot++)
            {
                if (BackPackIndices[slot - 11] <= 0)//-1, or 0 before the backpack is first drawn: no object has index 0
                {
                    var previous = CurrentSlot;
                    CurrentSlot = slot;
                    PickupToEmptySlot(playerdat.ObjectInHand);
                    CurrentSlot = previous;
                    return;
                }
            }
        }

        /// <summary>
        /// BlockingTerrain: whether the terrain stops the player reaching obj: too low or high for him, a tile on the
        /// straight line to it lower than he stands (normal reach only), or the line crossing another terrain class than
        /// the one he stands on (water, lava), unless a bridge carries it. A distance of 0 is never blocked.
        /// </summary>
        static bool BlockingTerrain(int dist, uwObject obj)
        {
            if (dist == 0)
            {
                return false;
            }
            var px = playerdat.playerObject.tileX; var py = playerdat.playerObject.tileY;
            var ox = obj.tileX; var oy = obj.tileY;
            if ((px == ox) && (py == oy))
            {
                return false;
            }
            var terr = TileTerrainClass(UWTileMap.current_tilemap.Tiles[px, py]);
            var xdir = System.Math.Sign(ox - px);
            var ydir = System.Math.Sign(oy - py);
            var pole = playerdat.usingpole ? 1 : 0;
            var reach = (pole + 1) << 3;
            var high = (pole + 1) << 5;
            int pz = playerdat.playerObject.zpos;
            pz = pz > reach ? pz - reach : 0;
            var oz = obj.zpos;
            if ((oz < pz) || (pz + high < oz))
            {
                return true;
            }
            var x = px; var y = py;
            while (true)
            {
                x += xdir;
                if (((xdir < 0) && (x < ox)) || ((xdir > 0) && (x > ox))) { x = ox; }
                y += ydir;
                if (((ydir < 0) && (y < oy)) || ((ydir > 0) && (y > oy))) { y = oy; }
                var tile = UWTileMap.current_tilemap.Tiles[x, y];
                //BridgeHeight: the highest bridge on the tile
                var bridge = -1;
                var next = tile.indexObjectList;
                while (next != 0)
                {
                    var o = UWTileMap.current_tilemap.LevelObjects[next];
                    if ((o.item_id == 0x164) && (o.zpos > bridge))
                    {
                        bridge = o.zpos;
                    }
                    next = o.next;
                }
                var found = (bridge >= 0) && (bridge <= oz) && (bridge >= pz);
                if (!found && ((pz >> 3) > tile.floorHeight) && (dist == 0x90))
                {
                    return true;
                }
                if ((x == ox) && (y == oy))
                {
                    break;
                }
                var t = TileTerrainClass(tile);
                if ((dist <= 0x90) && !found && (t != 0) && (t != terr))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The floor's terrain (TERRAIN): UW2 its class (bits 6-7), UW1 the whole terrain value.
        /// </summary>
        static int TileTerrainClass(TileInfo tile)
        {
            if (UWClass._RES == UWClass.GAME_UW2)
            {
                return TerrainDatLoader.GetTerrainTypeNo(tile);
            }
            var floorterrain = tileMapRender.FloorTexture_MapIndex(tile);
            return TerrainDatLoader.Terrain[46 + UWTileMap.current_tilemap.texture_map[floorterrain]];
        }

        static bool CanReach(uwObject obj, int UseDistance)
        {
            if (UseDistance == 0)
            {
                return true;
            }
            else
            {
                var xCoord = Math.Abs((obj.tileX << 3) + obj.xpos) - ((playerdat.playerObject.tileX << 3) + playerdat.playerObject.xpos);
                var yCoord = Math.Abs((obj.tileY << 3) + obj.ypos) - ((playerdat.playerObject.tileY << 3) + playerdat.playerObject.ypos);

                if ((yCoord * yCoord) + (xCoord * xCoord) > UseDistance)
                {
                    return false;
                }
                else
                {
                    var zDiff = playerdat.playerObject.zpos - obj.zpos;
                    if ((UWClass._RES == UWClass.GAME_UW2) && (playerdat.SwimCounter > 0x50))
                    {//UW2 only: swimming lowers the reach
                        zDiff -= (playerdat.SwimCounter >> 3);
                    }
                    int pole = 0;
                    if (playerdat.usingpole)
                    {
                        pole = 1;
                    }
                    pole++;

                    if ((pole * 0xC) >= zDiff)
                    {
                        if ((pole * -24) <= zDiff)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

            }
            return false;
        }

        public static void InteractionModeToggle(InteractionModes index)
        {
            PreviousInteractionMode = InteractionMode;
            ToggleInteractionButtonDisplay(index);
            UpdateModeCursor();

            ///special handling for some modes
            switch (index)
            {
                case InteractionModes.ModeOptions:
                    {
                        if (UWClass._RES == UWClass.GAME_UW2)
                        {

                        }
                        else
                        {
                            InteractionModeShowHide(false);//hide the interaction buttons.  
                        }
                        //turn off mouselook (obsolete?)
                        Input.MouseMode = Input.MouseModeEnum.Hidden;
                        ReturnToTopOptionsMenu();
                        break;
                    }
                case InteractionModes.ModeAttack:
                    {//deal_with_icons: the weapon is drawn, but not while swimming, nor in UW2 asleep in the void or paralysed
                        if (((playerdat.TileState & 1) != 0)
                            || ((UWClass._RES == UWClass.GAME_UW2) && (playerdat.DreamingInVoid || playerdat.ParalyseTimer > 0)))
                        {
                            EnterInteractionModeNone();
                        }
                        else if (playerdat.play_drawn != 1)
                        {
                            ToggleWeaponAnimationState(true);
                        }
                        break;
                    }
                default:
                    //ensure weapon is not drawn.
                    if (playerdat.play_drawn == 1)
                    {
                        ToggleWeaponAnimationState(false);
                    }

                    //XMIMusic.PickLevelThemeMusic(); //in future this needs to take into account combat state.
                    //
                    break;

            }
        }

        /// <summary>
        /// toggle_fightmode, a click on the empty weapon hand: sheathes (punt_fightmode) or draws (pick_fightmode, refused
        /// only while swimming).
        /// </summary>
        public static void ToggleFightModeFromWeaponHand()
        {
            if (playerdat.play_drawn == 1)
            {
                PutAwayWeapon();
                return;
            }
            if ((playerdat.TileState & 1) != 0)
            {
                return;
            }
            PreviousInteractionMode = InteractionMode;
            ToggleInteractionButtonDisplay(InteractionModes.ModeAttack);
            UpdateModeCursor();
            ToggleWeaponAnimationState(true);
        }

        /// <summary>
        /// DOS "no mode": no interaction button selected. Inventory uses L=Use, R=Look.
        /// </summary>
        public static void EnterInteractionModeNone()
        {
            PreviousInteractionMode = InteractionMode;
            InteractionMode = InteractionModes.ModeNone;
            SetAllInteractionButtonsOff();
            UpdateModeCursor();
            if (playerdat.play_drawn == 1)
            {
                ToggleWeaponAnimationState(false);
            }
        }

        /// <summary>The pointer follows the selected icon, unless an object or a spell is on the cursor.</summary>
        static void UpdateModeCursor()
        {
            if ((playerdat.ObjectInHand == -1) && (UsageMode == 0))
            {
                instance.mousecursor.SetCursorToCursor();
            }
        }

        static void SetAllInteractionButtonsOff()
        {
            if (UWClass._RES == UWClass.GAME_UW2)
            {
                for (int i = 0; i <= instance.InteractionButtonsUW2.GetUpperBound(0); i++)
                {
                    instance.InteractionButtonsUW2[i].Texture = instance.UW2InteractionBtnsOff[i];
                }
            }
            else
            {
                for (int i = 0; i <= instance.InteractionButtonsUW1.GetUpperBound(0); i++)
                {
                    instance.InteractionButtonsUW1[i].Texture = grLfti.LoadImageAt(i * 2, false);
                }
            }
        }

        /// <summary>
        /// Sets the on button for the interaction buttons to the specified index.
        /// </summary>
        /// <param name="index"></param>
        private static void ToggleInteractionButtonDisplay(InteractionModes index)
        {
            if (index == InteractionModes.ModeNone)
            {
                EnterInteractionModeNone();
                return;
            }

            if (UWClass._RES == UWClass.GAME_UW2)
            {
                for (int i = 0; i <= instance.InteractionButtonsUW2.GetUpperBound(0); i++)
                {
                    if (i == (int)(index))
                    {
                        InteractionMode = index;
                        instance.InteractionButtonsUW2[i].Texture = instance.UW2InteractionButtonsOn[i];
                    }
                    else
                    {
                        instance.InteractionButtonsUW2[i].Texture = instance.UW2InteractionBtnsOff[i];
                    }
                }
            }
            else
            {
                for (int i = 0; i <= instance.InteractionButtonsUW1.GetUpperBound(0); i++)
                {
                    if (i == (int)(index))
                    {
                        InteractionMode = index;
                        instance.InteractionButtonsUW1[i].Texture = grLfti.LoadImageAt(i * 2 + 1, false);//on button
                    }
                    else
                    {
                        instance.InteractionButtonsUW1[i].Texture = grLfti.LoadImageAt(i * 2, false);//off button
                    }
                }
            }
        }


        public static void ToggleWeaponAnimationState(bool drawWeapon, bool updateThemes = true)
        {
            //PreviousWeaponAnimation = -1; //force redraw.
            //combat.stage = combat.CombatStages.OutOfCombat;
            //the armed music, unless a combat theme (foe hurt, combat, danger) is playing; back to walking music
            //only from the armed one (deal_with_icons)
            var theme = XMIMusic.CurrentlyPlayingThemeNo;
            var combatTheme = UWClass._RES == UWClass.GAME_UW2 ? (theme >= 2 && theme <= 4) : (theme >= 5 && theme <= 7);
            if (drawWeapon)     //(playerdat.play_drawn != 1)
            {
                playerdat.play_drawn = 1;//draw the weapon 
                if (updateThemes && !combatTheme)
                {
                    XMIMusic.ChangeThemeMusic(XMIMusic.Armed);
                }
            }
            else
            {
                playerdat.play_drawn = 0; //ensure weapon is not drawn.
                if (updateThemes && theme == XMIMusic.Armed)
                {
                    XMIMusic.PickLevelThemeMusic(0);
                }
                if (UWClass._RES == UWClass.GAME_UW2)
                {
                    instance.InteractionButtonsUW2[(int)(InteractionModes.ModeAttack)].Texture = instance.UW2InteractionBtnsOff[(int)(InteractionModes.ModeAttack)];
                }
                else
                {
                    instance.InteractionButtonsUW1[(int)(InteractionModes.ModeAttack)].Texture = grLfti.LoadImageAt((int)(InteractionModes.ModeAttack) * 2, false);
                }
            }

            var obj = playerdat.PrimaryHandObject;
            switch (combat.isWeapon(obj))
            {
                case 1://melee weapon
                case 2: //rangedweapon
                    combat.currentweapon = obj; break;
                default:
                    combat.currentweapon = null; break;
            }
        }

        /// <summary>
        /// Puts the weapon away from the paperdoll (punt_fightmode): no icon selected (the default mode, or context clicks
        /// under modern controls) and the walking music.
        /// </summary>
        public static void PutAwayWeapon()
        {
            ToggleWeaponAnimationState(false, updateThemes: false);
            XMIMusic.PickLevelThemeMusic(0);
            EnterInteractionModeNone();
        }

        public static void InteractionModeShowHide(bool state)
        {
            if (UWClass._RES == UWClass.GAME_UW2)
            {

                for (int i = 0; i <= instance.InteractionButtonsUW2.GetUpperBound(0); i++)
                {
                    EnableDisable(instance.InteractionButtonsUW2[i], state);
                }
            }
            else
            {
                for (int i = 0; i <= instance.InteractionButtonsUW1.GetUpperBound(0); i++)
                {
                    EnableDisable(instance.InteractionButtonsUW1[i], state);
                }
            }
        }


        /// <summary>
        /// User clicked a mode button / pressed F2-F6. Re-clicking the active mode
        /// (except Options) enters no-mode. Attack also sheaths if drawn.
        /// </summary>
        public static void InteractionModeButtonPressed(InteractionModes index)
        {
            if (index == InteractionMode
                && index != InteractionModes.ModeOptions
                && index != InteractionModes.ModeNone)
            {
                if (index == InteractionModes.ModeAttack && playerdat.play_drawn == 1)
                {
                    ToggleWeaponAnimationState(false);
                }
                EnterInteractionModeNone();
                return;
            }
            InteractionModeToggle(index);
        }

        private void _on_interactionoptions_button(InputEvent @event, long extra_arg_0)
        {
            if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed && eventMouseButton.ButtonIndex == MouseButton.Left)
            {
                if (playerdat.play_hp == 0)
                {
                    //prevent player from entering menu or changing modes when dead...
                    return;
                }

                if (!uimanager.blockinput)
                {
                    if (playerdat.ObjectInHand != -1)
                    {
                        return;
                    }
                    if (UsageMode != 0)
                    {
                        return;
                    }
                    InteractionModeButtonPressed((InteractionModes)extra_arg_0);
                }
            }
        }


    }//end class    
}//end namespace