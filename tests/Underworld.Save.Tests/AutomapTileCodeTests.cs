using System;
using Underworld;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// An automap byte keeps the tile type in its low nibble and two fields in its high nibble: the kind
/// (GRIDDB's curautocode: 1 door, 2 bridge, 3 stairs texture) and the floor's terrain class (1 water,
/// 2 lava, 3 ice). UW2 has the kind in bits 4-5 and the terrain in bits 6-7 (DoTile); UW1 the other way
/// round. Each field is read on its own, so a door over water is both a door and water.
/// </summary>
[Collection("UWClassState")]
public class AutomapTileCodeTests : IDisposable
{
    private readonly byte _origRes = UWClass._RES;

    public void Dispose()
    {
        UWClass._RES = _origRes;
    }

    private static automaptileinfo Tile(byte code)
    {
        var buffer = new byte[] { code };
        return new automaptileinfo(0, ref buffer);
    }

    [Fact]
    public void Uw2_lava_is_terrain_class_2_and_a_bridge_is_kind_2()
    {
        UWClass._RES = UWClass.GAME_UW2;
        var lava = Tile(0x81);
        Assert.True(lava.IsLava);
        Assert.False(lava.IsBridge);
        var bridge = Tile(0x21);
        Assert.True(bridge.IsBridge);
        Assert.False(bridge.IsLava);
        Assert.False(bridge.IsWater);
    }

    [Fact]
    public void Uw2_door_over_water_is_both()
    {
        UWClass._RES = UWClass.GAME_UW2;
        var t = Tile(0x51);
        Assert.True(t.IsDoor);
        Assert.True(t.IsWater);
        Assert.Equal(1, t.tileType);
    }

    [Fact]
    public void Uw1_fields_are_the_other_way_round()
    {
        UWClass._RES = UWClass.GAME_UW1;
        Assert.True(Tile(0x21).IsLava);
        Assert.False(Tile(0x21).IsBridge);
        Assert.True(Tile(0x81).IsBridge);//a bridge over a plain floor
        Assert.False(Tile(0x81).IsLava);
        var doorOverWater = Tile(0x51);
        Assert.True(doorOverWater.IsDoor);
        Assert.True(doorOverWater.IsWater);
        Assert.True(Tile(0xC1).IsStair);
        Assert.False(Tile(0xC1).IsIce);//UW1 has no ice class
    }

    [Theory]
    [InlineData(UWClass.GAME_UW2, automaptileinfo.KindBridge, 0x20)]
    [InlineData(UWClass.GAME_UW2, automaptileinfo.KindStair, 0x30)]
    [InlineData(UWClass.GAME_UW1, automaptileinfo.KindBridge, 0x80)]
    [InlineData(UWClass.GAME_UW1, automaptileinfo.KindDoor, 0x40)]
    public void A_kind_is_written_without_touching_the_terrain_bits(byte res, int kind, int bits)
    {
        UWClass._RES = res;
        Assert.Equal(bits, automaptileinfo.KindBits(kind));
    }
}
