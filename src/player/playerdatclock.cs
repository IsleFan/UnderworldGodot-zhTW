using System.Diagnostics;

namespace Underworld
{
    //Clock/time player data
    public partial class playerdat : Loader
    {


        /// <summary>
        /// The value of the clock in the player data
        /// </summary>
        public static int ClockValue
        {
            get
            {
                if (_RES == GAME_UW2)
                {
                    return GetAt32(0x36A);
                }
                else
                {
                    return GetAt32(0xCF);
                }
            }
            set
            {
                if (_RES==GAME_UW2)
                {
                    SetAt32(0x36A, value);
                }
                else
                {
                    SetAt32(0xCF, value);
                }
            }
        }

        public static int game_time
        {
            get
            {
                return ClockValue / 0x3BC4;
            }
        }

        public static int game_mins
        {
            get
            {
                return game_time % 0x5A0;
            }
        }

        public static int game_days
        {
            get
            {
                return ClockValue / 0x1502e80;
            }
        }


        /// <summary>
        /// Hour for pocket watch
        /// </summary>
        public static int TwelveHourClock
        {
            get
            {
                return (ClockValue / 0xE1000) % 12;
            }
        }

        /// <summary>
        /// Minute for pocket watch
        /// </summary>
        public static int Minute
        {
            get
            {
                return (ClockValue / 0x3C00) % 60;
            }
        }

        /// <summary>
        /// Lets <paramref name="span"/> seconds of game time pass, as sleeping, repairing or
        /// coming back from death does. The clock (1/256 seconds) moves on; in UW2 the time of
        /// day (X clock 0, 72 steps of 20 minutes) is set from it and every schedule in SCD.ARK
        /// catches up (pass_time). UW1 only moves the clock.
        /// </summary>
        public static void AdvanceTime(int span)
        {
            Debug.Print ($"Advance time {span}");
            ClockValue += span << 8;
            if (_RES == GAME_UW2)
            {
                SetXClock(0, (int)((uint)ClockValue / 0x4B000 % 72));
                scd.ProcessSCDArk(1);
            }
        }
    }  //end class
}//end namespace