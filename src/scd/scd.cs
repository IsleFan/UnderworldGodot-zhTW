using System.Diagnostics;

namespace Underworld
{
    /// <summary>
    /// What the hell is SCD.ARK? It's an event system. Data is read from scd.ark at the end of conversations, level transitions, by a hack trap, when a block of time is added to the clock and when Killorn is crashing.
    /// </summary>
    public partial class scd : UWClass
    {
        static int BlockIdentifier;
        /// <summary>
        /// Starts the SCD process.
        /// Checks if scd_data is loaded and check each xclock to see if events need to be ran based on various conditions.
        /// </summary>
        public static void ProcessSCDArk(int mode)
        {
            Debug.Print("Starting to process SCU.ARK");
            if (scd_data == null)
            {
                scd_data = new UWBlock[0x10];
            }
            int error_var2 = 0;//flags if an error has occurred and stop execution.
            for (int blockno = 0; blockno < 0x10; blockno++)
            {
                BlockIdentifier = blockno;
                Debug.Print($"Processing SCD Block {blockno}");
                var si_xclock = playerdat.GetXClock(blockno);
                if (scd_data[blockno] == null)
                {
                    if (!LoadSCDBlock(blockno))
                    {
                        Debug.Print($"Error loading scd block {blockno}");
                        return; //stop                     
                    }
                }

                scd_data[blockno].Data[2] = (byte)blockno;

                if (blockno == 0)
                {
                    error_var2 = ProcessBlock0(
                        currentblock: scd_data[blockno].Data,
                        arg0_xclock: playerdat.GetXClock(0),
                        NoOfMaps: 72,
                        mode_arg4: mode);
                }
                else
                {
                    error_var2 = ProcessGeneralBlock(
                        currentblock: scd_data[blockno].Data,
                        XClockValue: si_xclock,
                        mode: mode);
                }
                if (error_var2 != 0)
                {
                    return;
                }
                //The block stays loaded: scd_data is this game's SCD.ARK as it now stands (the
                //original writes a changed block back to the save directory's SCD.ARK at once,
                //Sched_Save), and ScdArkWriter writes it out with the save. Dropping an unchanged
                //block here used to reload it from the save folder next time, losing what earlier
                //calls had changed (deleted rows, the levels' clocks and next rows).
            }
        }

        /// <summary>
        /// UW2's hour (every 60 checks, 20 minutes of game time): the time of day, X clock 0, steps
        /// on round its 72 steps, and block 0, the daily schedule, runs up to it (duration_check).
        /// </summary>
        public static void AdvanceTimeOfDay()
        {
            if (_RES != GAME_UW2)
            {
                return;
            }
            var time = (playerdat.GetXClock(0) + 1) % 72;
            playerdat.SetXClock(0, time);
            scd_data ??= new UWBlock[0x10];
            if (scd_data[0] == null && !LoadSCDBlock(0))
            {
                return;
            }
            scd_data[0].Data[2] = 0;
            ProcessBlock0(scd_data[0].Data, time, 72, 1);
        }

        static int Word(byte[] block, int offset) => block[offset] | (block[offset + 1] << 8);

        static void SetWord(byte[] block, int offset, int value)
        {
            block[offset] = (byte)value;
            block[offset + 1] = (byte)(value >> 8);
        }

        //A block as stored in SCD.ARK: the row count (word), the block number, a spare byte, 80
        //clocks of two words (the time the level has reached, the next row it has not run), then
        //16-byte rows starting with their time (word).
        static int Rows(byte[] block) => Word(block, 0);
        static int ClockTime(byte[] block) => Word(block, 4 + playerdat.dungeon_level * 4);
        static void SetClockTime(byte[] block, int time) => SetWord(block, 4 + playerdat.dungeon_level * 4, time);
        static int NextRow(byte[] block) => Word(block, 6 + playerdat.dungeon_level * 4);
        static void SetNextRow(byte[] block, int row) => SetWord(block, 6 + playerdat.dungeon_level * 4, row);
        static int RowTime(byte[] block, int row) => Word(block, 324 + row * 16);

        /// <summary>
        /// Block 0, the time of day: a cyclic clock of NoOfMaps steps (Sched_WrapTime). The time is
        /// taken modulo the span; when it has wrapped, the level first runs to the end of the day
        /// and then starts again from 0.
        /// </summary>
        static int ProcessBlock0(byte[] currentblock, int arg0_xclock, int NoOfMaps, int mode_arg4)
        {
            var time = arg0_xclock % NoOfMaps;
            if (ClockTime(currentblock) > time)
            {
                var result = ProcessGeneralBlock(currentblock, NoOfMaps - 1, mode_arg4);
                if (result != 0)
                {
                    return result;
                }
                SetClockTime(currentblock, 0);
                SetNextRow(currentblock, 0);
            }
            return ProcessGeneralBlock(currentblock, time, mode_arg4);
        }

