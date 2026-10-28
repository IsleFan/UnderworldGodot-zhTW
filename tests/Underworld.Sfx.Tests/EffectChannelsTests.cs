using Underworld.Sfx;

namespace Underworld.Sfx.Tests;

/// <summary>
/// The original's effect channels (SOUND.C): fx_play gives a MIDI effect one of UW1's four or UW2's three slots
/// for its SOUNDS.DAT length and drops it when all are busy; kill_effect frees a slot at once. UW2's digital
/// channel (digi_fx_play) goes to a sound of at least the priority of the one still playing, so a footstep
/// (priority 1) cannot cut off a blow (1004) while it plays, but takes the channel once it is quiet.
/// </summary>
public class EffectChannelsTests
{
    [Fact]
    public void A_full_set_of_slots_drops_the_next_effect_until_one_ends()
    {
        var c = new EffectChannels(3);
        Assert.Equal(0, c.AcquireMidi(0, 256));//one second
        Assert.Equal(1, c.AcquireMidi(0, 256));
        Assert.Equal(2, c.AcquireMidi(0, 256));
        Assert.Equal(-1, c.AcquireMidi(500, 256));
        Assert.Equal(0, c.AcquireMidi(1000, 256));//the first has run its length
    }

    [Fact]
    public void Killing_an_effect_frees_its_slot_and_a_held_one_waits_for_it()
    {
        var c = new EffectChannels(4);
        var held = c.AcquireMidi(0, 0xFFFF);
        Assert.True(c.MidiBusy(held, long.MaxValue - 1));
        c.ReleaseMidi(held);
        Assert.False(c.MidiBusy(held, 1));
        Assert.Equal(held, c.AcquireMidi(1, 64));
    }

    [Fact]
    public void Killing_all_effects_frees_every_slot()
    {
        var c = new EffectChannels(4);
        for (int i = 0; i < 4; i++) c.AcquireMidi(0, 6400);
        Assert.Equal(-1, c.AcquireMidi(10, 64));
        c.ReleaseAllMidi();
        Assert.Equal(0, c.AcquireMidi(10, 64));
    }

    [Fact]
    public void A_weaker_sound_cannot_take_the_digital_channel_while_a_stronger_one_plays()
    {
        var c = new EffectChannels(3);
        Assert.True(c.TryTakeDigital(1004 + 0x60, busy: false));//a blow
        Assert.False(c.TryTakeDigital(1 + 0x50, busy: true));//a footstep while it plays: to MIDI
        Assert.True(c.TryTakeDigital(1204, busy: true));//a death cry is stronger
        Assert.True(c.TryTakeDigital(1 + 0x50, busy: false));//the channel is quiet again
    }

    [Fact]
    public void An_equal_priority_takes_the_digital_channel()
    {
        var c = new EffectChannels(3);
        Assert.True(c.TryTakeDigital(700, busy: false));
        Assert.True(c.TryTakeDigital(700, busy: true));
    }
}
