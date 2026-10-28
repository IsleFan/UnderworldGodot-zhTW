using System;

namespace Underworld
{
    //for handling loop updates for the player.
    public partial class playerdat : Loader
    {
        //public static bool PlayerInDeathMode;
        /// <summary>
        /// Handles player death.
        /// </summary>
        static void PlayerDeath()
        {
            // if (PlayerInDeathMode)
            // {
            //     return;
            // }
            // PlayerInDeathMode = true;
            playerdat.play_drawn = 0;
            UWsoundeffects.KillAllEffects();//player_is_dead silences the effects first
            PuntMouseObject();

            if (_RES == GAME_UW2)
            {
                PlayerDeathUW2();
            }
            else
            {
                //uw1
                PlayerDeathUW1();
            }
        }


        /// <summary>
        /// punt_mouse_obj: what is on the cursor is put down near the player (near_mob_put_at, range 6).
        /// </summary>
        public static void PuntMouseObject()
        {
            if (ObjectInHand == -1)
            {
                return;
            }
            var held = UWTileMap.current_tilemap.LevelObjects[ObjectInHand];
            ObjectInHand = -1;
            uimanager.instance.mousecursor.SetCursorToCursor();
            var placed = motion.PutAt(held, (playerObject.tileX << 3) + playerObject.xpos, (playerObject.tileY << 3) + playerObject.ypos, playerObject.zpos, 6, false);
            if (placed != null)
            {
                ObjectCreator.RenderObject(placed, UWTileMap.current_tilemap);
            }
        }

        /// <summary>
        /// if silver tree planted. do the tree animation. 
        /// respawn at tree level via a delegate function.        
        /// otherwise return to main menu and Start death animation.
        /// </summary>
        private static void PlayerDeathUW1()
        {
            LeaveBones();
            if (SilverTreeDungeon != 0)
            {
                //resurrect at silver tree
                uimanager.EnableDisable(uimanager.instance.uwviewport,false); 
                cutsplayer.PlayCutscene(CutsceneNo: 0x102, callBackMethod: ResurrectAtSilverTree, useSingleRedChannel: true);
            }
            else
            {
                //die fully with cutscene.
                uimanager.EnableDisable(uimanager.instance.uwviewport,false); 
                cutsplayer.PlayCutscene(CutsceneNo: 0x103, callBackMethod: uimanager.ReturnToMainMenu, useSingleRedChannel: true);
            }
        }

        /// <summary>
        /// player_is_dead (UW1): a skull or bones (0xC2 to 0xC6) are left where the player fell, at the player's height
        /// and fine position, owned by 0x3F ("an adventurer"), and settled by obj_deal.
        /// </summary>
        static void LeaveBones()
        {
            var index = ObjectCreator.PrepareNewObject(Rng.r.Next(5) + 0xC2);
            if (index == 0)
            {
                return;
            }
            var bones = UWTileMap.current_tilemap.LevelObjects[index];
            var placed = motion.PutAt(bones, (playerObject.tileX << 3) + playerObject.xpos, (playerObject.tileY << 3) + playerObject.ypos, playerObject.zpos, 0, nocull: true);
            if (placed == null)
            {
                return;
            }
            placed.zpos = playerObject.zpos;
            placed.owner = 0x3F;
            placed.xpos = playerObject.xpos;
            placed.ypos = playerObject.ypos;
            var settled = motion.PlacedObjectCollision_seg030_2BB7_10BC(placed, playerObject.tileX, playerObject.tileY, 1);
            if (settled != null)
            {
                ObjectCreator.RenderObject(settled, UWTileMap.current_tilemap);
            }
        }

        public static void ResurrectAtSilverTree()
        {
            uimanager.EnableDisable(uimanager.instance.uwviewport,true); 
            ChangeExperience(-Exp >> 3);
            //player_is_dead: no fade (NewPlyFade 0), and do_resurrect runs once the tree's level is loaded
            Teleportation.NewPlyFade = 0;
            Teleportation.CodeToRunBeforeLanding = DoResurrect;
            Teleportation.CodeToRunOnTeleport = () => XMIMusic.ChangeThemeMusic(4);
            Teleportation.Teleport(
                character: 0,
                tileX: 32, tileY: 32,
                newLevel: SilverTreeDungeon,
                HeadingHeightFlag: 0);
        }

