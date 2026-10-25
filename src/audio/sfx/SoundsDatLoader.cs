using System.IO;

namespace Underworld.Sfx;

public static class SoundsDatLoader
{
    static int BlockSize
    {
        get
        {
            if (UWClass._RES == UWClass.GAME_UW2)
            {
                return 8;
            }
            else
            {
                return 5;
            }
        }
    }
    public static SoundEntry[] Load(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < 1)
            throw new InvalidDataException("SOUNDS.DAT is empty");

        int count = data[0];
        int need = 1 + count * BlockSize;
        if (data.Length < need)
            throw new InvalidDataException(
                $"SOUNDS.DAT truncated: need {need} bytes for {count} entries, got {data.Length}");

        var entries = new SoundEntry[count];
        for (int i = 0; i < count; i++)
        {
            int o = 1 + i * BlockSize;
            // The duration word is big-endian in both games, byte 3 high:
            //   UW1: read_fx_data in underworld-exhumed's uw1/src/sound/SOUND.C,
            //     "length (high byte first)", e->length = (len_hi << 8) + len_lo.
            //   UW2: uw2_asm.asm:83683-83688  "shl ax, 8 ; add ax, dx"
            //     with al=byte[3], dl=byte[4] → word = (byte[3] << 8) | byte[4].
            // UW1's lengths then read 64, 128, 384 and 6400 (1/256 s); read
            // little-endian they were 16384 and up, most with bit 15 set.
            ushort dur = (ushort)((data[o + 3] << 8) | data[o + 4]);
            entries[i] = new SoundEntry(
                PatchNum:     data[o + 0],
                Note:         data[o + 1],
                Velocity:     data[o + 2],
                DurationWord: dur);
        }
        return entries;
    }
}
