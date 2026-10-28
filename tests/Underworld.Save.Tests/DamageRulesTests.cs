using System;
using System.IO;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// DAMAGE.C's damage_object and debris_type on UW1's COMOBJ.DAT. The objects are off the map (WorldObject false), so a
/// destroyed one is only reported (remove_object does nothing with x below 0).
/// </summary>
[Collection("UWClassState")]
public class DamageRulesTests : IDisposable
{
    private readonly string _origBasePath = UWClass.BasePath;
    private readonly byte _origRes = UWClass._RES;

    public DamageRulesTests()
    {
        UWClass.BasePath = Path.Combine(TestData.UW2GogRoot, "UW1");
        UWClass._RES = UWClass.GAME_UW1;
        GameStrings.LoadStringsPak(Path.Combine(UWClass.BasePath, "DATA", "STRINGS.PAK"));
        Loader.ReadStreamFile(Path.Combine(UWClass.BasePath, "DATA", "COMOBJ.DAT"), out var comobj);
        typeof(commonObjDat).GetField("buffer", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .SetValue(null, comobj);
    }

    public void Dispose()
    {
        UWClass.BasePath = _origBasePath;
        UWClass._RES = _origRes;
    }

    private static uwObject Item(int item_id, int quality)
    {
        var obj = new uwObject { IsStatic = true, index = 1, PTR = 0, DataBuffer = new byte[8] };
        obj.item_id = item_id;
        obj.quality = (short)quality;
        return obj;
    }

    private static bool Damage(uwObject obj, int amount) =>
        damage.DamageObject(obj, amount, damagetype: 0, objList: new[] { null, obj }, WorldObject: false, damagesource: 0);

    [Fact]
    public void Uw1_IdBit13_ProtectsEvenADoor()
    {
        var door = Item(0x140, quality: 40);   // quality class 0
        door.doordir = 1;

        Assert.False(Damage(door, 50));
        Assert.Equal(40, door.quality);
    }

    [Fact]
    public void DamageShiftedToNothing_DoesNothing_EvenAtQualityZero()
    {
        var axe = Item(0x2, quality: 0);   // quality class 2: 3 >> 2 is 0
        Assert.Equal(2, commonObjDat.qualityclass(0x2));

        Assert.False(Damage(axe, 3));
    }

    [Fact]
    public void EnoughDamage_DestroysIt()
    {
        var axe = Item(0x2, quality: 1);

        Assert.True(Damage(axe, 4));   // 4 >> 2 = 1
        Assert.Equal(0, axe.quality);
        Assert.Equal(0x2, axe.item_id);   // off the map it is left as it is, for DamageEquipment to remove
    }

    [Theory]
    [InlineData(0x3, 4, 0xC7)]     // the dagger: the broken dagger
    [InlineData(0x3, 8, 0xD6)]     // pure fire breaks no weapon
    [InlineData(0x150, 4, 0xDC)]   // furniture: wood chips
    [InlineData(0x80, 4, 0xD6)]    // anything else: debris
    public void DebrisType_FollowsTheOriginal(int item_id, int damagetype, int expected)
    {
        Assert.Equal(expected, damage.GetObjectTypeDebris(Item(item_id, quality: 40), damagetype));
    }
}