        /// <summary>
        /// do_resurrect (UW1): the player arrives at the silver tree, with the hit points and mana of a resurrection.
        /// </summary>
        static void DoResurrect()
        {
            Teleportation.MoveTo(7, 0, 0xA);
            ResurrectionEffects();
        }

        private static bool PlayerDeathUW2()
        {
            //If in Britannia.
            //Check if was killed by a castle inhabitant.get sent to jail. otherwise die.
            //else If in other dimension.
            //Check if dreaming in void. then so wake up where you were sleeping
            //If dueling in the pits then lose duel
            //respawn in gem room.
            if (dungeon_level == 1)
            {
                //playerObject.ProjectileSourceID = 235;  // for testing against LB
                //Check if was killed by an ally
                if (playerObject.ProjectileSourceID != 0)
                {
                    var killer = UWTileMap.current_tilemap.LevelObjects[playerObject.ProjectileSourceID];
                    if (killer != null)
                    {   //was avatar killed by a castle inhabitant or Syria or a castle guard.
                        if (
                            ((killer.npc_whoami >= 0x81) && (killer.npc_whoami < 0x8F))
                            ||
                            (killer.npc_whoami == 0xA8)
                            ||
                            (killer.npc_whoami == 0x95)
                            )
                        {
                            if (killer.UnkBit_0XA_Bit7 == 0)
                            {
                                //send to jail (put_player_in_jail: no resurrection's healing, see GoToJail)
                                Teleportation.CodeToRunOnTeleport = GoToJail;
                                Teleportation.Teleport(
                                    character: 0,
                                    tileX: 42, tileY: 38,
                                    newLevel: 1,
                                    HeadingHeightFlag: 0);
                                return false;
                            }
                        }
                    }
                }

            }

            if (DreamingInVoid)
            {
                //check if died while dreaming
                sleep.AwakenFromTheVoid();
                return false;
            }

            if (CurrentWorld != 0)
            {
                //respawn in the gem chamber                        
                if (IsFightingInPit)
                {
                    SetQuest(129, 0);//arena win record
                    SetQuest(133, 0);//jospur debt
                                     //clear fighters
                    for (int i = 0; i < 5; i++)
                    {
                        SetPitFighter(i, 0);//clear opponents.
                    }
                    IsFightingInPit = false;
                }
                //player_is_dead: no fade (NewPlyFade 0), and do_gem runs once level 5 is loaded
                Teleportation.NewPlyFade = 0;
                Teleportation.CodeToRunBeforeLanding = DoGem;
                Teleportation.CodeToRunOnTeleport = ResurrectAtBlackrockGem;
                Teleportation.Teleport(
                    character: 0,
                    tileX: 0x1C, tileY: 0x28,
                    newLevel: 5,
                    HeadingHeightFlag: 0);
            }
            else
            {
                //die fully with cutscene.
                uimanager.EnableDisable(uimanager.instance.uwviewport,false); 
                cutsplayer.PlayCutscene(0x103, uimanager.ReturnToMainMenu);
            }

            return true;
        }




