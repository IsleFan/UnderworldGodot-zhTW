using Underworld.Sfx;

namespace Underworld.Sfx.Tests;

public class PositionalAudioTests
{
    // All tests use packed coords (tile<<3 | fine). 8 units per tile.
    // Constants under test trace to:
    //   UW1 seg014_8AE  (UW1_asm.asm:64454-64921)
    //   UW2 Maybe3DAudioSource  (uw2_asm.asm:79351-79706)

    [Fact]
    public void Dist_zero_returns_base_volume_and_centre_pan()
    {
        // UW1 seg014_8AE short-circuit at UW1_asm.asm:64614-64645:
        //   if dist == 0: vol = SOUNDS.DAT[+2] + volDelta; pan = 0x40.
        var r = PositionalAudio.Sample(
            srcX: 80, srcY: 80, playerX: 80, playerY: 80,
            heading8: 0, baseVelocity: 0x50, volDelta: 0);
        Assert.False(r.Culled);
        Assert.Equal((byte)0x50, r.Vol);
        Assert.Equal((byte)0x40, r.Pan);
    }

    [Fact]
    public void Dist_seven_is_full_volume()
    {
        // dist < 8 branch: no attenuation (UW1_asm.asm:64858 / uw2_asm.asm:79649).
        var r = PositionalAudio.Sample(
            srcX: 87, srcY: 80, playerX: 80, playerY: 80,
            heading8: 0, baseVelocity: 0x50, volDelta: 0);
        Assert.False(r.Culled);
        Assert.Equal((byte)0x50, r.Vol);
    }

    [Fact]
    public void Dist_eight_begins_attenuation()
    {
        // dist == 8 → vol = raw * (0x30 - 8) / 0x28 = raw * 40/40 = raw.
        // (First point of the attenuation branch; still unattenuated at the edge.)
        // Constants: UW1_asm.asm:64850, 64858, 64866-64871.
        var r = PositionalAudio.Sample(
            srcX: 88, srcY: 80, playerX: 80, playerY: 80,
            heading8: 0, baseVelocity: 0x50, volDelta: 0);
        Assert.False(r.Culled);
        Assert.Equal((byte)0x50, r.Vol);
    }

    [Fact]
    public void Dist_twenty_eight_is_attenuated_halfway()
    {
        // dist = 28, raw = 0x50 (80): vol = 80 * (48-28) / 40 = 80 * 20/40 = 40 = 0x28.
        var r = PositionalAudio.Sample(
            srcX: 80 + 28, srcY: 80, playerX: 80, playerY: 80,
            heading8: 0, baseVelocity: 0x50, volDelta: 0);
        Assert.False(r.Culled);
        Assert.Equal((byte)0x28, r.Vol);
    }

    [Fact]
    public void Dist_fortyeight_still_audible()
    {
        // UW1_asm.asm:64850 uses JG (>), i.e. STRICT greater-than → 48 passes.
        // raw * (48-48) / 40 = 0 → clamped to 0.
        var r = PositionalAudio.Sample(
            srcX: 80 + 48, srcY: 80, playerX: 80, playerY: 80,
            heading8: 0, baseVelocity: 0x50, volDelta: 0);
        Assert.False(r.Culled);
        Assert.Equal((byte)0x00, r.Vol);
    }

    [Fact]
    public void Dist_fortynine_is_culled()
    {
        // dist > 48 → return 0xFF ("culled") per UW1_asm.asm:64850-64856.
        var r = PositionalAudio.Sample(
            srcX: 80 + 49, srcY: 80, playerX: 80, playerY: 80,
            heading8: 0, baseVelocity: 0x50, volDelta: 0);
        Assert.True(r.Culled);
    }

    [Fact]
    public void Volume_clamps_to_upper_bound()
    {
        // raw = 0x7F + 10 = 137 → clamp to 0x7F at UW1_asm.asm:64874-64888.
        var r = PositionalAudio.Sample(
            srcX: 80, srcY: 80, playerX: 80, playerY: 80,
            heading8: 0, baseVelocity: 0x7F, volDelta: 10);
        Assert.False(r.Culled);
        Assert.Equal((byte)0x7F, r.Vol);
    }

