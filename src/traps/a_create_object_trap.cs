using System.Diagnostics;

namespace Underworld
{
    public class a_create_object_trap : trap
    {

        static bool FoundCritterWithBitD = false;
        /// <summary>
        /// Spawns a copy of the object at trap link. UW1 and UW2 have some differing logic here.
        /// </summary>
        /// <param name="triggerObj"></param>
        /// <param name="trapObj"></param>
        /// <param name="objList"></param>
        public static void Activate(uwObject trapObj, int triggerX, int triggerY, uwObject[] objList)
        {
            //rng test on trap.
            if (Rng.r.NextScaled(0x3F) < trapObj.quality)//rand() * 0x3F / 0x8000
            {
                Debug.Print($"Cancelling createobject trap {trapObj.a_name} i:{trapObj.index} l:{trapObj.link} due to RNG");
                return;
            }
            if ((trapObj.link != 0) && (trapObj.is_quant == 0))
            {
                var template = objList[trapObj.link];
                bool doCreate = false;
                if (template.majorclass == 1)
                {
                    //check for nearby spawns
                    doCreate = !CheckForMobileBitD_8InArea(template);
                }
                else
                {
                    doCreate = true;
                }
                if (doCreate)
                {
                    Debug.Print($"Cloning {template.index} {template.a_name}");
                    if (template != null)
                    {
                        if (_RES == GAME_UW2)
                        {
                            CreateObjectUW2(template, triggerX, triggerY, objList);
                        }
                        else
                        {
                            DoCreateObject(triggerX, triggerY, template);//the one roll above is all UW1 makes
                        }
                    }
                }



            }
        }

        /// <summary>
        /// UW2 create object has additional logic relating to loths tomb after loth is freed 
        /// and can spawn leveled creatures if the template object is an adventurer
        /// </summary>
        /// <param name="triggerObj"></param>
        /// <param name="trapObj"></param>
        /// <param name="template"></param>
        /// <param name="objList"></param>
        static void CreateObjectUW2(uwObject template, int triggerX, int triggerY, uwObject[] objList)
        {
            if (worlds.GetWorldNo(playerdat.dungeon_level) == 6)//not in loths tomb
            {
                if (playerdat.GetQuest(7) == 1)
                {
                    return; //loth has been freed. no more object creation in this world, stops the infinite spawning skeletons
                }
            }

            if (template.item_id == 127)//adventurer
            {
                //spawn a random leveled creature. this is caused by sleeping near the trap or on the playerupdatetimer 
                // if within range of 8 tiles from the trap.
                //Eg traps with this condition exist in the gem chamber
                //TRAP_CREATE_OBJECT: rows of 16 creatures up to (level within the world * 3 + castle clock) / 9, and
                //within the row up to the player's level + castle clock + 1 (at most 16), redrawn until eligible
                var lo = ((((playerdat.dungeon_level - 1) % 8) + 1) * 3 + playerdat.GetXClock(1)) / 9;
                var range = playerdat.play_level + playerdat.GetXClock(1) + 1;
                if (range > 16)
                {
                    range = 16;
                }
                if (lo < 1)
                {
                    lo = 1;
                }
                var item_id = 0;
                bool CritterFound = false;
                do
                {
                    item_id = 0x40 + (Rng.r.Next(lo) << 4) + Rng.r.Next(range);
                    var str = critterObjectDat.strength(item_id);//eligible_castle_monster
                    CritterFound = (str != 0) && (Rng.r.Next(str) == 0);
                } while (!CritterFound);

                Debug.Print($"This trap will spawn {GameStrings.GetSimpleObjectNameUW(item_id)}");

                //I think the logic now is to temporarily make the template the new item id, 
                template.item_id = item_id;
                //but also initialise its defaults depending on the critter defaults
                ObjectCreator.InitialiseCritter(template);
                //Create the object 
                //template.MobileUnk_0xA |=0x80; //set bit
                template.UnkBit_0XA_Bit7 = 1;
                var spawned = DoCreateObject(triggerX, triggerY, template);
                if (spawned != null)
                {
                    spawned.npc_attitude = 0;
                    spawned.SpawnedCritter_0XD_Bit8 = 1;
                }
                //and then revert it back to a the adventurer template.
                template.item_id = 127;

            }
            else
            {
                DoCreateObject(triggerX, triggerY, template);
            }
        }


