
using System;

namespace Underworld
{
    /// <summary>
    /// Handles RNG calls for the game. Eventually this may replicate the original rng that is based on system time rather than .NET rng.
    /// </summary>
    public class Rng : UWClass
    {
        public static uint Seed;
        const int Multiplier = 0x15A4E35;
        public static Rng r = new();
        //public static Random r_unseeded = new(); //for rng calls where the seed never gets reset

        public Rng()
        {
            InitRng();
        }

        /// <summary>
        /// Returns a random value which is base value offset by the upper and lower range
        /// </summary>
        /// <param name="basevalue"></param>
        /// <param name="lower_offset"></param>
        /// <param name="upper_offset"></param>
        /// <returns></returns>
        public static int RandomOffset(int basevalue, int lower_offset, int upper_offset)
        {//the original's range(): the product is a 16-bit int, as Turbo C's, and the sum is stored in 16 bits
            var rnd = r.NextScaled(upper_offset - lower_offset);
            return (short)(basevalue + (short)(basevalue * (lower_offset + rnd)) / 100);
        }


        /// <summary>
        /// Returns NoOfLoops + ({NoOfLoops}D{DiceRange})
        /// </summary>
        /// <param name="NoOfLoops"></param>
        /// <param name="diceRange"></param>
        /// <returns></returns>
        public static int DiceRoll(int NoOfLoops, int diceRange)
        {
            //var di = diceRange;
            var si = NoOfLoops;
            if (diceRange <= 0)
            {
                return si;
            }
            else
            {
                if (NoOfLoops <= 0)
                {
                    return si;
                }
                else
                {
                    while (NoOfLoops != 0)
                    {
                        si += r.NextScaled(diceRange);//rollem
                        NoOfLoops--;
                    }
                    return si;
                }
            }
        }


        /// <summary>
        /// The unix epoch is found and stored as the RNG seed.
        /// </summary>
        public static void InitRng()
        {
            Seed = (ushort)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & 0xFFFF);
            //Debug.Print($"Rng Seeded to {Seed}");
        }

        /// <summary>
        /// An RNG result is obtained by multipying Seed * Constant Multipler. 
        /// The multiplication result is stored as the next seed.
        /// The max value is mod divided into the (result & 0x7FFF) to get the RNG number
        /// </summary>
        /// <param name="max"></param>
        /// <returns></returns>
        public int Next(int max)
        {//the original's Turbo C rand(): seed = seed * 0x015A4E35 + 1, bits 16 to 30 of the new seed, then % max
            if (max < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max));
            }
            Seed = (uint)(Seed * Multiplier + 1);
            var value = (int)((Seed >> 16) & 0x7FFF);
            return max == 0 ? 0 : value % max;
        }

        /// <summary>
        /// rand() scaled into 0 to max - 1 as (rand() * max) / 0x8000, the form rollem and a few other rolls use.
        /// </summary>
        public int NextScaled(int max)
        {
            var value = Next(0x8000);
            return (int)(((long)value * max) / 0x8000);
        }

        public int Next(int min, int max)
        {
            var res = Next(max-min);
            return min + res;
        }

        void RngTest()
        {
            int[] rngtest = new int[100];
            for (int i = 0; i<4000;i++)
            {
                var res = r.Next(100);
                rngtest[res]++;
            }
        }

    }//end class
}//end namespace