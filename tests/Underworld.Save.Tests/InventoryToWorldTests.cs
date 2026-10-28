using System;
using System.IO;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// Carried objects live outside the level object list here, so moving one back to the
/// world allocates static objects for it and everything inside it. The original never
/// runs out on this path (its carried objects stay in the level list), so the whole chain
/// must fit before anything moves; otherwise slot 0, the "no object" index, was written.
/// </summary>
[Collection("UWClassState")]
public class InventoryToWorldTests : IDisposable
{
    private readonly string _origBasePath = UWClass.BasePath;
    private readonly byte _origRes = UWClass._RES;
    private readonly byte[] _origLevArkFileData = LevArkLoader.lev_ark_file_data;
    private readonly UWTileMap[] _origDungeons = UWTileMap.dungeons;
    private readonly UWTileMap _origCurrent = UWTileMap.current_tilemap;
    private readonly uwObject[] _origInventory = playerdat.InventoryObjects;
    private readonly byte[] _origPdat = playerdat.pdat;
    private readonly uint _origSeed = Rng.Seed;

    public void Dispose()
    {
        UWClass.BasePath = _origBasePath;
        UWClass._RES = _origRes;
        LevArkLoader.lev_ark_file_data = _origLevArkFileData;
        UWTileMap.dungeons = _origDungeons;
        UWTileMap.current_tilemap = _origCurrent;
        playerdat.InventoryObjects = _origInventory;
        playerdat.pdat = _origPdat;
        Rng.Seed = _origSeed;
    }

    private static UWTileMap LoadBuiltLevel()
    {
        UWClass.BasePath = Path.Combine(TestData.UW2GogRoot, "UW1");
        UWClass._RES = UWClass.GAME_UW1;
        LevArkLoader.LoadLevArkFileData(folder: "DATA");
        UWTileMap.dungeons = new UWTileMap[UWTileMap.NO_OF_LEVELS];
        var tm = new UWTileMap(0);
        UWTileMap.dungeons[0] = tm;
        UWTileMap.current_tilemap = tm;
        tm.BuildTileMapUW(levelNo: 0, tex_ark: tm.tex_ark_block, ovl_ark: tm.ovl_ark_block);
        playerdat.InitEmptyPlayer("TestPlayer");   // the weight carried lives in pdat
        return tm;
    }

    /// <summary>
    /// Inventory 1 is a sack (item 128) holding inventory 2 and 3, two sling stones (item 16).
    /// </summary>
    private static void CarrySackWithTwoStones()
    {
        var buffer = new byte[8 * 4];
        playerdat.InventoryObjects = new uwObject[512];
        for (short i = 1; i <= 3; i++)
        {
            playerdat.InventoryObjects[i] = new uwObject { IsStatic = true, index = i, PTR = i * 8, DataBuffer = buffer };
        }
        var sack = playerdat.InventoryObjects[1];
        sack.item_id = 128; sack.is_quant = 0; sack.link = 2; sack.next = 0;
        var stone = playerdat.InventoryObjects[2];
        stone.item_id = 16; stone.is_quant = 1; stone.link = 1; stone.next = 3;
        stone = playerdat.InventoryObjects[3];
        stone.item_id = 16; stone.is_quant = 1; stone.link = 1; stone.next = 0;
    }

    private static void ClearAllTiles(UWTileMap tm)
    {
        for (int x = 0; x < 64; x++)
        {
            for (int y = 0; y < 64; y++)
            {
                tm.Tiles[x, y].indexObjectList = 0;
            }
        }
    }

    [Fact]
    public void TooFewFreeObjects_AndNothingToCollect_ReturnsZeroAndChangesNothing()
    {
        var tm = LoadBuiltLevel();
        ClearAllTiles(tm);
        tm.StaticFreeListPtr = 1;   // two free, the sack needs three
        CarrySackWithTwoStones();
        var object0 = new byte[8];
        Array.Copy(tm.lev_ark_block.Data, tm.LevelObjects[0].PTR, object0, 0, 8);

        int got = playerdat.AddInventoryObjectToWorld(objIndex: 1, updateUI: false, RemoveNext: false);

        Assert.Equal(0, got);
        Assert.Equal(1, tm.StaticFreeListPtr);
        Assert.Equal(2, playerdat.InventoryObjects[1].link);   // still points at the carried stone
        Assert.NotNull(playerdat.InventoryObjects[2]);
        Assert.NotNull(playerdat.InventoryObjects[3]);
        var after = new byte[8];
        Array.Copy(tm.lev_ark_block.Data, tm.LevelObjects[0].PTR, after, 0, 8);
        Assert.Equal(object0, after);
    }

    [Fact]
    public void TooFewFreeObjects_CollectsFarObjects_ThenMovesTheWholeSack()
    {
        Rng.Seed = 0x1234;
        var tm = LoadBuiltLevel();
        tm.StaticFreeListPtr = -1;
        CarrySackWithTwoStones();

        int got = playerdat.AddInventoryObjectToWorld(objIndex: 1, updateUI: false, RemoveNext: false, DestroyInventoryObject: false);

        Assert.True(got >= 256, $"expected a static object, got {got}");
        var sack = tm.LevelObjects[got];
        Assert.Equal(128, sack.item_id);
        Assert.True(sack.link >= 256, $"sack contents {sack.link} should be a world object");
        var first = tm.LevelObjects[sack.link];
        Assert.Equal(16, first.item_id);
        Assert.True(first.next >= 256, $"second stone {first.next} should be a world object");
        Assert.Equal(16, tm.LevelObjects[first.next].item_id);
        Assert.Null(playerdat.InventoryObjects[2]);   // the contents left the inventory
        Assert.Null(playerdat.InventoryObjects[3]);
    }
}
