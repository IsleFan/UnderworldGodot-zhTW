using System.Diagnostics;
using System.Collections;
using Peaky.Coroutines;

namespace Underworld
{
    public class trapdisarming : UWClass
    {

        /// <summary>
        /// Checks for a linked damage trap-trigger
        /// </summary>
        /// <param name="index"></param>
        /// <param name="objList"></param>
        /// <param name="searchSkill"></param>
        /// <returns>Skill check result if a trigger linked to a trap is found</returns>
        public static playerdat.SkillCheckResult DetectTrapTrigger(int index, uwObject[] objList, int searchSkill)
        {//DetectedTrap
            var objToCheck = objList[index];
            if (FindTrap(objToCheck, objList, out _, out var foundtrap))
            {
                if (_RES == GAME_UW2)
                {//against 10 + twice the world number
                    return playerdat.SkillCheck(searchSkill, 0xA + (worlds.GetWorldNo(playerdat.dungeon_level) << 1), true);
                }
                if ((foundtrap.item_id & 0x3F) <= 2)
                {//UW1: only damage, teleport and arrow traps, against 8
                    return playerdat.SkillCheck(searchSkill, 8, true);
                }
            }
            return playerdat.SkillCheckResult.Fail;
        }

        /// <summary>
        /// The trap on an object: UW2's is the first minor 3 trigger among its contents and the trap it points at; UW1's is
        /// the first trap object among them (a trigger standing for the trap it points at).
        /// </summary>
        static bool FindTrap(uwObject objToCheck, uwObject[] objList, out uwObject foundtrigger, out uwObject foundtrap)
        {
            foundtrigger = null; foundtrap = null;
            if (objToCheck.is_quant != 0 || objToCheck.link == 0)
            {
                return false;
            }
            var next = objToCheck.link;
            while (next != 0)
            {
                var found = objectsearch.FindMatchInObjectChain(next, 6, -1, -1, objList, SkipLinks: true);
                if (found == null)
                {
                    return false;
                }
                if (_RES != GAME_UW2)
                {
                    if (found.minorclass >= 2)
                    {
                        foundtrigger = found;
                        foundtrap = found.link != 0 ? objList[found.link] : null;
                    }
                    else
                    {
                        foundtrap = found;
                    }
                    return foundtrap != null;
                }
                if (found.minorclass == 3)
                {
                    foundtrigger = found;
                    foundtrap = found.link != 0 ? objList[found.link] : null;
                    return foundtrap != null;
                }
                next = found.next;
            }
            return false;
        }

        /// <summary>
        /// Finds and Disarms a trap (RemoveTrap)
        /// </summary>
        /// <param name="index"></param>
        /// <param name="objList"></param>
        /// <param name="trapsSkill"></param>
        public static playerdat.SkillCheckResult DisarmTrap(int index, uwObject[] objList, int trapsSkill)
        {
            var objToCheck = objList[index];
            if (!FindTrap(objToCheck, objList, out var foundtrigger, out var foundtrap))
            {
                return playerdat.SkillCheckResult.Fail;
            }
            if ((_RES != GAME_UW2) && ((foundtrap.item_id & 0x3F) > 2))
            {
                return playerdat.SkillCheckResult.Fail;
            }
            return DoTrapDisarm(objList: objList, trapsSkill: trapsSkill, objToCheck: objToCheck, foundtrigger: foundtrigger, foundtrap: foundtrap);
        }