        /// <summary>
        /// Sets the current level's clock on the block to XClockValue and, with mode set, runs the
        /// rows that fall due (Sched_SetTime). A clock going back starts again from row 0 when
        /// running, and otherwise only moves the level's next row back.
        /// </summary>
        static int ProcessGeneralBlock(byte[] currentblock, int XClockValue, int mode)
        {
            var old = ClockTime(currentblock);
            SetClockTime(currentblock, XClockValue);
            if (old == XClockValue)
            {
                return 0;
            }
            if (old > XClockValue)
            {
                if (mode == 0)
                {
                    return SetSomeValuesInSCDRows_ovr151_D3(currentblock);
                }
                SetNextRow(currentblock, 0);
            }
            return FindSCDRowsToExecute(currentblock, mode);
        }

        /// <summary>
        /// The clock went back without running: moves the level's next row back to the first row
        /// after the clock time (SetSomeValuesInSCDRows_ovr151_D3).
        /// </summary>
        static int SetSomeValuesInSCDRows_ovr151_D3(byte[] currentblock)
        {
            //(the original starts at the next row itself, which past the last row reads beyond the block)
            var i = System.Math.Min(NextRow(currentblock), System.Math.Max(Rows(currentblock) - 1, 0));
            while (i > 0)
            {
                if (RowTime(currentblock, i) <= ClockTime(currentblock))
                {
                    i++;
                    break;
                }
                i--;
            }
            SetNextRow(currentblock, i);
            return 0;
        }

        /// <summary>
        /// Runs (mode set) or skips the rows from the level's next row up to its clock time
        /// (FindSCDRowsToExecute_ovr151_0). An event's result: 0 it ran, 4 its row was deleted (so
        /// the same index is looked at again), 5 nothing to do; anything else stops here and is
        /// returned. Block 15 is scanned from row 0 every time.
        /// </summary>
        static int FindSCDRowsToExecute(byte[] currentblock, int mode)
        {
            var i = NextRow(currentblock);
            if (currentblock[2] == 0xF)
            {
                i = 0;
            }
            while (i < Rows(currentblock))
            {
                if (RowTime(currentblock, i) > ClockTime(currentblock))
                {
                    break;
                }
                if (mode != 0)
                {
                    var result = ProcessSCDEventRow(currentblock, 324 + (i * 16));
                    switch (result)
                    {
                        case 4://the row deleted itself: the next row has moved into its place
                            i--;
                            break;
                        case 0:
                        case 5:
                            break;
                        default:
                            SetNextRow(currentblock, i);
                            return result;
                    }
                }
                i++;
            }
            SetNextRow(currentblock, i);
            return 0;
        }


        /// <summary>
        /// Calls the event based on player being in the matching level, world or any levels
        /// </summary>
        /// <param name="currentblock"></param>
        /// <param name="eventOffset"></param>
        /// <returns></returns>
        static int ProcessSCDEventRow(byte[] currentblock, int eventOffset)
        {            
            if (
                (currentblock[eventOffset + 2] == playerdat.dungeon_level)
                || (currentblock[eventOffset + 2] == 0xFF)
                || (currentblock[eventOffset + 2] - 246 == playerdat.CurrentWorld)
                )
            {
                Debug.Print($"Process Event Row at Offset {eventOffset}  #{(eventOffset-324)/16}");
                //DoEvent = true;
                // var var1 = 1;
                // var var2 = 1;

                if ((sbyte)(currentblock[eventOffset + 4]) >= 0)
                {
                    if ((sbyte)(currentblock[eventOffset + 4]) <0xC)
                    {                      
                        var var2 = RunSCDFunction(
                            currentblock: currentblock,
                            eventOffset: eventOffset);
                        
                        if (currentblock[eventOffset + 3] != 0)
                        {
                            Debug.Print($"{eventOffset} to be deleted!");
                            DeleteRow(currentblock, eventOffset);
                            return 4;//return row has been deleted.
                        }
                        else
                        {
                            return var2;//result of function call.
                        }
                    }
                    else
                    {
                        return 6; //error stops processing
                    }
                }
                else
                {
                    return 5;//process next
                }
            }
            else
            {
                return 5;//process next
            }
        }
    }//end class
}//end namespace