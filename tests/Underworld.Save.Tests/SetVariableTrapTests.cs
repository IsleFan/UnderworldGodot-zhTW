using System;
using System.IO;
using Underworld;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// UW1's set variable trap with z 0 works on a quest flag: the trap's packed operand,
/// ((quality &lt;&lt; 5 | owner) &lt;&lt; 3) | fine y, is the flag's number (TRIGGER.C UseTrap,
/// TRAP_SET_VARIABLE); heading 1 clears it, 5 toggles it, anything else sets it.
/// Level 3 uses it to set quest 7.
/// </summary>
[Collection("UWClassState")]
public class SetVariableTrapTests : IDisposable
{
    private readonly string _origBasePath;
    private readonly byte _origRes;

    public SetVariableTrapTests()
    {
        _origBasePath = UWClass.BasePath;
        _origRes = UWClass._RES;
        UWClass.BasePath = Path.Combine(TestData.UW2GogRoot, "UW1");
        UWClass._RES = UWClass.GAME_UW1;
        playerdat.InitEmptyPlayer("TestGronk");
    }

    public void Dispose()
    {
        UWClass.BasePath = _origBasePath;
        UWClass._RES = _origRes;
    }

    private static uwObject QuestTrap(int quest, int heading)
    {
        var trap = new uwObject { DataBuffer = new byte[27], PTR = 0 };
        trap.zpos = 0;
        trap.quality = (short)(quest >> 8);
        trap.owner = (short)((quest >> 3) & 0x1F);
        trap.ypos = (short)(quest & 7);
        trap.heading = (short)heading;
        return trap;
    }

    [GameDataFact]
    public void ZeroZ_SetsClearsAndTogglesTheNumberedQuestFlag()
    {
        a_set_variable_trap.Activate(QuestTrap(7, heading: 0));
        Assert.Equal(1, playerdat.GetQuest(7));
        //Before the fix the operand was taken as 1 << 7, quest 128, and quest 7 never changed.
        for (int q = 0; q < 7; q++)
        {
            Assert.Equal(0, playerdat.GetQuest(q));
        }

        a_set_variable_trap.Activate(QuestTrap(7, heading: 5));
        Assert.Equal(0, playerdat.GetQuest(7));
        a_set_variable_trap.Activate(QuestTrap(7, heading: 5));
        Assert.Equal(1, playerdat.GetQuest(7));
        a_set_variable_trap.Activate(QuestTrap(7, heading: 1));
        Assert.Equal(0, playerdat.GetQuest(7));
    }
}
