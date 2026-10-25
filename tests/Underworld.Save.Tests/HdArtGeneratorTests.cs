using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// The in-game Pixel HD generator (HdArtGenerator), a port of upscale-lab's export_hd.py
/// rotsprite4x mode. The port has to give the same files as the script, because the script's
/// output is what the HD loading and its alignment test (hd_art_test) were checked against.
/// </summary>
public class HdArtGeneratorTests
{
    /// <summary>
    /// A diagonal stays one index per pixel and gains no colour: RotSprite only rearranges the
    /// indices it was given, so palettes and lighting keep working, and index 0 (transparent)
    /// is never taken for a similar colour, so a sprite's outline does not swallow its edge.
    /// </summary>
    [Fact]
    public void RotSprite_UsesOnlyTheGivenIndices_AndKeepsTransparencyApart()
    {
        var colours = new byte[256 * 3];
        colours[5 * 3] = 200; //index 5 is red
        colours[6 * 3] = 190; //index 6 is a red close enough to 5 to count as the same
        var p = new HdArtGenerator.Picture(4, 4, new byte[]
        {
            5, 0, 0, 0,
            0, 5, 0, 0,
            0, 0, 6, 0,
            0, 0, 0, 5,
        });

        var big = HdArtGenerator.RotSprite(p, colours, 4, transparent0: true);

        Assert.Equal(16, big.Width);
        Assert.Equal(16, big.Height);
        Assert.True(big.Data.All(b => b == 0 || b == 5 || b == 6));
        // the corners stay what they were: opaque on the diagonal, transparent off it
        Assert.Equal(5, big.Data[0]);
        Assert.Equal(0, big.Data[15]);
        Assert.Equal(5, big.Data[16 * 16 - 1]);
        // the line is drawn thicker than nothing and thinner than a filled square
        int opaque = big.Data.Count(b => b != 0);
        Assert.InRange(opaque, 4 * 16, 16 * 16 / 2);
    }

    /// <summary>
    /// Every file the generator writes is byte for byte the file export_hd.py rotsprite2x or
    /// rotsprite4x wrote, fonts aside (made in Godot by a different method). Needs the game data
    /// and the script's output, named by UW_HDGEN_GAMES (holding UW1 and UW2) and
    /// UW_HDGEN_REFERENCE (holding rotsprite2x and rotsprite4x); skipped without them.
    /// </summary>
    [HdGenReferenceTheory]
    [InlineData(2)]
    [InlineData(4)]
    public void Generate_MatchesTheScriptsRotSpriteSet(int scale)
    {
        string games = Environment.GetEnvironmentVariable("UW_HDGEN_GAMES");
        string reference = Path.Combine(Environment.GetEnvironmentVariable("UW_HDGEN_REFERENCE"), HdArtGenerator.SetName(scale));
        string outDir = Path.Combine(Path.GetTempPath(), "hdgen-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var game in new[] { "uw1", "uw2" })
            {
                int written = HdArtGenerator.Generate(Path.Combine(games, game.ToUpperInvariant()), game, outDir, scale);
                var expected = Directory.GetFiles(Path.Combine(reference, game), "*.uwhd", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(Path.Combine(reference, game), f))
                    .Where(f => !f.StartsWith("font"))
                    .OrderBy(f => f, StringComparer.Ordinal).ToList();
                var actual = Directory.GetFiles(Path.Combine(outDir, game), "*.uwhd", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(Path.Combine(outDir, game), f))
                    .OrderBy(f => f, StringComparer.Ordinal).ToList();
                Assert.Equal(expected, actual);
                Assert.Equal(expected.Count, written);
                var differ = expected.Where(f => !File.ReadAllBytes(Path.Combine(reference, game, f))
                    .AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(outDir, game, f)))).ToList();
                Assert.True(differ.Count == 0, $"{game}: {differ.Count} files differ, e.g. {string.Join(", ", differ.Take(5))}");
            }
        }
        finally
        {
            if (Directory.Exists(outDir)) { Directory.Delete(outDir, true); }
        }
    }
}

/// <summary>Skips, visibly, unless UW_HDGEN_GAMES and UW_HDGEN_REFERENCE name existing folders.</summary>
public sealed class HdGenReferenceTheoryAttribute : TheoryAttribute
{
    public HdGenReferenceTheoryAttribute()
    {
        var games = Environment.GetEnvironmentVariable("UW_HDGEN_GAMES");
        var reference = Environment.GetEnvironmentVariable("UW_HDGEN_REFERENCE");
        if (!Directory.Exists(games ?? "") || !Directory.Exists(reference ?? ""))
        {
            Skip = "set UW_HDGEN_GAMES (the folder holding UW1 and UW2) and UW_HDGEN_REFERENCE (holding export_hd.py's rotsprite2x and rotsprite4x) to run";
        }
    }
}