        /// <summary>
        /// Does the skill check and actions of disarming a trap.
        /// </summary>
        /// <param name="objList"></param>
        /// <param name="trapsSkill"></param>
        /// <param name="objToCheck"></param>
        /// <param name="foundtrigger"></param>
        /// <param name="foundtrap"></param>
        /// <returns></returns>
        private static playerdat.SkillCheckResult DoTrapDisarm(uwObject[] objList, int trapsSkill, uwObject objToCheck, uwObject foundtrigger, uwObject foundtrap)
        {
            string traptype = GetTrapType(foundtrap);
            //UW2 against 8 + the world number, UW1 against 8
            var skillcheckresult = playerdat.SkillCheck(trapsSkill, 8 + (_RES == GAME_UW2 ? worlds.GetWorldNo(playerdat.dungeon_level) : 0), true);
            var inLevel = objList == UWTileMap.current_tilemap.LevelObjects;
            switch (skillcheckresult)
            {
                case playerdat.SkillCheckResult.Success:
                case playerdat.SkillCheckResult.CritSucess:
                    {
                        //The trap on the object was disarmed
                        uimanager.AddToMessageScroll(ZhText.Enabled
                            ? ZhText.Compose(ZhText.ObjectName(objToCheck.item_id), "上的", traptype, GameStrings.GetDisplayString(1, 0x168))
                            : $"The {traptype} on the {GameStrings.GetObjectNounUW(objToCheck.item_id)}{GameStrings.GetString(1, 0x168)}");
                        if (_RES == GAME_UW2)
                        {//the trap and the triggers pointing at it go, from the trigger's target square (delete_trap)
                            if (inLevel && UWTileMap.ValidTile(foundtrigger.quality, foundtrigger.owner))
                            {
                                var tile = UWTileMap.current_tilemap.Tiles[foundtrigger.quality, foundtrigger.owner];
                                ObjectRemover_OLD.RemoveTrapChain(foundtrap, tile.Ptr + 2);
                            }
                            foundtrigger.link = 0;//(in the inventory the trigger is only disconnected)
                        }
                        else
                        {//UW1 frees the trap or trigger from the object's contents (Obj_FreeChain)
                            var found = foundtrigger ?? foundtrap;
                            if (ObjectRemover_OLD.RemoveObjectFromLinkedList(objToCheck.link, found.index, objList, objToCheck.PTR + 6))
                            {
                                if (inLevel)
                                {
                                    ObjectRemover_OLD.ClearContents(found);
                                }
                                ObjectFreeLists.ReleaseFreeObject(found);
                            }
                        }
                        break;
                    }
                case playerdat.SkillCheckResult.Fail:
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x167));//you have failed to disarm
                    break;
                case playerdat.SkillCheckResult.CritFail:
                    uimanager.AddToMessageScroll(ZhText.Enabled
                        ? ZhText.Compose(GameStrings.GetDisplayString(1, 0x166), traptype, "！")
                        : $"{GameStrings.GetString(1, 0x166)} {traptype}");//your bumbling efforts
                    if (foundtrigger == null)
                    {//a trap with no trigger goes off and is deleted
                        trap.ActivateTrap(character: 1, trapObj: foundtrap, ObjectUsed: objToCheck, triggerX: playerdat.playerObject.tileX, triggerY: playerdat.playerObject.tileY, objList: objList);
                        if (inLevel)
                        {
                            ObjectRemover_OLD.RemoveTrapChain(foundtrap, objToCheck.PTR + 6);
                        }
                    }
                    else
                    {
                        trigger.RunTrigger(character: 1, ObjectUsed: objToCheck, TriggerObject: foundtrigger, triggerType: -1, objList: objList);
                    }
                    break;
            }
            return skillcheckresult;
        }


        /// <summary>
        /// Gets the name for the trap
        /// </summary>
        /// <param name="foundtrap"></param>
        /// <returns></returns>
        private static string GetTrapType(uwObject foundtrap)
        {
            int trapclass = foundtrap.item_id & 0x3F;
            string traptype;
            if (trapclass == 0xF)
            {
                //null trap
                traptype = ZhText.Enabled ? "陷阱" : "trap";
            }
            else
            {
                if (trapclass == 0 && foundtrap.owner != 0)
                {
                    traptype = ZhText.Enabled ? "毒陷阱" : "poison trap";
                }
                else
                {
                    traptype = ZhText.Enabled ? ZhText.ObjectName(foundtrap.item_id) : GameStrings.GetObjectNounUW(foundtrap.item_id);
                }
            }

            return traptype;
        }


        /// <summary>
        /// The trap disarming spell
        /// </summary>
        /// <param name="index"></param>
        /// <param name="objList"></param>
        public static void TrapDisarmSpell(int index, uwObject[] objList)
        {
            switch (trapdisarming.DetectTrapTrigger(index, objList, 45))
            {
                case playerdat.SkillCheckResult.Success:
                case playerdat.SkillCheckResult.CritSucess:
                    trapdisarming.DisarmTrap(index, objList, 0x2D); break;//you have detected
                default:
                    Debug.Print("There is no trap detected on that object!");
                    break;
            }
        }


        /// <summary>
        /// Disarming of a trap detected by LOOKing at an object. 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="objList"></param>
        /// <returns></returns>
        public static IEnumerator DisarmAttemptQuestion(int index, uwObject[] objList)
        {
            var disarmstring = GameStrings.GetDisplayString(1, GameStrings.str_you_found_a_trap__do_you_wish_to_try_to_disarm_it_2);//player_3dlook's wyorn: UW1 0xF4, UW2 0x103
            uimanager.instance.TypedInput.Text = "Yes";
            MessageDisplay.YesNoOption = "Yes";
            MessageDisplay.WaitingForYesOrNo = true;
            uimanager.AddToMessageScroll($"{disarmstring}{{TYPEDINPUT}}", mode: MessageDisplay.MessageDisplayMode.TypedInput);

            while (MessageDisplay.WaitingForYesOrNo)
            {
                yield return new WaitOneFrame();
            }
            var response = uimanager.instance.TypedInput;
            if (response.Text.ToUpper() == "YES")
            {
                //attempt disarm
                trapdisarming.DisarmTrap(index, objList, playerdat.Traps);
            }

            //regardless of the result of the disarming. Any look trigger linked to the object has to be fired.
            trigger.TriggerObjectLink(
                character: 1,
                ObjectUsed: objList[index],
                triggerType: (int)triggerObjectDat.triggertypes.LOOK,
                triggerX: objList[index].tileX,
                triggerY: objList[index].tileY,
                objList: UWTileMap.current_tilemap.LevelObjects);

            yield return 0;
        }

    }//end class
}//end namespace