        /// <summary>
        /// Standard create object
        /// </summary>
        /// <param name="triggerObj"></param>
        /// <param name="template"></param>
        private static uwObject DoCreateObject(int triggerX, int triggerY, uwObject template)
        {
            int slot;
            if (template.IsStatic)
            {
                //static object spawn
                slot = ObjectFreeLists.GetAvailableObjectSlot(ObjectFreeLists.ObjectListType.StaticList);
            }
            else
            {
                //mobile object spawn
                slot = ObjectFreeLists.GetAvailableObjectSlot(ObjectFreeLists.ObjectListType.MobileList);
            }
            if (slot == 0)
            {//no free object: nothing is created
                return null;
            }
            var newobj = UWTileMap.current_tilemap.LevelObjects[slot];
            //copy from template to new obj
            if (template.IsStatic)
            {
                for (int i = 0; i < 8; i++)
                {
                    newobj.DataBuffer[newobj.PTR + i] = template.DataBuffer[template.PTR + i];
                }
            }
            else
            {
                for (int i = 0; i < 27; i++)
                {
                    newobj.DataBuffer[newobj.PTR + i] = template.DataBuffer[template.PTR + i];
                }
            }

            //set new position and spawn
            newobj.tileX = triggerX;//triggerObj.quality;
            newobj.tileY = triggerY;//triggerObj.owner;
            if (UWTileMap.ValidTile(newobj.tileX, newobj.tileY))
            {
                var tile = UWTileMap.current_tilemap.Tiles[newobj.tileX, newobj.tileY];
                UWTileMap.GetRandomXYZForTile(tile, out int newxpos, out int newypos, out int newzpos);
                newobj.xpos = (short)newxpos;//obj.xpos;
                newobj.ypos = (short)newypos;///obj.ypos;
                newobj.zpos = (short)newzpos; //obj.zpos;
                newobj.next = tile.indexObjectList;//link to tile
                tile.indexObjectList = newobj.index;
            }
            if ((newobj.is_quant == 0) && (newobj.link > 0))
            {//the copy gets its own copy of the first object the template holds, with nothing after or in it
                var contentSlot = ObjectFreeLists.GetAvailableObjectSlot(ObjectFreeLists.ObjectListType.StaticList);
                if (contentSlot != 0)
                {
                    var source = UWTileMap.current_tilemap.LevelObjects[newobj.link];
                    var contentCopy = UWTileMap.current_tilemap.LevelObjects[contentSlot];
                    for (int i = 0; i < 8; i++)
                    {
                        contentCopy.DataBuffer[contentCopy.PTR + i] = source.DataBuffer[source.PTR + i];
                    }
                    contentCopy.next = 0;
                    if ((contentCopy.is_quant == 0) && (contentCopy.link > 0))
                    {
                        contentCopy.link = 0;
                    }
                    newobj.link = contentCopy.index;
                }
            }
            ObjectCreator.RenderObject(newobj, UWTileMap.current_tilemap);
            if (newobj.majorclass == 7)
            {//an animation joins the animation list (add_animobj, running until it ends)
                animo.CreateAnimoLink(newobj, 0xFFFF);
            }
            return newobj;
        }

        /// <summary>
        /// Search for Create Object Traps that are on the map and if the player is within 8 tiles of them trigger them
        /// </summary>
        public static void FindAndRunCreateObjectTraps(bool isPlayer)
        {//DoWanderingMonsters: awake (isPlayer) only out of the player's sight, 8 squares or more away; asleep everywhere
            //function that spawns critters randomly based on create object traps on the map");
            //this is called from sleeping and during playerupdates on a random chance.
            for (int x = 0; x < 64; x++)
            {
                for (int y = 0; y < 64; y++)
                {
                    var foundObject = objectsearch.FindMatchInTile(
                        tileX: x, tileY: y,
                        majorclass: 6,
                        minorclass: 0,
                        classindex: 7);
                    if (foundObject != null)
                    {

                        Debug.Print($"Found Create Object Trap at {foundObject.index}");
                        if (foundObject.flags_full == 0)
                        {
                            if (foundObject.link > 0)
                            {
                                var toCreate = UWTileMap.current_tilemap.LevelObjects[foundObject.link];
                                if (toCreate != null)
                                {
                                    if (!toCreate.IsStatic)
                                    {
                                        toCreate.SpawnedCritter_0XD_Bit8 = 1;

                                        if (TileInfo.CheckIfOutsideRange(targetX: x, targetY: y, isPlayer: isPlayer, range: 8))
                                        {
                                            trap.ActivateTrap(0, foundObject, null, x, y, UWTileMap.current_tilemap.LevelObjects);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        static bool CheckForMobileBitD_8InArea(uwObject template)
        {//dont_create_wandering_monster_here: a created object within four squares of the template
            FoundCritterWithBitD = false;

            //CallBacks.RunCodeOnTargetsAroundObject()
            CallBacks.RunCodeOnTargetsAroundObject(
                methodToCall: CheckForMobileBitD_8,
                CentreObject: template.index,
                rngProbablity: 1,
                targetType: 0,
                distanceFromObject: 0,
                tileRadius: 4);

            return FoundCritterWithBitD;
        }

        public static bool CheckForMobileBitD_8(int x, int y, uwObject obj, TileInfo tile, int srcIndex)
        {
            if (obj.SpawnedCritter_0XD_Bit8 == 0)
            {
                return FoundCritterWithBitD;
            }
            if (obj == CallBacks.CodeCenterObject)
            {
                return FoundCritterWithBitD;
            }
            if (obj == playerdat.playerObject)
            {
                return FoundCritterWithBitD;
            }
            FoundCritterWithBitD = true;
            return FoundCritterWithBitD;
        }

    }//end class
}//end namespace