using System;
using System.Diagnostics;
namespace Underworld
{
    /// <summary>
    /// Class for managing damage to objects.
    /// </summary>
    public class damage : UWClass
    {
        public static void DamagePlayer(int basedamage, int damagetype, int damagesource)
        {
            Debug.Print("TODO further implement this");
            basedamage = ScaleDamage(playerdat.playerObject.item_id, ref basedamage, damagetype);//check_res
            playerdat.playerObject.ProjectileSourceID = (short)damagesource;

            if (basedamage < playerdat.play_hp)
            {
                playerdat.play_hp -= basedamage;
            }
            else
            {
                playerdat.play_hp = 0;
            }
        }

        /// <summary>
        /// Applies damage to objects
        /// </summary>
        /// <param name="objToDamage"></param>
        /// <param name="basedamage"></param>
        /// <param name="damagetype"></param>
        /// <param name="damagesource"></param>
        public static bool DamageObject(uwObject objToDamage, int basedamage, int damagetype, uwObject[] objList, bool WorldObject, int damagesource)
        {
            basedamage = ScaleDamage(objToDamage.item_id, ref basedamage, damagetype);
            if (basedamage != 0)
            {
                Debug.Print($"Damage {objToDamage.a_name} by {basedamage} type {damagetype} source {damagesource}");
            }
      
            if (objToDamage.majorclass == 1)
            {
                return DamageNPC(
                    critter: objToDamage,
                    basedamage: basedamage,
                    damagesource: damagesource);
            }
            else
            {
                if (DamageOtherObjectTypes(objToDamage, basedamage, damagesource, WorldObject))
                {
                    //object should be destroyed
                    return ObjectDestruction(
                        objToDestroy: objToDamage,
                        damagetype: damagetype,
                        objList: objList,
                        WorldObject: WorldObject);
                }
            }
            return false;
        }


