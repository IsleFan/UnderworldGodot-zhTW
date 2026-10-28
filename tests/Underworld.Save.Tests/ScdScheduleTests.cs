using System;
using System.IO;
using Underworld;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// The UW2 schedules (SCD.ARK) run against the original's rules (SCHEDULE.C): rows fall due as
/// a block's clock for the current level passes their time, a one-off row deletes itself
/// without stopping the rows after it, and a row timed 65535 never runs.
///
/// The blocks are built here rather than read from SCD.ARK so that every row is a variable
/// operation (event 9) on a game variable: the effect is visible in player data and nothing
/// touches the world or the engine.
/// </summary>
[Collection("UWClassState")]
public class ScdScheduleTests : IDisposable
{
    private readonly string _origBasePath;
    private readonly byte _origRes;
    private readonly UWBlock[] _origScdData;

    public ScdScheduleTests()
    {
        _origBasePath = UWClass.BasePath;
        _origRes = UWClass._RES;
        _origScdData = scd.scd_data;

        UWClass.BasePath = Path.Combine(TestData.UW2GogRoot, "UW2");
        UWClass._RES = UWClass.GAME_UW2;
        playerdat.InitEmptyPlayer("TestGronk");
        scd.scd_data = new UWBlock[0x10];
        for (int b = 0; b < 0x10; b++)
        {
            scd.scd_data[b] = Block();
        }
    }

    public void Dispose()
    {
        UWClass.BasePath = _origBasePath;
        UWClass._RES = _origRes;
        scd.scd_data = _origScdData;
    }

    /// <summary>A row that adds 1 to game variable `variable` at `time`.</summary>
    private static (int time, bool once, int variable) Row(int time, int variable, bool once = false)
        => (time, once, variable);

    private static UWBlock Block(params (int time, bool once, int variable)[] rows)
    {
        var data = new byte[324 + rows.Length * 16];
        data[0] = (byte)rows.Length;
        data[1] = (byte)(rows.Length >> 8);
        for (int r = 0; r < rows.Length; r++)
        {
            var o = 324 + r * 16;
            data[o] = (byte)rows[r].time;
            data[o + 1] = (byte)(rows[r].time >> 8);
            data[o + 2] = 0xFF;                 //any level
            data[o + 3] = (byte)(rows[r].once ? 1 : 0);
            data[o + 4] = 9;                    //variable operation
            data[o + 5] = (byte)rows[r].variable;
            data[o + 7] = 0;                    //add
            data[o + 8] = 1;
        }
        return new UWBlock { Data = data, DataLen = data.Length };
    }

    private static int Word(byte[] data, int offset) => data[offset] | (data[offset + 1] << 8);
    private static int RowCount(int block) => Word(scd.scd_data[block].Data, 0);
    private static int ClockTime(int block) => Word(scd.scd_data[block].Data, 4 + playerdat.dungeon_level * 4);
    private static int NextRow(int block) => Word(scd.scd_data[block].Data, 6 + playerdat.dungeon_level * 4);

    [GameDataFact]
    public void OneOffRow_IsDeleted_AndTheRowsAfterItStillRun()
    {
        scd.scd_data[1] = Block(
            Row(1, variable: 20, once: true),
            Row(1, variable: 21),
            Row(2, variable: 22),
            Row(65535, variable: 23));
        playerdat.SetXClock(1, 2);

        scd.ProcessSCDArk(1);

        //Before the fix the loop stopped at the deleted row, so only variable 20 changed.
        Assert.Equal(1, playerdat.GetGameVariable(20));
        Assert.Equal(1, playerdat.GetGameVariable(21));
        Assert.Equal(1, playerdat.GetGameVariable(22));
        Assert.Equal(0, playerdat.GetGameVariable(23));
        Assert.Equal(3, RowCount(1));
        Assert.Equal(2, ClockTime(1));
        Assert.Equal(2, NextRow(1)); //the 65535 row, now row 2, is next
    }

    [GameDataFact]
    public void RowTimedNever_DoesNotRun_WhenTheClockReachesItsLowByte()
    {
        //Read as a byte, 65535 is 255: the clock reaching 255 used to run it.
        scd.scd_data[1] = Block(Row(65535, variable: 30));
        playerdat.SetXClock(1, 255);

        scd.ProcessSCDArk(1);

        Assert.Equal(0, playerdat.GetGameVariable(30));
        Assert.Equal(0, NextRow(1));
    }

    [GameDataFact]
    public void RunningTwice_DoesNotRepeatRows_NorResetTheTimeOfDay()
    {
        scd.scd_data[1] = Block(Row(1, variable: 40), Row(3, variable: 41));
        playerdat.SetXClock(0, 5);
        playerdat.SetXClock(1, 1);

        scd.ProcessSCDArk(1);
        playerdat.SetXClock(1, 3);
        scd.ProcessSCDArk(1);

        //The block keeps its clock and next row between calls instead of being reloaded.
        Assert.Equal(1, playerdat.GetGameVariable(40));
        Assert.Equal(1, playerdat.GetGameVariable(41));
        //Running the general blocks used to set X clock 0 back to 0.
        Assert.Equal(5, playerdat.GetXClock(0));
    }

    [GameDataFact]
    public void Event4_SetsTheQuestFlag()
    {
        //As SCD.ARK's only event 4 (block 1, row 43): quest 56 set to 1 at time 14, once.
        scd.scd_data[1] = Block(Row(14, variable: 0, once: true));
        var row = scd.scd_data[1].Data;
        row[324 + 4] = 4;
        row[324 + 5] = 56;
        row[324 + 6] = 1;
        playerdat.SetQuest(57, 1); //a neighbour in the same long, left alone
        playerdat.SetXClock(1, 14);

        scd.ProcessSCDArk(1);

        Assert.Equal(1, playerdat.GetQuest(56));
        Assert.Equal(1, playerdat.GetQuest(57));
        Assert.Equal(0, RowCount(1));
    }

    [GameDataFact]
    public void TimeOfDay_RunsTheDailySchedule_AndWrapsAtTheEndOfTheDay()
    {
        scd.scd_data[0] = Block(Row(0, variable: 50), Row(71, variable: 51));
        playerdat.SetXClock(0, 70);
        scd.ProcessSCDArk(1);   //block 0 up to 70: the row at 0 runs
        Assert.Equal(1, playerdat.GetGameVariable(50));

        scd.AdvanceTimeOfDay(); //71
        Assert.Equal(71, playerdat.GetXClock(0));
        Assert.Equal(1, playerdat.GetGameVariable(51));

        scd.AdvanceTimeOfDay(); //0: the day starts again, but a clock set to the time it starts from runs nothing (Sched_SetTime)
        Assert.Equal(0, playerdat.GetXClock(0));
        Assert.Equal(0, ClockTime(0));
        Assert.Equal(1, playerdat.GetGameVariable(50));

        scd.AdvanceTimeOfDay(); //1: the row at 0 runs on the new day
        Assert.Equal(2, playerdat.GetGameVariable(50));
        Assert.Equal(1, playerdat.GetGameVariable(51));
    }
}
