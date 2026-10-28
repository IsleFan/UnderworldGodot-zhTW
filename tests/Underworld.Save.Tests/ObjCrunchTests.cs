using System.IO;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// UW2 MAP.C Map_Save runs ObjCrunch before writing a level: with consistent object lists the free records are
/// zeroed, so the saved block is clean; a free record that is also in a tile's list must stop it, or an object in
/// use would be wiped from the save.
/// </summary>
[Collection("UWClassState")]
public class ObjCrunchTests : System.IDisposable
{
    private readonly string _origBasePath = Underworld.UWClass.BasePath;
    private readonly byte _origRes = Underworld.UWClass._RES;

    public void Dispose()
    {
        Underworld.UWClass.BasePath = _origBasePath;
        Underworld.UWClass._RES = _origRes;
    }

    static int Word(byte[] b, int o) => b[o] | (b[o + 1] << 8);

    static byte[] Level(int levelNo)
    {
        Underworld.UWClass.BasePath = Path.Combine(TestData.UW2GogRoot, "UW2");
        Underworld.UWClass._RES = Underworld.UWClass.GAME_UW2;
        Assert.True(Underworld.LevArkLoader.LoadLevArkFileData(folder: "DATA"));
        var block = Underworld.LevArkLoader.LoadLevArkBlock(levelNo);
        var data = new byte[0x7E08];
        System.Array.Copy(block.Data, data, System.Math.Min(block.Data.Length, data.Length));
        return data;
    }

    [GameDataFact]
    public void A_shipped_level_is_consistent_and_its_free_statics_are_zeroed()
    {
        var data = Level(0);
        var top = (short)Word(data, 0x7C04);
        Assert.True(top >= 0);
        var free = Word(data, 0x74FC + top * 2);
        var at = 0x4000 + 256 * 0x1B + (free - 256) * 8;

        Assert.True(Underworld.LevArkWriter.CrunchObjects(data));
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(0, data[at + i]);
        }
    }

    [GameDataFact]
    public void A_free_record_still_in_a_tile_list_is_not_wiped()
    {
        var data = Level(0);
        int head = 0;
        for (int tile = 0; (tile < 64 * 64) && (head < 256); tile++)
        {
            head = Word(data, tile * 4 + 2) >> 6;
        }
        Assert.True(head >= 256);//a static object lying on a tile
        var top = (short)Word(data, 0x7C04);
        data[0x74FC + top * 2] = (byte)(head & 0xFF);
        data[0x74FC + top * 2 + 1] = (byte)(head >> 8);
        var before = (byte[])data.Clone();

        Assert.False(Underworld.LevArkWriter.CrunchObjects(data));
        Assert.Equal(before, data);
    }
}