        /// <summary>
        /// Avatar will respawn at the black rock if they die in another world
        /// </summary>
        static void ResurrectAtBlackrockGem()
        {//the hit points do_gem gave (2 to 4 short of the maximum) stay
            ChangeExperience(-Exp / 9);
            Teleportation.CodeToRunOnTeleport = null;
            XMIMusic.ChangeThemeMusic(0xA);
            //PlayerInDeathMode = false;
            uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x169));
        }

        /// <summary>
        /// do_gem (UW2): the player arrives near (0x1C, 0x28) of level 5, moved 8 squares a random way from it, with the
        /// hit points and mana of a resurrection, and a blood stain on the floor there when it is an open square.
        /// </summary>
        static void DoGem()
        {
            int x = 0x1C, y = 0x28;
            var xoff = Rng.r.Next(5) + 1;
            var yoff = Rng.r.Next(5) + 1;
            Teleportation.MoveAlong(Rng.r.Next(0x8000), 8, ref x, ref y);
            Teleportation.TargetX = x;
            Teleportation.TargetY = y;
            ResurrectionEffects();
            if (!UWTileMap.ValidTile(x, y))
            {
                return;
            }
            var tile = UWTileMap.current_tilemap.Tiles[x, y];
            if (tile.tileType == 1)
            {//put there exactly (put_at range 0); the original then sets its fine x from the y offset and its fine y from the x offset
                var bloodstainIndex = ObjectCreator.PrepareNewObject(0xDF);
                if (bloodstainIndex != 0)
                {
                    var bloodstain = UWTileMap.current_tilemap.LevelObjects[bloodstainIndex];
                    var z = tile.floorHeight << 3;
                    var placed = motion.PutAt(bloodstain, (x << 3) + xoff, (y << 3) + yoff, z, 0, false);
                    if (placed != null)
                    {
                        placed.zpos = (short)z;
                        placed.xpos = (short)yoff;
                        placed.ypos = (short)xoff;
                        var settled = motion.PlacedObjectCollision_seg030_2BB7_10BC(placed, x, y, 1);
                        if (settled != null)
                        {
                            ObjectCreator.RenderObject(settled, UWTileMap.current_tilemap);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Avatar goes to jail if killed while fighting the inhabitents of the castle.
        /// (if this happens before talking to LB the game will leave the avatar permanently in jail), 
        /// otherwise Lord British will teleport in to free the avatar
        /// </summary>
        static void GoToJail()
        {
            Teleportation.CodeToRunOnTeleport = null;
            play_hp = STR;//put_player_in_jail: the hit points become the strength
            ChangeExperience(-Exp / 9);//loss of exp
            //PlayerInDeathMode = false;
            playerdat.playerObject.npc_animation = 1;
            XMIMusic.ChangeThemeMusic(0xA);//play depressed theme
            
            //make humans friendly, set quests, run a trap, advance the clock and move LB to the prison to release you.
            uwObject.ResetMobileObjects();
            
            var newtime = (ClockValue / 0x3C00) % 20;
            newtime = Math.Abs(0x28 - newtime);
            newtime = newtime * 0x3C;
            newtime = newtime - Rng.r.Next(0x5A) + 0x3C;            
            AdvanceTime(newtime);
            
            CallBacks.RunCodeOnRace(npc.set_attitude_by_array, 0x1C, new int[] { 2 }, true);
            CallBacks.RunCodeOnRace(npc.set_goal_and_target_by_array, 0x1C, new int[] { 0, 1 }, true); // goal=1, gtarg=0
            
            SetQuest(112, 1);//avatar has been fighting.
            
            //uimanager.AddToMessageScroll(GameStrings.GetString(9, 4));//you awaken in jail message is printed by a text_string trap

            //Find LB and move him to outside the jail doorway.
            uwObject LBritish = null;
            for (int i = 1; i < 256; i++)
            {//find lord british
                LBritish = UWTileMap.current_tilemap.LevelObjects[i];
                if (LBritish.npc_whoami == 0x8E)
                {
                    break;
                }
                LBritish = null;
            }
            if (LBritish != null)
            {
                npc.moveNPCToTile(critter: LBritish, destTileX: 0x2A, destTileY: 0x22); //teleport_critter(lb, 0x2A, 0x22)
                LBritish.quality = 0x28;
                LBritish.owner = 0x27;
                LBritish.npc_goal = 1;
            }

            trigger.TriggerTrapInTile(39, 37);

            //put weapons away.
            playerdat.PutWeaponAway();

            //gruesome_door_hack(0x2A, 0x26): the cell's door is unlocked and opened
            Teleportation.GruesomeDoorHack(0x2A, 0x26);
        }



        /// <summary>
        /// Processes standard character changes when resurrected.
        /// </summary>
        static void ResurrectionEffects()
        {
            ResurrectionHPRestoration();
            ResurrectionManaRestoration();
            play_poison = 0;
            ActiveSpellEffectCount = 0;

            AutomapEnabled = true;
        }

        /// <summary>
        /// Restores an amount of HP on resurrection
        /// </summary>
        static void ResurrectionHPRestoration()
        {
            if (max_hp > 8)
            {
                play_hp = max_hp - 2 - Rng.r.Next(3);
            }
            else
            {
                play_hp = max_hp;
            }
        }

        static void ResurrectionManaRestoration()
        {
            if (max_mana > 8)
            {
                play_mana = max_mana - (2 + max_mana/8);
            }
            else
            {
                play_mana = max_mana;
            }
        }

    }//end class
}//end namespace