    [Fact]
    public void Volume_clamps_to_lower_bound()
    {
        // raw = 5 + (-20) = -15 → clamp to 0.
        var r = PositionalAudio.Sample(
            srcX: 80, srcY: 80, playerX: 80, playerY: 80,
            heading8: 0, baseVelocity: 5, volDelta: -20);
        Assert.False(r.Culled);
        Assert.Equal((byte)0, r.Vol);
    }

    // Heading convention — UW2 SOUND.C: the angle 0x4000 - (heading8 << 8) goes to
    // cFstSinCos, which gives the sine a then the cosine b, and the side is
    // (a * nx - ny * b) >> 8. At heading 0 the sine is 1 and the cosine 0, so the
    // side is the source's x: the player faces along y, a source at +x is to one
    // side and one at -x to the other. 0x7F * 0x7F >> 8 is 0x3F, so the full side is
    // pan 1 (Miles' right), and the other -0x40 clamps to 0x7F.

    [Fact]
    public void Pan_is_centred_for_a_source_ahead_or_behind()
    {
        var ahead = PositionalAudio.Sample(
            srcX: 80, srcY: 80 + 16, playerX: 80, playerY: 80,
            heading8: 0, baseVelocity: 0x50, volDelta: 0);
        var behind = PositionalAudio.Sample(
            srcX: 80, srcY: 80 - 16, playerX: 80, playerY: 80,
            heading8: 0, baseVelocity: 0x50, volDelta: 0);
        Assert.InRange(ahead.Pan, (byte)(0x40 - 4), (byte)(0x40 + 4));
        Assert.InRange(behind.Pan, (byte)(0x40 - 4), (byte)(0x40 + 4));
    }

    [Fact]
    public void Pan_goes_fully_to_the_side_of_a_source_beside_the_player()
    {
        var plusX = PositionalAudio.Sample(
            srcX: 80 + 16, srcY: 80, playerX: 80, playerY: 80,
            heading8: 0, baseVelocity: 0x50, volDelta: 0);
        var minusX = PositionalAudio.Sample(
            srcX: 80 - 16, srcY: 80, playerX: 80, playerY: 80,
            heading8: 0, baseVelocity: 0x50, volDelta: 0);
        Assert.Equal((byte)0x01, plusX.Pan);
        Assert.Equal((byte)0x7F, minusX.Pan);
    }

    [Fact]
    public void Turning_a_quarter_brings_the_side_source_ahead()
    {
        // heading8 0x40: the angle is 0, the sine 0 and the cosine 1, so the side is -y and a source at +x is centred.
        var r = PositionalAudio.Sample(
            srcX: 80 + 16, srcY: 80, playerX: 80, playerY: 80,
            heading8: 0x40, baseVelocity: 0x50, volDelta: 0);
        Assert.InRange(r.Pan, (byte)(0x40 - 4), (byte)(0x40 + 4));
    }

    [Fact]
    public void Turning_round_swaps_the_sides()
    {
        var at0 = PositionalAudio.Sample(
            srcX: 80 + 16, srcY: 80, playerX: 80, playerY: 80,
            heading8: 0x00, baseVelocity: 0x50, volDelta: 0);
        var at180 = PositionalAudio.Sample(
            srcX: 80 + 16, srcY: 80, playerX: 80, playerY: 80,
            heading8: 0x80, baseVelocity: 0x50, volDelta: 0);
        Assert.Equal((byte)0x01, at0.Pan);
        Assert.Equal((byte)0x7F, at180.Pan);
    }

    [Fact]
    public void Pan_clamps_to_valid_range()
    {
        // Source at extreme dy with small dx — normalised ratio hits saturation
        // at ±0x7F (UW1_asm.asm:64657, 64678). Result must be in [0, 0x7F].
        var r = PositionalAudio.Sample(
            srcX: 80, srcY: 80 + 40, playerX: 80, playerY: 80,
            heading8: 0x40, baseVelocity: 0x50, volDelta: 0);
        Assert.InRange(r.Pan, (byte)0, (byte)0x7F);
    }
}
