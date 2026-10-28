using System;
using System.Diagnostics;

namespace Underworld
{
    /// <summary>
    /// Class for managing the spawning of npc loot objects
    /// </summary>
    public partial class npc : objectInstance
    {
        //static bool DebugSpawnAllLoot=false;

        /// <summary>
        /// Generates the npc's inventory on demand. If lootspawned flag is set the npc already has an inventory
        /// </summary>
        /// <param name="critter"></param>
        public static void spawnloot(uwObject critter)
        {
            if (critter.LootSpawnedFlag == 0)
            {
                ValuableObjectLoot(critter);
                FoodLoot(critter);
                WeaponLoot(critter);
                OtherLoot(critter);
            }
            critter.LootSpawnedFlag = 1;
        }

        /// <summary>
        /// Drops coins and gems into then npc inventory.
        /// </summary>
        /// <param name="critter"></param>
        public static void ValuableObjectLoot(uwObject critter)
        {
            //The game world chooses which valuable item type is dropped in the valuables object class (starting at coin)
            var world = worlds.GetWorldNo(playerdat.dungeon_level);

            var Critter_var_5 = critterObjectDat.valuable_multipleprobability(critter.item_id);
            var valuableDropProbability = critterObjectDat.valuable_loot_probability(critter.item_id);

            //generate_treasure
            if (Rng.r.Next(16) >= valuableDropProbability)
            {//Do not spawn a valuable.
                return;
            }
            //the treasure type: deeper (UW1 the level, UW2 the world) gives better treasure more often
            int valuable_type_var_1;
            if (_RES == GAME_UW2)
            {
                var w = (playerdat.dungeon_level - 1) / 8;
                valuable_type_var_1 = Rng.r.Next(37 - w * 3) - (30 - w * 3);
            }
            else
            {
                valuable_type_var_1 = Rng.r.Next(40 - playerdat.dungeon_level * 3) - (33 - playerdat.dungeon_level * 3);
            }
            if (valuable_type_var_1 < 0) { valuable_type_var_1 = 0; }
            if ((valuable_type_var_1 == 1) && (_RES == GAME_UW2)) { valuable_type_var_1 = 0; }//make sure no storage crystal is dropped in UW2

            //the type's value, stretched above 4, in a signed char as the original's (gems of 25 and more go negative)
            var coinitemid = 0xA0;
            sbyte value = (sbyte)commonObjDat.monetaryvalue(coinitemid + valuable_type_var_1);
            if (value == 0) { value = 1; }
            if (value >= 12)
            {
                value = (sbyte)(value * 8 - 68);
            }
            else if (value >= 8)
            {
                value = (sbyte)(value * 4 - 20);
            }
            else if (value >= 4)
            {
                value = (sbyte)(value * 2 - 4);
            }

            int var_3_qty;
            if ((Critter_var_5 << 2) < value)
            {//one, at a chance
                if (Rng.r.Next(value) >= (Critter_var_5 << 2))
                {
                    return;
                }
                var_3_qty = 1;
            }
            else
            {//4d(2 * (4 * prob / value)) / 4
                var dice = value == 0 ? 0 : ((Critter_var_5 << 2) / value) << 1;
                var_3_qty = (sbyte)(Rng.DiceRoll(4, dice) >> 2);
            }

            if (var_3_qty > 0)
            {
                //Create obj
                uwObject obj = spawnLootObject(critter, valuable_type_var_1 + coinitemid);
                if (obj != null)
                {
                    //Apply the new qty of valuables to the result
                    obj.link = (short)var_3_qty;
                    Debug.Print($"Spawning {var_3_qty} {GameStrings.GetObjectNounUW(coinitemid + valuable_type_var_1)}");
                }
            }
        }

        /// <summary>
        /// Spawns the final loot object on the critters inventory
        /// </summary>
        /// <param name="critter"></param>
        /// <param name="itemid"></param>
        /// <returns></returns>
        private static uwObject spawnLootObject(uwObject critter, int itemid)
        {
            var slot = ObjectCreator.PrepareNewObject(itemid);
            if (slot != 0)
            {
                //add to critter object list
                var obj = UWTileMap.current_tilemap.LevelObjects[slot];
                //Insert at the head of the critters inventory.
                obj.next = critter.link;
                critter.link = (short)slot;
                return obj;
            }
            else
            {
                return null;
            }
        }

        public static void FoodLoot(uwObject critter)
        {
            if (Rng.r.Next(0, 16) < critterObjectDat.foodloot_probability(critter.item_id))
            {
                var item_id = critterObjectDat.foodloot_item(critter.item_id) + 0xB0;
                var obj = spawnLootObject(critter, item_id);
                if(obj!=null)
                {
                    Debug.Print($"Spawning {GameStrings.GetObjectNounUW(item_id)}");
                }                
            }
        }

        /// <summary>
        /// Drops loot that has a quality and qty to it. Probably weapons.
        /// </summary>
        /// <param name="critter"></param>
        public static void WeaponLoot(uwObject critter)
        {
            for (int i = 0; i < 2; i++)
            {
                int item_id = critterObjectDat.weaponloot(critter.item_id, i);
                int quality = 0;
                int qty = 0;
                if (item_id != -1)
                {
                    //Create object now. Set properties later

                    //Now calculate quality 50:50 chance of rnd(0-64) or a figure based on the current dungeon level
                    quality = RandomLootQuality();
                    if ((item_id & 0x30) >> 4 == 1)
                    {
                        if (rangedObjectDat.RangedWeaponType(item_id) == 0xC0)
                        {
                            //item is ammo. it needs a qty.
                            qty = 4 + (Rng.r.Next(0, 8));
                        }
                    }

                    var obj = spawnLootObject(critter, item_id);
                    if (obj != null)
                    {
                        obj.quality = (short)(quality & 0x3F);//the rolled quality (a 6-bit field)
                        if (qty > 0)
                        {
                            obj.link = (short)qty;
                        }
                        Debug.Print($"Spawning {qty} {GameStrings.GetObjectNounUW(item_id)} of quality {quality}");
                    }
                }
            }
        }

        /// <summary>
        /// Loot spawns for NPCs have random quality. 50:50 either based on rng(0-63) or based on level no
        /// </summary>
        /// <returns></returns>
        private static int RandomLootQuality()
        {
            int quality;
            if (Rng.r.Next(2) == 0)
            {//4 to 8 times the level (the 6-bit field wraps it on deep levels)
                quality = (playerdat.dungeon_level << 2) + Rng.r.Next(playerdat.dungeon_level << 2);
            }
            else
            {
                quality = Rng.r.Next(64);
            }

            return quality;
        }

        /// <summary>
        /// Drops other loot objects based on rng
        /// </summary>
        /// <param name="critter"></param>
        public static void OtherLoot(uwObject critter)
        {
            for (int i = 0; i < 2; i++)
            {
                if (Rng.r.Next(0, 16) < critterObjectDat.otherloot_probability(critter.item_id, i))
                {
                    var item_id = critterObjectDat.otherloot_item(critter.item_id, i);
                    var quality = RandomLootQuality();
                    if ((_RES == GAME_UW2) && (commonObjDat.qualitytype(item_id) == 0xF))
                    {//UW2: these are always 40
                        quality = 40;
                    }
                    var obj = spawnLootObject(critter, item_id);
                    if (obj!=null)
                    {
                        obj.quality = (short)(quality & 0x3F);
                        Debug.Print($"Spawning {GameStrings.GetObjectNounUW(item_id)} of quality {quality}");
                    }                    
                }
            }
        }
    } //end class
}//end namespace