        /// <summary>
        /// Applies damage to the npc with the specified damage type
        /// </summary>
        /// <param name="critter"></param>
        /// <param name="damage"></param>
        /// <param name="damagetype"></param>
        static bool DamageNPC(uwObject critter, int basedamage, int damagesource)
        {
            //basedamage = ScaleDamage(critter.item_id, ref basedamage, damagetype);
            if (basedamage != 0)
            {
                Debug.Print($"Damage {critter.a_name} by {basedamage}");
            }

            //Note to be strictly compatable with UW behaviour the damage should be accumulated for the npc and test
            //once per tick This is used to control the angering behaviour of the npc in checking against passiveness and for music/gui updates on the avatar.
            critter.AccumulatedDamage += (short)basedamage;//how much total damage has been applied in this tick.        
            critter.npc_hp = (byte)Math.Max(0, critter.npc_hp - basedamage);
            if (critter == playerdat.playerObject)
            {
                playerdat.play_hp = critter.npc_hp;
            }

            //make the npc react to the damage source. player if 1 
            critter.ProjectileSourceID = (short)damagesource;
            if (damagesource == 1)
            {//player applied damage
                if (critter.UnkBit_0XA_Bit7 == 0)
                {
                    playerdat.LastDamagedNPCIndex = critter.index;
                    playerdat.LastDamagedNPCType = critterObjectDat.generaltype(critter.item_id);
                    playerdat.LastDamagedNPCTime = playerdat.ClockValue;
                    playerdat.LastDamagedNPCTileX = critter.tileX;
                    playerdat.LastDamagedNPCTileY = critter.tileY;
                    playerdat.LastDamagedNPCZpos = critter.zpos;   
                }
            }


            //The combat themes (damage_crit): the player's blow on a critter (UW1 any of his blows) picks foe hurt below a
            //quarter of its average hit points, else combat; a blow on the player from anyone picks danger below a quarter
            //of his maximum, else combat. Only these mark the last combat for the themes' ten seconds. A killing blow
            //returns before them (crit_die), its victory theme comes from the experience award.
            byte foeHurt = (byte)(_RES == GAME_UW2 ? 2 : 5);
            var killed = (critter.npc_hp == 0) && (critter.index != 1);
            if (!killed)
            {
                if ((damagesource == 1) && ((_RES != GAME_UW2) || (critter.index != 1)))
                {
                    var ratio = (critter.npc_hp << 6) / (critterObjectDat.avghit(critter.item_id) + 1);
                    XMIMusic.ChangeThemeMusic(ratio < 0x10 ? foeHurt : (byte)(foeHurt + 1));
                    XMIMusic.CombatMusicTimer = main.GlobalPITTimer;
                }
                else if ((critter.index == 1) && (damagesource != 0))
                {
                    var ratio = (playerdat.play_hp << 6) / (playerdat.max_hp + 1);
                    XMIMusic.ChangeThemeMusic(ratio < 0x10 ? (byte)(foeHurt + 2) : (byte)(foeHurt + 1));
                    XMIMusic.CombatMusicTimer = main.GlobalPITTimer;
                }
            }

            if ((critter.npc_hp == 0) & (critter.index != 1))
            {
                if (critter.npc_animation != npc.ANIMATION_DEATH)
                { 
                    //if not already in the death animation
                    if (npc.SpecialDeathCases(critter))
                    {
                        if (damagesource == 1)
                        {//only the player's kills earn experience (crit_die, who == 1)
                            playerdat.AwardXPKill(critter);
                        }
                        critter.npc_animation = npc.ANIMATION_DEATH;
                        critter.AnimationFrame = 0;
                        npc.RedrawAnimation(critter);
                        byte DeathSound = 0xFF;
                        if (_RES == GAME_UW2)
                        {
                            switch (critterObjectDat.deathsound(critter.item_id))
                            {
                                case 1://humanoids
                                    DeathSound = 0x6; break;
                                case 2://creepycrawlies
                                    DeathSound = 0x22; break;
                                case 3://Undead/Demons
                                    DeathSound = 0x23; break;
                                case 4://Monsters
                                    DeathSound = 0x24; break;
                                case 0://silent
                                    break;
                                default://crit_die: any other kind is the humanoid's
                                    DeathSound = 0x6; break;
                            }
                        }
                        else
                        {
                            if (critterObjectDat.deathsound(critter.item_id) == 1)
                            {
                                DeathSound = 0x6; //only the avatar has this sound effect?
                            }
                        }
                        if (DeathSound != 0xFF)
                        {
                            UWsoundeffects.PlaySoundEffectAtCoordinate(DeathSound, (critter.tileX << 3) + critter.xpos, (critter.tileY << 3) + critter.ypos, 0);
                        }
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// damage_object: no damage for quality class 3 or an object with id bit 13 (UW2: other than a door); the damage
        /// is shifted right by the quality class and nothing happens when none is left. A mobile object loses hit points, a
        /// spiked door (0x140..0x147, owner bit 0) the strength in its owner field, anything else quality (UW2: the owner
        /// is told). A destroyed static object on the map sets off its use triggers. Returns whether it was destroyed.
        /// </summary>
        static bool DamageOtherObjectTypes(uwObject objToDamage, int basedamage, int damagesource, bool WorldObject)
        {
            //traps and triggers are not spared: their quality class is 0 in both games, so damage_square's blasts wear
            //them down like any other object
            bool IsBroken = false;
            var qualityclass = commonObjDat.qualityclass(objToDamage.item_id);
            if ((objToDamage.doordir == 1) && ((_RES != GAME_UW2) || (objToDamage.OneF0Class != 0x14)))
            {//id bit 13: UW1 any object, UW2 any but a door
                return false;
            }
            if (qualityclass == 3)
            {
                Debug.Print($"{objToDamage.a_name} cannot be damaged");
                return false;//invulnerable object
            }

            basedamage >>= qualityclass;//reduce damage based on quality class
            if (basedamage <= 0)
            {
                return false;
            }
            if (objToDamage.IsStatic)
            {//damage to a static object

                if (
                    (objToDamage.item_id >= 0x140) && (objToDamage.item_id <= 0x147)
                    &&
                    ((objToDamage.owner & 0x1) == 1)
                    &&
                    ((objToDamage.owner >> 1) > 0)
                )
                {
                    //damage to spiked closed doors?
                    var finalquality = Math.Max(0, (objToDamage.owner >> 1) - basedamage);
                    finalquality <<= 1;
                    finalquality = (objToDamage.owner & 0x1) | finalquality;
                    finalquality &= 0x3F;
                    objToDamage.owner = (short)finalquality; //apply damage to owner instead.
                    Debug.Print($"Final owner(spiked door) is {objToDamage.owner}");
                }
                else
                {
                    var finalquality = Math.Max(0, objToDamage.quality - basedamage);
                    objToDamage.quality = (short)finalquality;
                    Debug.Print($"Final quality is {objToDamage.quality}");
                    IsBroken = (finalquality == 0);
                    if ((_RES == GAME_UW2) && commonObjDat.canhaveowner(objToDamage.item_id))
                    {//damaging an owned object counts against the player with its owner (damage_object, player_did_bad)
                        a_do_trap_trespass.HackTrapTrespass(objToDamage.owner);
                    }
                }
            }
            else
            {//damage to a mobile object (excl NPCS)
                var finalquality = Math.Max(0, objToDamage.npc_hp - basedamage);
                objToDamage.npc_hp = (byte)finalquality;
                Debug.Print($"Final hp is {objToDamage.npc_hp}");
                IsBroken = (finalquality == 0);
            }

            if ((IsBroken) && (objToDamage.IsStatic) && WorldObject)
            {
                if (UWTileMap.ValidTile(objToDamage.tileX, objToDamage.tileY))
                {
                    trigger.TriggerObjectLink(
                            character: damagesource,
                            ObjectUsed: objToDamage,
                            triggerType: (int)triggerObjectDat.triggertypes.USE,
                            triggerX: objToDamage.tileX,
                            triggerY: objToDamage.tileY,
                            objList: UWTileMap.current_tilemap.LevelObjects);
                }
            }
            return IsBroken;
        }


        /// <summary>
        /// remove_object: what a destroyed object leaves. Off the map (in the inventory) nothing happens and it counts as
        /// gone. A door (class index 0..7) bursts open and loses its locks; a chest or barrel loses its first lock and
        /// spills (UW2: unless id bit 13); a UW2 weapon breaks unless the damage is pure fire; a bag spills unless something
        /// in it is tenacious (Obj_Elem_Fate 10); UW2's djinn bottle has its quest. Anything else: fire burns away a pile
        /// of debris, or one time in four sets it alight; its contents go. Then it becomes its debris (UW1 0xD5 or 0xD6 at
        /// random, UW2 debris_type) and settles with obj_deal.
        /// </summary>
        /// <returns>Whether the object has left the map</returns>
        public static bool ObjectDestruction(uwObject objToDestroy, int damagetype, uwObject[] objList, bool WorldObject)
        {
            if ((!WorldObject) || (!UWTileMap.ValidTile(objToDestroy.tileX, objToDestroy.tileY)))
            {
                return true;
            }
            var item = objToDestroy.item_id;
            int Debris = -2;
            if (objToDestroy.OneF0Class == 0x14)
            {
                //doors
                if (objToDestroy.classindex <= 7)
                {//it bursts open (no unlocking, so no unlock traps), then its locks go
                    door.OpenDoor(objToDestroy);
                }
                a_lock.RemoveAllLocks(obj: objToDestroy, RemoveAll: true);
                Debris = -1;
            }
            else if ((item == 0x15B) || (item == 0x15D))
            {//barrel, chest
                if ((_RES == GAME_UW2) && (objToDestroy.doordir == 1))
                {
                    Debris = -1;
                }
                else
                {//its first lock goes, then it is used as a container by no one (emptied, no messages)
                    a_lock.RemoveAllLocks(objToDestroy, false);
                    container.Use(objToDestroy, WorldObject, toPlayer: false);
                }
            }
            else if ((_RES == GAME_UW2) && (objToDestroy.OneF0Class == 0) && (damagetype != 8))
            {//weapons
                Debris = ((item == 3) || (item == 0xA)) ? 0xC7 : 0xC5 + weaponObjectDat.skill(item);
            }
            else if (objToDestroy.OneF0Class == 8)
            {//bags
                if (!ObjectRemover_OLD.ObjectCullingTest(objToDestroy, 10))
                {
                    Debris = -1;
                }
                else
                {
                    container.DumpTheBag(objToDestroy, false);
                }
            }
            else if ((_RES == GAME_UW2) && (item == 0x116))
            {
                if (djinnbottle.DestroyDjinnBottle(objToDestroy))
                {
                    return true;
                }
            }
            else
            {
                if ((damagetype & 8) != 0)
                {
                    if ((item == 0xD6) || ((_RES != GAME_UW2) && (item == 0xD5)))
                    {//fire burns away a pile of debris
                        ObjectRemover_OLD.DeleteObjectFromTile_DEPRECIATED(
                            tileX: objToDestroy.tileX,
                            tileY: objToDestroy.tileY,
                            indexToDelete: objToDestroy.index,
                            RemoveFromWorld: true);
                        return true;
                    }
                    else if ((Rng.r.Next(0x8000) & 3) == 0)
                    {
                        animo.SpawnAnimoAtTarget(objToDestroy, 8, 0, objToDestroy.tileX, objToDestroy.tileY);
                        Debris = _RES == GAME_UW2 ? 0xD6 : 0xD5 + Rng.r.Next(0x8000) * 2 / 0x8000;
                    }
                }
                ObjectRemover_OLD.ClearContents(objToDestroy);
            }

            if (Debris < -1)
            {
                Debris = _RES == GAME_UW2 ? GetObjectTypeDebris(objToDestroy, damagetype) : 0xD5 + Rng.r.Next(0x8000) * 2 / 0x8000;
            }

            if (Debris >= 0)
            {
                objToDestroy.item_id = Debris;
                if (_RES == GAME_UW2)
                {
                    if (!objToDestroy.IsStatic)
                    {
                        objToDestroy.npc_hp = 40;
                    }
                    objToDestroy.quality = 40;
                }
                objectInstance.RedrawFull(objToDestroy);
                if (objToDestroy.IsStatic && (motion.PlacedObjectCollision_seg030_2BB7_10BC(objToDestroy, objToDestroy.tileX, objToDestroy.tileY, 1) == null))
                {//obj_deal: it fell into water or lava
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// debris_type (UW2): a weapon (items 0..15) breaks unless the damage is pure fire, the dagger into the broken
        /// dagger and others into the broken form of their skill (0xC5 + skill); furniture leaves wood chips, the rest debris.
        /// </summary>
        public static int GetObjectTypeDebris(uwObject objToDestroy, int damagetype)
        {
            if ((objToDestroy.OneF0Class == 0) && (damagetype != 8))
            {
                return objToDestroy.item_id == 3 ? 0xC7 : 0xC5 + weaponObjectDat.skill(objToDestroy.item_id);
            }
            return objToDestroy.OneF0Class == 0x15 ? 0xDC : 0xD6;
        }


        public static int ScaleDamage(int item_id, ref int basedamage, int damagetype)
        {
            if (_RES == GAME_UW2)
            {
                return ScaleDamageUW2(item_id: item_id, basedamage: ref basedamage, damagetype: damagetype);
            }
            else
            {
                return ScaleDamageUW1(item_id: item_id, basedamage: ref basedamage, damagetype: damagetype);
            }
        }


        /// <summary>
        /// The item's resist byte; the player's (item 127) also has the bits his resistance spells and
        /// enchantments set (player_affected_by ORs them into ComObjData[127].resist).
        /// </summary>
        static int Resistances(int item_id)
        {
            var scales = commonObjDat.scaleresistances(item_id);
            if (item_id == 127)
            {
                scales |= playerdat.PlayerDamageTypeScale;
            }
            return scales;
        }

        static int ScaleDamageUW1(int item_id, ref int basedamage, int damagetype)
        {

            var scales = Resistances(item_id);

            if ((scales & damagetype) != 0)
            { //has some sort of resistance
                if ((damagetype & 3) != 0) //magic resistances
                {
                    //damage & 3 != 0
                    //seg025_26A1_4BA:
                    var r = Rng.r.Next(0, 3);
                    if (r >= (scales & 0x3))
                    {
                        //seg025_26A1_4D5
                        damagetype &= 0xFC; //mask out magic damage. possibly means magic can be used in combo with other damage type??
                    }
                    else
                    {
                        return 0;
                    }
                }

                //seg025_26A1_4DD
                if ((scales & damagetype) != 0)
                { //the damage type is resisted after possibly masking out magic damage.
                    return 0;
                }
            }
            return basedamage;
        }
        /// <summary>
        /// Scales damage up or down based on the NPCs damage resistances
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="basedamage"></param>
        /// <param name="damagetype"></param>
        /// <returns></returns>
        static int ScaleDamageUW2(int item_id, ref int basedamage, int damagetype)
        {
            return CheckResistanceUW2(Resistances(item_id), ref basedamage, damagetype);
        }

        /// <summary>
        /// UW2's check_res on a resist byte: the damage left after resistances, doubled for fire against
        /// cold resistance or cold against fire resistance without cold resistance.
        /// </summary>
        public static int CheckResistanceUW2(int scales, ref int basedamage, int damagetype)
        {
            if ((scales & damagetype) != 0)
            { //has some sort of resistance
                if ((damagetype & 3) != 0) //magic resistances
                {
                    //damage & 3 != 0
                    //seg025_26A1_4BA:
                    var r = Rng.r.Next(0, 3);
                    if (r >= (scales & 0x3))
                    {
                        //seg025_26A1_4D5
                        damagetype &= 0xFC; //mask out magic damage. possibly means magic can be used in combo with other damage type??
                    }
                    else
                    {
                        return 0;
                    }
                }

                //seg025_26A1_4DD
                if ((scales & damagetype) != 0)
                { //the damage type is resisted after possibly masking out magic damage.
                    return 0;
                }
            }

            //if at this point magic has possibly been masked out. but checks need to be made to see how fire/ice damage interact with fire/ice resistances
            //Ice is unique to UW2 so this does not apply to UW1.
            //seg 25:429
            if ((damagetype & 8) != 0) //test for fire damage
            {
                if ((scales & 0x20) != 0) //test for ice resistance
                {
                    return VulnerableDamage(ref basedamage); //fire does double damage to ice resistance creatures?
                }
            }

            //seg025_26A1_4F5
            if ((damagetype & 0x20) == 0) //test for ice damage
            {
                return basedamage;
            }
            else
            {//not ice damage
                if ((scales & 8) == 0)//test for no fire resistance
                {
                    return basedamage;
                }
                else
                {
                    //if has fire resistance
                    if ((scales & 0x28) == 0x28) //test for fire and ice together.
                    {
                        return basedamage; //when fire and ice together ice only does regular damage
                    }
                    else
                    {
                        return VulnerableDamage(ref basedamage); //if has no ice resistance, ice damage will do double damage
                    }
                }
            }

        }

        /// <summary>
        /// Applies damage increase when object is vulnerable to a damage type.
        /// </summary>
        /// <param name="basedamage"></param>
        /// <returns></returns>
        static int VulnerableDamage(ref int basedamage)
        {
            basedamage = basedamage < 0x7F ? basedamage << 1 : 0xFF;//check_res
            return basedamage;
        }


        public static void DamageObjectsInTile(int tileX, int tileY, int source, int spellclass)
        {
            if (spellclass != 0)
            {
                var tile = UWTileMap.current_tilemap.Tiles[tileX, tileY];

                if (tile.indexObjectList != 0)
                {
                    var next = tile.indexObjectList;
                    int[] damagetypes = new int[] { 0xB, 3 };
                    int[] damagerange = new int[] { 6, 5 };
                    int[] damagerolls = new int[] { 0xA, 6 };
                    int index = (spellclass - 1) & 1;
                    while (next != 0)
                    {
                        var nextObj = UWTileMap.current_tilemap.LevelObjects[next];
                        next = nextObj.next;
                        var damagetoapply = Rng.DiceRoll(damagerolls[index], damagerange[index]);
                        DamageObject(
                            objToDamage: nextObj,
                            basedamage: damagetoapply,
                            damagetype: damagetypes[index],
                            objList: UWTileMap.current_tilemap.LevelObjects,
                            WorldObject: true,
                            damagesource: source);

                    }
                }
            }
        }


        /// <summary>
        /// Damages a piece of equipment in the inventory at the specified slot
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>1 if object was destroyed, 0 if damage, negative if no change</returns>
        public static int DamageEquipment(int slot, int damage, int damagetype, int arg6, int arg8)
        {
            uwObject obj = null;

            var si = 0;
            string distring = "";
            if (slot <= 11)
            {
                obj = playerdat.GetInventorySlotObject(slot);
            }
            else
            {
                var index = uimanager.BackPackIndices[slot - 11];
                if (index != -1)
                {
                    obj = playerdat.InventoryObjects[index];
                }
            }

            if (obj != null)
            {
                var originalquality = obj.quality;
                switch (arg6)
                {
                    case 2:
                        break;
                    case 0:
                        if (obj.OneF0Class != 0)
                        {//a melee weapon only (class 0, items 0-15)
                            return -2;

                        }
                        break;
                    default:
                        {
                            if (!uwObject.CheckIfObjectInValidSlot(obj, slot))//worn in the slot being damaged (ObjWorn)
                            {
                                return -2;
                            }
                        }
                        break;
                }

                //apply damage: damage_item off the map, so a destroyed object is left as it is for this to remove
                var result = DamageObject(objToDamage: obj, basedamage: damage, damagetype: damagetype, objList: playerdat.InventoryObjects, WorldObject: false, damagesource: 0);
                if (result)
                {//destroyed
                    if (arg8 != 0)
                    {//its debris by the player (near_mob_put_at, range 6)
                        var debris = _RES == GAME_UW2 ? GetObjectTypeDebris(obj, damagetype) : 0xD5 + Rng.r.Next(0x8000) * 2 / 0x8000;
                        var junkIndex = ObjectCreator.PrepareNewObject(debris);
                        if (junkIndex != 0)
                        {
                            var junk = UWTileMap.current_tilemap.LevelObjects[junkIndex];
                            var placed = motion.PutAt(junk, (playerdat.playerObject.tileX << 3) + playerdat.playerObject.xpos, (playerdat.playerObject.tileY << 3) + playerdat.playerObject.ypos, playerdat.playerObject.zpos, 6, false);
                            if (placed != null)
                            {
                                ObjectCreator.RenderObject(placed, UWTileMap.current_tilemap);
                            }
                        }
                    }
                    ObjectCreator.Consume(obj, true);//removes from inventory     
                    playerdat.PlayerStatusUpdate();
                    distring = " destroyed.";
                    si = 1;
                }
                else
                {//taken damage and not destroyed
                    if (obj.quality != originalquality)
                    {
                        distring = " damaged.";
                        si = 0;
                    }
                    else
                    {
                        return -1;
                    }
                }
                var name = GameStrings.GetName(obj.item_id, article: false, plural: false);
                uimanager.AddToMessageScroll(ZhText.Enabled
                    ? ZhText.Compose("你的", ZhText.ObjectName(obj.item_id), si == 1 ? "壞掉了。" : "受損了。")
                    : $"Your {name}{(name.EndsWith("s") ? " were" : " was")}{distring}");
                return si;
            }
            else
            {
                return -2;
            }
        }


    }//end class
}//end namespace