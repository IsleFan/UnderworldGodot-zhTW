using System.IO;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// UW2 keeps each level's automap (the tiles seen) in LEV.ARK block 0x9F + level, 160 + the 0-based level here, and
/// the original writes it on every save and level change (SaveLevel's SaveAutoMapLevel). A save must carry what was
/// explored since the game was loaded, and leave alone the levels not visited this session.
/// </summary>
[Collection("UWClassState")]
public class Uw2AutomapSaveTests : System.IDisposable
{
    private readonly string _origBasePath = Underworld.UWClass.BasePath;
    private readonly byte _origRes = Underworld.UWClass._RES;

    public void Dispose()
    {
        Underworld.UWClass.BasePath = _origBasePath;
        Underworld.UWClass._RES = _origRes;
    }

    [GameDataFact]
    public void Exploration_since_loading_survives_a_save_and_reload()
    {
        Underworld.UWClass.BasePath = Path.Combine(TestData.UW2GogRoot, "UW2");
        Underworld.UWClass._RES = Underworld.UWClass.GAME_UW2;
        Assert.True(Underworld.LevArkLoader.LoadLevArkFileData(folder: "SAVE0"));

        byte[] originalFile = Underworld.LevArkLoader.lev_ark_file_data;
        try
        {
            const int seen = 10 * 64 + 10;
            var unvisitedBefore = new Underworld.automap(5).buffer;

            var level0 = new Underworld.automap(0);
            var marked = (byte)(level0.buffer[seen] == 0x01 ? 0x02 : 0x01);//a floor tile seen
            level0.buffer[seen] = marked;
            Underworld.automap.automaps = new Underworld.automap[Underworld.UWTileMap.NO_OF_LEVELS];
            Underworld.automap.automaps[0] = level0;

            byte[] saved = Underworld.LevArkWriter.Serialize();
            Underworld.automap.automaps = null;
            Underworld.LevArkLoader.lev_ark_file_data = saved;

            Assert.Equal(marked, new Underworld.automap(0).buffer[seen]);
            Assert.Equal(unvisitedBefore, new Underworld.automap(5).buffer);
        }
        finally
        {
            Underworld.LevArkLoader.lev_ark_file_data = originalFile;
            Underworld.automap.automaps = null;
        }
    }
}
