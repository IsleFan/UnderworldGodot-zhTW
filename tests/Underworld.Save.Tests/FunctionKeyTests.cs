using System;
using Underworld;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// F1 to F6 select the interaction icons in the order they stand on each game's screen (PLAYER.C's key table): UW1
/// options, talk, get, look, fight, use from the top; UW2 fight, use, get, talk, look, options.
/// </summary>
[Collection("UWClassState")]
public class FunctionKeyTests : IDisposable
{
    private readonly byte _origRes = UWClass._RES;

    public void Dispose()
    {
        UWClass._RES = _origRes;
    }

    [Theory]
    [InlineData(UWClass.GAME_UW1, 0, uimanager.InteractionModes.ModeOptions)]
    [InlineData(UWClass.GAME_UW1, 1, uimanager.InteractionModes.ModeTalk)]
    [InlineData(UWClass.GAME_UW1, 4, uimanager.InteractionModes.ModeAttack)]
    [InlineData(UWClass.GAME_UW1, 5, uimanager.InteractionModes.ModeUse)]
    [InlineData(UWClass.GAME_UW2, 0, uimanager.InteractionModes.ModeAttack)]
    [InlineData(UWClass.GAME_UW2, 1, uimanager.InteractionModes.ModeUse)]
    [InlineData(UWClass.GAME_UW2, 2, uimanager.InteractionModes.ModePickup)]
    [InlineData(UWClass.GAME_UW2, 3, uimanager.InteractionModes.ModeTalk)]
    [InlineData(UWClass.GAME_UW2, 4, uimanager.InteractionModes.ModeLook)]
    [InlineData(UWClass.GAME_UW2, 5, uimanager.InteractionModes.ModeOptions)]
    public void Function_keys_follow_the_icons_on_screen(byte res, int key, uimanager.InteractionModes icon)
    {
        UWClass._RES = res;
        Assert.Equal(icon, Controls.FunctionKeyIcon(key));
    }
}
