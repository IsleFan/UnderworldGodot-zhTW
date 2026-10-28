using System;
using System.Diagnostics;

namespace Underworld
{
    public class Food : objectInstance
    {
        public static bool Use(uwObject obj, bool WorldObject)
        {
            //Ordinarily food cannot be used from the world except when dropped on the player head.
            //when dropping food on the player head the object will be a WorldObject. 
            //This checks that the object being used is the object in the player hand. 
            // Fixes issue where dropping food was not working because original check was for world object only
            if ((WorldObject) && (obj.index != playerdat.ObjectInHand))  
            {
                return true; // suppress any do not use message.
            }
            if ((obj.index == playerdat.ObjectInHand) && (obj.ObjectQuantity > 1))
            {//UseFood: a stack on the cursor cannot be eaten (UW1 says so)
                if (_RES != GAME_UW2)
                {
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x77));
                }
                return true;
            }

            if (_RES == GAME_UW2)
            {
                switch (obj.classindex)
                {
                    case 0: //a_piece of meat&pieces of meat
                    case 1: //a_piece of meat&pieces of meat
                    case 2: //a_piece of cheese&pieces of cheese
                    case 3: //an_apple
                    case 4: //an_ear of corn&ears of corn
                    case 5: //a_loaf of bread&loaves of bread
                    case 6: //a_fish&fish
                    case 7: //some_popcorn&bunches of popcorn
                    case 8: //a_pastry&pastries

                    case 0xA: //a_honeycomb
                    case 0xE: //some_meat-on-a-stick&pieces of meat-on-a-stick
                    case 0xF: //a_nutritious wafer
                        EatFood(obj, !WorldObject); break;
                    case 0xB: //a_bottle of ale&bottles of ale
                    case 0xC: //a_bottle of water&bottles of water
                    case 0xD: //a_bottle of wine&bottles of wine
                        DrinkLiquid(obj, !WorldObject); break;
                    case 9: //a_mushroom 
                        TakeShrooms(obj, !WorldObject);
                        break;
                }
                return true;
            }
            else
            {
                switch (obj.classindex)
                {
                    case 0:// a_piece of meat&pieces of meat
                    case 1:// a_loaf of bread&loaves of bread
                    case 2:// a_piece of cheese&pieces of cheese
                    case 3:// an_apple
                    case 4:// an_ear of corn&ears of corn
                    case 5:// a_loaf of bread&loaves of bread
                    case 6:// a_fish&fish
                    case 7:// some_popcorn&bunches of popcorn

                    case 9:// a_toadstool
                        EatToadstool(obj, !WorldObject); break;
                    case 0xA:// a_bottle of ale&bottles of ale
                        DrinkLiquid(obj, !WorldObject); break;
                    case 0xB:// a_red potion
                    case 0xC:// a_green potion
                        potion.QuaffPotion(obj, !WorldObject);
                        break;
                    case 0xD:// a_bottle of water&bottles of water
                    case 0xE:// a_flask of port&flasks of port
                        DrinkLiquid(obj, !WorldObject); break;
                    case 0xF:// a_bottle of wine&bottles of wine
                        uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x7F)); break;
                    case 8:// a_mushroom
                        TakeShrooms(obj, !WorldObject); break;
                }
            }
            return true;
        }

        /// <summary>
        /// Handles eating of regular food items.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="UsedFromInventory"></param>
        static void EatFood(uwObject obj, bool UsedFromInventory)
        {
            var nutrition = foodObjectDat.nutrition(obj.item_id);
            if (nutrition >= 0)
            {
                if (playerdat.play_hunger + nutrition > 255)
                {
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, GameStrings.str_you_are_too_full_to_eat_that_now_));
                    return;
                }
                else
                {
                    //update hunger
                    playerdat.ChangeHunger(nutrition);

                    PlayEatingSounds();
                    TasteDescriptionString(obj);

                    int foodItemID = obj.item_id;
                    ObjectCreator.Consume(obj, UsedFromInventory);
                    if (_RES == GAME_UW2)
                    {
                        SpawnLeftOvers(foodItemID);
                    }
                }
            }
        }

        /// <summary>
        /// Generates the text describing how the food tasted
        /// </summary>
        /// <param name="obj"></param>
        private static void TasteDescriptionString(uwObject obj)
        {
            var objname = "That " + GameStrings.GetName(obj.item_id, article: false, plural: false);//get_name(0, 0)
            // Build the string for the taste description.
            //Taste string: (rand() * 20 / 0x8000 + quality) >> 4
            var taste = (obj.quality + Rng.r.NextScaled(0x14)) >> 4;
            if (taste > 4) { taste = 4; }

            //;  tasted putrid. \n
            //;  tasted a little rancid. \n
            //;  tasted kind of bland. \n
            //;  tasted pretty good. \n
            //;  tasted great. \n
            if (ZhText.Enabled)
            {//the translations say how it tastes with the food's name before them
                uimanager.AddToMessageScroll(ZhText.Compose(ZhText.ObjectName(obj.item_id), GameStrings.GetDisplayString(1, (_RES == GAME_UW2 ? 0xBB : 0xAC) + taste)));
            }
            else if (_RES == GAME_UW2)
            {
                uimanager.AddToMessageScroll($"{objname}{GameStrings.GetString(1, 0xBB + taste)}");
            }
            else
            {
                uimanager.AddToMessageScroll($"{objname}{GameStrings.GetString(1, 0xAC + taste)}");
            }
        }


        /// <summary>
        /// Play eating sound. (sound played depends on how hungry the player is)  
        /// </summary>
        private static void PlayEatingSounds()
        {
            if (_RES != GAME_UW2)
            {//UW1 eats silently
                return;
            }
            if (playerdat.play_hunger >= 192)
            {
                Debug.Print("Play Sound Effect 0x25 (eating)");
                UWsoundeffects.PlaySoundEffectAtAvatar(0x25,0x40,0);
            }
            else
            {
                if (playerdat.play_hunger > 90)
                {
                    Debug.Print("Play Sound Effect 0x1F (eating)");
                    UWsoundeffects.PlaySoundEffectAtAvatar(0x1F,0x40,0);
                }
                else
                {
                    Debug.Print("Play Sound Effect 0x21h (eating)");
                    UWsoundeffects.PlaySoundEffectAtAvatar(0x21,0x40,0);
                }
            }
        }


        /// <summary>
        /// Spawns leftovers in the players hand after eating certain foods and drinks
        /// </summary>
        /// <param name="foodItemId"></param>
        private static void SpawnLeftOvers(int foodItemId)
        {
            var leftOvers = -1;
            if (_RES == GAME_UW2)
            {
                switch (foodItemId)
                {
                    case 0xB0://piece of meat (only this one leaves a bone)
                        leftOvers = 0xC5; break;
                    case 0xBA://candle
                        leftOvers = 0xD2; break;
                    case 0xBB: //bottles
                    case 0xBC:
                    case 0xBD:
                        leftOvers = 0x13D; break;
                    case 0xBE://meat on a stick
                        leftOvers = 0x13E; break;                        
                    default:
                        //leftOvers = 0;
                        break;
                }
            }
            if (leftOvers != -1)
            {
                //create the left overs in the players hand
                ObjectCreator.SpawnObjectInHand(leftOvers);
            }
        }



        /// <summary>
        /// Handles the drinking of alcohol and water(?)
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="UsedFromInventory"></param>
        static void DrinkLiquid(uwObject obj, bool UsedFromInventory)
        {//UseFood with a negative nutrition: the taste line, then alcohol only for -0x7F < nutrition < -1 (not water)
            var nutrition = foodObjectDat.nutrition(obj.item_id);
            if (_RES == GAME_UW2)
            {
                UWsoundeffects.PlaySoundEffectAtAvatar(0x1E, 0x40, 0);
            }
            //taste: UW2 0xFC water, 0xFD wine, 0xFE ale; UW1 0xED water, 0xEE port, 0xEF ale
            int taste;
            if (_RES == GAME_UW2)
            {
                taste = obj.item_id == 0xBB ? 0xFE : obj.item_id == 0xBD ? 0xFD : 0xFC;
            }
            else
            {
                taste = obj.item_id == 0xBA ? 0xEF : obj.item_id == 0xBE ? 0xEE : 0xED;
            }
            uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, taste));
            if ((nutrition < -1) && (nutrition > -0x7F))
            {
                playerdat.intoxication = System.Math.Min(playerdat.intoxication - nutrition, 0x3F);//to at most 0x3F
                //a strength check against how drunk
                switch (playerdat.SkillCheck(playerdat.STR, playerdat.intoxication))
                {
                    case playerdat.SkillCheckResult.CritFail:
                        {//As the alcohol hits you, you stumble and collapse into sleep.
                            uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, _RES == GAME_UW2 ? 0x100 : 0xF1));
                            sleep.Sleep(-2);
                            if (playerdat.play_hp > 0)
                            {//You wake feeling somewhat unstable but better
                                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, _RES == GAME_UW2 ? 0x102 : 0xF3));
                                motion.SetScreenShake(TypeOfShake: 0x40, duration: (byte)(0xA + (playerdat.intoxication / 6)));
                            }
                            break;
                        }
                    case playerdat.SkillCheckResult.CritSucess:
                        {//The drink makes you feel a little better for now.
                            uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, _RES == GAME_UW2 ? 0x101 : 0xF2));
                            playerdat.play_hp = (byte)System.Math.Min(playerdat.play_hp + 2, playerdat.max_hp);
                            break;
                        }
                    case playerdat.SkillCheckResult.Fail:
                        {
                            motion.SetScreenShake(TypeOfShake: 0x40, duration: (byte)(playerdat.intoxication / 6));
                            break;
                        }
                }
            }
            var foodItemID = obj.item_id;
            ObjectCreator.Consume(obj, UsedFromInventory);
            if (_RES == GAME_UW2)
            {
                SpawnLeftOvers(foodItemID);
            }
        }

        /// <summary>
        /// UW1 toadstool: taste 0xE7, then poison to at least 4, or 2 more up to 0xD.
        /// </summary>
        static void EatToadstool(uwObject obj, bool UsedFromInventory)
        {
            var nutrition = foodObjectDat.nutrition(obj.item_id);
            if ((nutrition > 0) && (playerdat.play_hunger + nutrition > 255))
            {
                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, GameStrings.str_you_are_too_full_to_eat_that_now_));
                return;
            }
            if (nutrition > 0)
            {
                playerdat.ChangeHunger(nutrition);
            }
            uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0xE7));
            if (playerdat.play_poison < 4)
            {
                playerdat.play_poison = 4;
            }
            else if (playerdat.play_poison < 0xD)
            {
                playerdat.play_poison = (byte)(playerdat.play_poison + 2);
            }
            ObjectCreator.Consume(obj, UsedFromInventory);
        }

        /// <summary>
        /// Applies the effects of mushroom consumption.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="UsedFromInventory"></param>
        static void TakeShrooms(uwObject obj, bool UsedFromInventory)
        {
            if (playerdat.SkillCheck(playerdat.INT, 0x14) != playerdat.SkillCheckResult.Fail)
            {//anything but a plain failure restores a flat 0 to 2 mana
                playerdat.ManaRegenChange(-Rng.r.NextScaled(3));
            }

            playerdat.shrooms = Math.Min(3, playerdat.shrooms + 1);
            uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, GameStrings.str_the_mushroom_causes_your_head_to_spin_and_your_vision_to_blur_));
            ObjectCreator.Consume(obj, UsedFromInventory);
            playerdat.PlayerStatusUpdate();
        }

        /// <summary>
        /// Special cases where eating non-regular food objects have an effect caused by dragging them to the mouth
        /// </summary>
        /// <param name="obj"></param>
        public static bool SpecialFoodCases(uwObject obj, bool UsedFromInventory)
        {
            var tastestring = 0;
            var nutrition = 0;
            if (_RES == GAME_UW2)
            {//uw2

                switch (obj.item_id)
                {
                    case 0x92://Candle
                        tastestring = 245;
                        break;
                    case 0xCE://throny flower
                        tastestring = 251;
                        nutrition = 4;
                        break;
                    case 0x110://eyeball
                        tastestring = 246; // i think
                        break;
                    case 0xCF:
                    case 0x114://plant
                        tastestring = 248;
                        nutrition = 23;
                        break;
                    case 0x125://leeches
                        tastestring = 244;
                        break;
                    default:
                        return false; //cannot be eaten
                }
            }
            else
            {//uw1
                switch (obj.item_id)
                {
                    case 0x92://Candle
                        tastestring = 230;
                        break;
                    case 0xCE: //plant
                        tastestring = 236;
                        nutrition = 4;
                        break;
                    case 0xCF:  //plant
                        tastestring = 233;
                        nutrition = 23;
                        break;
                    case 0xD9://rotworm corpse
                        tastestring = 234;
                        nutrition = 4;
                        break;
                    case 0x11B: //rotworm stew
                        tastestring = 235;
                        nutrition = 64;
                        break;
                    case 0x125://leech
                        tastestring = 229;
                        break;
                    default:
                        return false;
                }
            }
            if (nutrition == 0)
            {
                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, tastestring));
            }
            else
            {
                if (playerdat.play_hunger + nutrition <= 255)
                {
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, tastestring));
                    playerdat.ChangeHunger(nutrition);
                }
                else
                {
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, GameStrings.str_you_are_too_full_to_eat_that_now_));
                    return true;
                }
            }

            if ((_RES == GAME_UW2) && (obj.item_id == 0x114))
            {
                if (!playerdat.DreamingInVoid)
                {
                    playerdat.DreamPlantCounter = (2 + Rng.r.Next(4)) & 0x7;
                }
            }
           
            var foodItemID = obj.item_id;
            
            ObjectCreator.Consume(obj, UsedFromInventory);
            
            if (_RES == GAME_UW2)
            {
                SpawnLeftOvers(foodItemID);
            }
            return true;
        }


        public static bool IsFood(uwObject obj)
        {

            if (obj.majorclass == 2)
            {
                if (_RES == GAME_UW2)
                {
                    switch (obj.classindex)
                    {
                        case 0: //a_piece of meat&pieces of meat
                        case 1: //a_piece of meat&pieces of meat
                        case 2: //a_piece of cheese&pieces of cheese
                        case 3: //an_apple
                        case 4: //an_ear of corn&ears of corn
                        case 5: //a_loaf of bread&loaves of bread
                        case 6: //a_fish&fish
                        case 7: //some_popcorn&bunches of popcorn
                        case 8: //a_pastry&pastries
                        case 0xA: //a_honeycomb
                        case 0xE: //some_meat-on-a-stick&pieces of meat-on-a-stick
                        case 0xF: //a_nutritious wafer
                        case 0xB: //a_bottle of ale&bottles of ale
                        case 0xC: //a_bottle of water&bottles of water
                        case 0xD: //a_bottle of wine&bottles of wine
                        case 9: //a_mushroom  //this needs to do a shrooms skill check on it
                            return true;
                    }
                }
                else
                {
                    switch (obj.classindex)
                    {
                        case 0:// a_piece of meat&pieces of meat
                        case 1:// a_loaf of bread&loaves of bread
                        case 2:// a_piece of cheese&pieces of cheese
                        case 3:// an_apple
                        case 4:// an_ear of corn&ears of corn
                        case 5:// a_loaf of bread&loaves of bread
                        case 6:// a_fish&fish
                        case 7:// some_popcorn&bunches of popcorn
                        case 9:// a_toadstool                       
                        case 0xA:// a_bottle of ale&bottles of ale
                        case 0xB:// a_red potion
                        case 0xC:// a_green potion                      
                        case 0xD:// a_bottle of water&bottles of water
                        case 0xE:// a_flask of port&flasks of port                       
                        case 0xF:// a_bottle of wine&bottles of wine                       
                        case 8:// a_mushroom
                            return true;
                    }
                }
            }


            if (_RES == GAME_UW2)
            {//uw2
                switch (obj.item_id)
                {
                    case 0x92://Candle                     
                    case 0xCE://throny flower
                    case 0x110://eyeball
                    case 0xCF:
                    case 0x114://plant
                    case 0x125://leeches
                        return true;
                }
            }
            else
            {//uw1
                switch (obj.item_id)
                {
                    case 0x92://Candle
                    case 0xCE: //plant                     
                    case 0xCF:  //plant
                    case 0xD9://rotworm corpse                       
                    case 0x11B: //rotworm stew                      
                    case 0x125://leech
                        return true;
                }
            }
            return false;
        }       

    }//end class
}//end namespace