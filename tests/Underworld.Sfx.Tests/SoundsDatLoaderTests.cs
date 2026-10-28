using Underworld.Sfx;

namespace Underworld.Sfx.Tests;

public class SoundsDatLoaderTests
{
    [Fact]
    public void Uw1_fixture_is_121_bytes()
    {
        if (!Fixtures.SkipIfUnavailable()) return;
        // 1-byte count (0x18 = 24) + 24 * 5-byte records = 121 bytes.
        Assert.Equal(121, new System.IO.FileInfo(Fixtures.SoundsDat).Length);
    }

    [Fact]
    public void Loads_24_entries()
    {
        if (!Fixtures.SkipIfUnavailable()) return;
        Assert.Equal(24, SoundsDatLoader.Load(Fixtures.SoundsDat).Length);
    }

    [Fact]
    public void Entry_fields_are_in_range()
    {
        if (!Fixtures.SkipIfUnavailable()) return;
        foreach (var e in SoundsDatLoader.Load(Fixtures.SoundsDat))
        {
            Assert.InRange(e.Note, (byte)0, (byte)127);
            Assert.InRange(e.Velocity, (byte)0, (byte)127);
        }
    }

    [Fact]
    public void Entries_one_and_two_share_tvfx_number_step()
    {
        if (!Fixtures.SkipIfUnavailable()) return;
        // Per issue #28 name table: IDs 1 and 2 both map to tvfx=1 ("Step" / duplicate).
        var entries = SoundsDatLoader.Load(Fixtures.SoundsDat);
        Assert.Equal(entries[1].PatchNum, entries[2].PatchNum);
    }

    [Fact]
    public void Uw1_fixture_durations_read_as_the_original_does()
    {
        if (!Fixtures.SkipIfUnavailable()) return;
        // read_fx_data (underworld-exhumed SOUND.C) takes byte 3 as the high byte:
        // entry 0 (water) is 0x19 0x00 = 6400/256 s = 25 s, entry 1 (step) 0x00 0x40 = 0.25 s.
        var entries = SoundsDatLoader.Load(Fixtures.SoundsDat);
        Assert.Equal(6400, entries[0].DurationWord);
        Assert.Equal(64, entries[1].DurationWord);
    }

    [Fact]
    public void Uw2_digi_byte_says_whether_the_effect_has_a_voc()
    {
        // UW2's 8-byte entries keep a digi byte at 5 (SOUND.C struct Effect): 0 plays the MIDI patch even
        // where an SPnn.VOC exists, as entry 40 does in the shipped SOUNDS.DAT. UW1's 5-byte entries have none.
        var origRes = UWClass._RES;
        var tmp = System.IO.Path.GetTempFileName();
        try
        {
            UWClass._RES = UWClass.GAME_UW2;
            System.IO.File.WriteAllBytes(tmp, new byte[] {
                2,
                0x20, 0x3C, 0x60, 0x00, 0x40, 0, 0, 1,
                0x21, 0x3C, 0x60, 0x00, 0x40, 1, 0, 1 });
            var entries = SoundsDatLoader.Load(tmp);
            Assert.False(entries[0].Digi);
            Assert.True(entries[1].Digi);
        }
        finally
        {
            UWClass._RES = origRes;
            System.IO.File.Delete(tmp);
        }
    }

    [Fact]
    public void Uw1_duration_word_is_big_endian()
    {
        // A 1-entry UW1 file (5-byte entries) whose duration bytes are 0x01 0x80: the
        // original reads 0x0180 = 384 (1.5 s); little-endian it would be 0x8001, which
        // the lifetime conversion takes for infinite.
        var tmp = System.IO.Path.GetTempFileName();
        try
        {
            System.IO.File.WriteAllBytes(tmp, new byte[] { 0x01, 0x09, 0x40, 0x50, 0x01, 0x80 });
            var prev = UWClass._RES;
            UWClass._RES = UWClass.GAME_UW1;
            try
            {
                var entries = SoundsDatLoader.Load(tmp);
                Assert.Single(entries);
                Assert.Equal(0x0180, entries[0].DurationWord);
            }
            finally { UWClass._RES = prev; }
        }
        finally { System.IO.File.Delete(tmp); }
    }

    [Fact]
    public void Entry_0_patch_num_is_water()
    {
        if (!Fixtures.SkipIfUnavailable()) return;
        // Per issue #28 name table: ID 0 → tvfx=8 ("Water").
        Assert.Equal((byte)8, SoundsDatLoader.Load(Fixtures.SoundsDat)[0].PatchNum);
    }

    [Fact]
    public void Uw2_duration_word_is_big_endian()
    {
        // UW2 SOUNDS.DAT entry layout from uw2_asm.asm:83683-83688
        //   shl ax, 8 ; add ax, dx  → byte[3] is high, byte[4] is low → big-endian.
        // UW1 is big-endian too, with 5-byte entries (Uw1_duration_word_is_big_endian).
        //
        // Craft a 2-entry UW2 file: count byte 0x02, then two 8-byte entries where
        // byte[3]=0x12, byte[4]=0x34. Expect DurationWord == 0x1234.
        var tmp = System.IO.Path.GetTempFileName();
        try
        {
            byte[] buf = new byte[] {
                0x02,                                                   // 2 entries
                0x10, 0x20, 0x30, 0x12, 0x34, 0x01, 0x02, 0x03,          // entry 0
                0x11, 0x21, 0x31, 0xAA, 0xBB, 0x00, 0x00, 0x00,          // entry 1
            };
            System.IO.File.WriteAllBytes(tmp, buf);

            var prev = UWClass._RES;
            UWClass._RES = UWClass.GAME_UW2;
            try
            {
                var entries = SoundsDatLoader.Load(tmp);
                Assert.Equal(2, entries.Length);
                Assert.Equal(0x1234, entries[0].DurationWord);
                Assert.Equal(0xAABB, entries[1].DurationWord);
            }
            finally { UWClass._RES = prev; }
        }
        finally { System.IO.File.Delete(tmp); }
    }
}
