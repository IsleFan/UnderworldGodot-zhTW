using System;
using Underworld;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// The active spells and the flasks follow the original's rules: dispel_spell (PLAYTIME.C) moves the last
/// active spell into the slot it frees rather than shifting the others down, and set_screen_frame (PANELS.C)
/// shows hit points and mana as whole twelfths of their maximum, at most 12 and 0 with no maximum.
/// </summary>
[Collection("UWClassState")]
public class ActiveSpellPanelTests : IDisposable
{
    private readonly byte[] _origPdat = playerdat.pdat;
    private readonly byte _origRes = UWClass._RES;

    public void Dispose()
    {
        playerdat.pdat = _origPdat;
        UWClass._RES = _origRes;
    }

    private static void ThreeSpells(byte res)
    {
        UWClass._RES = res;
        playerdat.pdat = new byte[0x400];
        playerdat.SetSpellEffect(0, 0x13, 20);//A
        playerdat.SetSpellEffect(1, 0x24, 30);//B
        playerdat.SetSpellEffect(2, 0x35, 40);//C
        playerdat.ActiveSpellEffectCount = 3;
    }

    [Theory]
    [InlineData(UWClass.GAME_UW1)]
    [InlineData(UWClass.GAME_UW2)]
    public void Dispelling_the_first_spell_moves_the_last_one_into_its_slot(byte res)
    {
        ThreeSpells(res);
        playerdat.CancelEffect(0);
        Assert.Equal(2, playerdat.ActiveSpellEffectCount);
        Assert.Equal(0x35, playerdat.GetEffectClass(0));//C, with its duration
        Assert.Equal(40, playerdat.GetEffectStability(0));
        Assert.Equal(0x24, playerdat.GetEffectClass(1));//B stays where it was
        Assert.Equal(0, playerdat.GetEffectClass(2));
        Assert.Equal(0, playerdat.GetEffectStability(2));
    }

    [Fact]
    public void Dispelling_the_last_spell_only_empties_its_slot()
    {
        ThreeSpells(UWClass.GAME_UW2);
        playerdat.CancelEffect(2);
        Assert.Equal(2, playerdat.ActiveSpellEffectCount);
        Assert.Equal(0x13, playerdat.GetEffectClass(0));
        Assert.Equal(0x24, playerdat.GetEffectClass(1));
        Assert.Equal(0, playerdat.GetEffectClass(2));
    }

    [Theory]
    [InlineData(5, 10, 6)]//half
    [InlineData(1, 13, 0)]//whole twelfths only, rounded down
    [InlineData(25, 20, 12)]//more than the maximum shows a full flask
    [InlineData(7, 0, 0)]//no maximum shows an empty flask instead of dividing by zero
    public void Flask_level_is_whole_twelfths_of_the_maximum(int value, int max, int level)
    {
        Assert.Equal(level, uimanager.FlaskLevel(value, max));
    }
}
