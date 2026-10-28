using System;

namespace Underworld
{
    /// <summary>
    /// For managing UW2 timer triggers
    /// A timer trigger runs every zpos frames
    /// It takes 127 frames or so to run each timer.
    /// </summary>
    public class timers : UWClass
    {
        public static long FrameNo = 0;
        public static int GetTimer(int index)
        {
            return (int)Loader.getAt(UWTileMap.current_tilemap.lev_ark_block.Data, 0x7D88 + (index * 2), 16);
        }

        public static void SetTimer(int index, int value)
        {
           Loader.setAt(UWTileMap.current_tilemap.lev_ark_block.Data, 0x7D88 + (index * 2), 16, value);
        }

        /// <summary>
        /// Takes a deleted timer trigger off the list: the last entry moves into its place (rem_timer_obj).
        /// </summary>
        public static void RemoveTimerTrigger(int index)
        {
            var count = NoOfTimerTriggers;
            for (int i = 0; i < count; i++)
            {
                if (GetTimer(i) == index)
                {
                    SetTimer(i, GetTimer(count - 1));
                    SetTimer(count - 1, 0);
                    return;
                }
            }
        }

        public static int NoOfTimerTriggers
        {
            get
            {
                int counter = 0;
                for (int i = 0; i < 64; i++)
                {
                    var index = GetTimer(i);
                    if (index != 0)
                    {
                        counter++;
                    }
                    else
                    {
                        return counter;
                    }
                }
                return counter;
            }
        }
        public static void RunTimerTriggers(int delta = 1)
        {
            if (_RES != GAME_UW2) 
            { 
                return;
            }
            if (playerdat.FreezeTimeEnchantment)
            {
                return;
            }
            //loop all the timers in the data
            var counter = NoOfTimerTriggers;
            for (int t = 0; t < counter; t++)
            {
                var tIndex = GetTimer(t);
                if (tIndex != 0)
                {
                    var tTrigger = UWTileMap.current_tilemap.LevelObjects[tIndex];
                    //update_animobj: this gets the number of times the trigger would have run by this frame,
                    //and subtracts the number of times the trigger would have ran by the last frame. 
                    //The difference is the number of times the trigger needs to run in this frame.
                    var noOfRuns = ((FrameNo + delta) / (tTrigger.zpos + 1)) - (FrameNo / (tTrigger.zpos + 1));
                    //only while the player is within 8 squares of the trigger's target square (its quality and owner).
                    //(The original also counts the roaming eye's square; not done here.)
                    if ((noOfRuns > 0)
                        && (Math.Abs(playerdat.playerObject.tileX - tTrigger.quality) < 8)
                        && (Math.Abs(playerdat.playerObject.tileY - tTrigger.owner) < 8))
                    {
                        while (noOfRuns > 0)
                        {
                            trigger.RunTrigger(
                                character: 1,
                                ObjectUsed: null,
                                TriggerObject: tTrigger,
                                triggerType: (int)triggerObjectDat.triggertypes.TIMER,
                                objList: UWTileMap.current_tilemap.LevelObjects);
                            noOfRuns--;
                        }
                    }
                }
            }
            FrameNo = (FrameNo + delta) & 0x7F;//advance the frame count by the delta; it wraps at 0x80 (timer_tick)
        }
    }//end class
}//end namespace