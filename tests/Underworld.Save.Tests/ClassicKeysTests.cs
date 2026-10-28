using Godot;
using Underworld;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// Classic controls are the original's keys (PLAYER.C, PLAYMOVE.C's move_keys): w run, s walk, x back, a and d
/// turn, z and c slide, e and q fly. Modern controls keep their own defaults, so choosing classic does not move a
/// modern player's keys.
/// </summary>
public class ClassicKeysTests
{
    [Theory]
    [InlineData(Controls.Action.Forward, Key.W)]
    [InlineData(Controls.Action.Walk, Key.S)]
    [InlineData(Controls.Action.Back, Key.X)]
    [InlineData(Controls.Action.TurnLeft, Key.A)]
    [InlineData(Controls.Action.TurnRight, Key.D)]
    [InlineData(Controls.Action.StrafeLeft, Key.Z)]
    [InlineData(Controls.Action.StrafeRight, Key.C)]
    [InlineData(Controls.Action.FlyUp, Key.E)]
    [InlineData(Controls.Action.FlyDown, Key.Q)]
    [InlineData(Controls.Action.MouseLook, Key.None)]
    public void Classic_keys_are_the_originals(Controls.Action action, Key key)
    {
        Assert.Equal(key, Controls.ClassicKey(action));
    }

    [Theory]
    [InlineData(Controls.Action.Back, Key.S)]
    [InlineData(Controls.Action.StrafeLeft, Key.A)]
    [InlineData(Controls.Action.TurnLeft, Key.Q)]
    [InlineData(Controls.Action.Jump, Key.Space)]
    [InlineData(Controls.Action.Walk, Key.None)]
    public void Modern_defaults_are_unchanged(Controls.Action action, Key key)
    {
        Assert.Equal(key, Controls.DefaultKey(action));
    }
}
