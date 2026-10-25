using System;
using System.IO;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// The launcher's game finding (GameFinder): GOG's CD image is read as ISO 9660 and each game
/// copied out playable, and a game beside the program is found first.
/// </summary>
public class GameFinderTests
{
    /// <summary>
    /// Both games come out of a GOG installation's game.gog playable (InstallationValidator accepts
    /// them), and once copied beside the program they are what FindFolder returns, before any
    /// other place. Needs a GOG installation (macOS apps in Applications, or Windows GOG folders).
    /// </summary>
    [GogInstalledFact]
    public void GogImage_ExtractsBothGames_AndTheProgramFolderIsFoundFirst()
    {
        var programDir = Path.Combine(Path.GetTempPath(), "gamefinder-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var game in new[] { GameFinder.Uw1, GameFinder.Uw2 })
            {
                var image = GameFinder.FindGogImage(game);
                Assert.NotNull(image);
                var dest = Path.Combine(programDir, game.Folder);
                Assert.True(GameFinder.Extract(image, game, dest) > 100);
                Assert.True(File.Exists(Path.Combine(dest, game.Exe)));
                Assert.True(GameFinder.Usable(game, dest));
                Assert.Equal(dest, GameFinder.FindFolder(game, programDir));
                // an existing folder (it may hold saves) is never written over
                Assert.Throws<IOException>(() => GameFinder.Extract(image, game, dest));
            }
        }
        finally
        {
            if (Directory.Exists(programDir)) { Directory.Delete(programDir, true); }
        }
    }

    /// <summary>A folder without the game's fonts is not taken for the game.</summary>
    [Fact]
    public void EmptyFolder_IsNotUsable()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gamefinder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "DATA"));
        try
        {
            Assert.False(GameFinder.Usable(GameFinder.Uw1, dir));
            Assert.False(GameFinder.Usable(GameFinder.Uw2, null));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}

/// <summary>Skips, visibly, without a GOG installation of Ultima Underworld.</summary>
public sealed class GogInstalledFactAttribute : FactAttribute
{
    public GogInstalledFactAttribute()
    {
        if (GameFinder.FindGogImage(GameFinder.Uw1) == null) { Skip = "no GOG installation of Ultima Underworld here"; }
    }
}
