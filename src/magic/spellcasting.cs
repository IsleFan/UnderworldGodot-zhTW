using System.Diagnostics;

namespace Underworld
{

    public  partial class SpellCasting : UWClass
    {
        /// <summary>
        /// The current spell to cast after a delay.
        /// </summary>
        public static RunicMagic currentSpell;

        /// <summary>
        /// Logic for casting spells that are not from enchanted objects. Assumes all spell casting requirements have been met. 
        /// </summary>
        /// <param name="majorclass"></param>
        /// <param name="minorclass"></param>
        /// <param name="caster">Casting object (use if not the player casting)</param>
        /// <param name="target"></param>
        /// <returns></returns>
        static int PendingUW1CreatureSpell;

        /// <summary>
        /// One of UW1's five creature spells on a critter in the area (area1_spells): only Smite Undead can fail to act.
        /// </summary>
        static bool UW1CreatureSpellOnTarget(int x, int y, uwObject obj, TileInfo tile, int srcIndex)
        {
            if (obj == null)
            {
                return false;
            }
            if (PendingUW1CreatureSpell == 2)
            {
                return SmiteUndead(obj.index, UWTileMap.current_tilemap.LevelObjects, playerdat.playerObject);
            }
            CastClass7_SpellsOnCallBack(PendingUW1CreatureSpell, obj.index, UWTileMap.current_tilemap.LevelObjects, playerdat.playerObject);
            return true;
        }

        /// <returns>false when the spell is refused (do_spell): no magic here, or no room for another active spell</returns>
        public static bool CastSpell(int majorclass, int minorclass, uwObject caster, uwObject target, int tileX, int tileY, bool CastOnEquip)
        {
            bool PlayerCast = caster == playerdat.playerObject;
            Debug.Print($"{caster.a_name} is casting {majorclass},{minorclass}");
            if (_RES==GAME_UW1)
            {
                if (playerdat.dungeon_level==9)
                {
                    return false; //no casting in the ethereal void.
                }
            }
            //if no magic allow bit return
            if (UWTileMap.ValidTile(tileX, tileY))
            {
                var tile = UWTileMap.current_tilemap.Tiles[tileX, tileY];
                if (tile.noMagic == 1)
                {
                    return false;
                }
            }

            switch (majorclass)
            {
                //0-3 affect active player statuses
                //In this case use minorclass & 0xC0 as the spell stability class and minorclass & 0x3F as the minor class within the function.
                case 0://
                case 1://motion related
                case 2://
                case 3://
                    {
                        if (PlayerCast && (playerdat.ActiveSpellEffectCount >= 3))
                        {//no room for another active spell: set_curmagic fails and do_spell refuses
                            return false;
                        }
                        CastClass0123_Spells(caster: caster, majorclass: majorclass, minorclass: minorclass);                        
                        break;
                    }
                case 4://healing
                    CastClass4_Heal(minorclass, caster, target);
                    break;
                case 5://projectile spells
                    {
                        if (PlayerCast)
                        {
                            currentSpell = new RunicMagic(majorclass,minorclass);
                            uimanager.instance.mousecursor.SetCursorToCursor(9);
                        }
                        else
                        {//cast by an object, eg spell trap. fire off immediately
                            CastMagicProjectile(caster, minorclass);
                        }
                    }
                    break;
                case 6://spells that run code in an area around the player
                    CastClass6_SpellsAroundPlayer(minorclass, caster);
                    break;
                case 7://spells with prompts and code callbacks.
                    {
                        if (_RES != GAME_UW2)
                        {//UW1 casts its creature spells at once on the first critter within 2 squares of a point 4 ahead (nail_1area)
                            if (PlayerCast)
                            {
                                PendingUW1CreatureSpell = minorclass & 0x3F;
                                CallBacks.RunCodeOnTargetsAroundObject(UW1CreatureSpellOnTarget, caster.index, 1, minorclass & 0xC0, 4, 2);
                            }
                            break;
                        }
                        currentSpell = new RunicMagic(majorclass,minorclass);
                        uimanager.instance.mousecursor.SetCursorToCursor(10);
                    }
                    break;
                case 8://spells that create or summon
                    CastClass8_Summoning(
                        minorclass: minorclass, 
                        caster: caster);
                    break;
                case 9://curses
                    CastClass9_Curse(
                        minorclass: minorclass,
                        CastOnEquip: CastOnEquip);
                    break;
                case 10://mana change spells
                    CastClass10_ManaBoost(minorclass: minorclass);
                    break;
                case 11://misc or special spells
                    CastClassB_Spells(caster: caster, minorclass: minorclass, stability: minorclass & 0xC0);                    
                    break;
                case 12://not castable here
                    Debug.Print("Attempt to directly cast Class 0xC spell. This should not happen");
                    break;
                case 13://misc spells
                    CastClassD_Spells(minorclass: minorclass, caster);
                    break;
                case 14://cutscene spells.
                    cutsplayer.PlayCutscene(CutsceneNo: minorclass, callBackMethod: null);
                    break;
            }

            playerdat.PlayerStatusUpdate();
            return true;
        } 
        
        /// <summary>
        /// Casts the pre-stored spell on the currently clicked object
        /// </summary>
        /// <param name="index"></param>
        /// <param name="objList"></param>
        /// <param name="WorldObject"></param>
        public static void CastCurrentSpellOnRayCastTarget(int index, uwObject[] objList, bool WorldObject)
        {
            if (currentSpell != null)
            {
                switch (currentSpell.SpellMajorClass)
                {
                    case 5://projectiles
                        //Casting on ray cast target should stop the spell from launching
                        uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1,GameStrings.str_there_is_not_enough_room_to_release_that_spell_));
                        return;
                    case 7://click on object spells
                        //target_spells/obj_spells: the mana held back at the cast is paid only if the spell worked
                        if (CastClass7_SpellsOnCallBack(
                            minorclass: currentSpell.SpellMinorClass, 
                            index: index, 
                            objList: objList,
                            caster: playerdat.playerObject,                           
                            WorldObject: WorldObject))
                        {
                            playerdat.play_mana = System.Math.Max(0, playerdat.play_mana - RunicMagic.PendingSpellCost);
                            playerdat.PlayerStatusUpdate();
                        }
                        RunicMagic.PendingSpellCost = 0;
                        currentSpell = null;
                        uimanager.instance.mousecursor.SetCursorToCursor();
                        break;
                    case 0xB: 
                        if (_RES!=GAME_UW2)
                        {//special cases in UW1
                        CastClassB_SpellsOnCallBack(
                            minorclass: currentSpell.SpellMinorClass, 
                            index: index, 
                            objList: objList); 
                        }
                        currentSpell = null;
                        uimanager.instance.mousecursor.SetCursorToCursor();
                        break;
                }
            }
        }   

        public static void CastCurrentSpellAtPosition(Godot.Vector3 position) 
        {

        }

    }//end class
}//end namespace
