using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Underworld;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// Rules checked case by case against the outputs of UW2.EXE's own code (underworld-exhumed's
/// uw2/vectors, see vectors/README.md). Each case sets the Turbo C rand() state to `seed`, so the
/// port must draw the same numbers, in the same order, as the original: `seed_out` checks that too.
/// </summary>
[Collection("UWClassState")]
public class Uw2VectorTests : IDisposable
{
    private readonly uint _origSeed = Rng.Seed;

    public void Dispose() => Rng.Seed = _origSeed;

    private static IEnumerable<Dictionary<string, long>> Rows(string name)
    {
        var lines = File.ReadAllLines(Path.Combine(TestData.RepoRoot, "tests", "Underworld.Save.Tests", "vectors", name));
        var header = lines[0].Split(',');
        foreach (var line in lines.Skip(1).Where(l => l.Length > 0))
        {
            var cells = line.Split(',').Select(long.Parse).ToArray();
            yield return header.Zip(cells).ToDictionary(p => p.First, p => p.Second);
        }
    }

    [Fact]
    public void SkillCheck_MatchesUw2()
    {
        foreach (var r in Rows("skill_check.csv"))
        {
            Rng.Seed = (uint)r["seed"];
            var result = (int)playerdat.SkillCheck((int)r["value"], (int)r["target"]);
            Assert.True(r["result"] == result, $"value {r["value"]} target {r["target"]} seed {r["seed"]}: {result}, UW2 {r["result"]}");
            Assert.Equal((uint)r["seed_out"], Rng.Seed);
        }
    }

    [Fact]
    public void Rollem_MatchesUw2()
    {
        foreach (var r in Rows("rollem.csv"))
        {
            Rng.Seed = (uint)r["seed"];
            var total = Rng.DiceRoll((int)r["dice"], (int)r["sides"]);
            Assert.True(r["total"] == total, $"{r["dice"]}d{r["sides"]} seed {r["seed"]}: {total}, UW2 {r["total"]}");
            Assert.Equal((uint)r["seed_out"], Rng.Seed);
        }
    }

    [Fact]
    public void PickLoc_MatchesUw2()
    {
        foreach (var r in Rows("pickloc.csv"))
        {
            Rng.Seed = (uint)r["seed"];
            var loc = combat.PickBodyHitPoint((int)r["dz"], (int)r["dtop"], (int)r["az"], (int)r["atop"]);
            Assert.True(r["loc"] == loc, $"dz {r["dz"]} dtop {r["dtop"]} az {r["az"]} atop {r["atop"]} seed {r["seed"]}: {loc}, UW2 {r["loc"]}");
            Assert.Equal((uint)r["seed_out"], Rng.Seed);
        }
    }

    [Fact]
    public void IsSharp_MatchesUw2()
    {
        foreach (var r in Rows("is_sharp.csv"))
        {
            Assert.True((r["sharp"] == 1) == combat.IsSharp((int)r["item"]), $"item {r["item"]}");
        }
    }

    [Fact]
    public void CheckResistance_MatchesUw2()
    {
        foreach (var r in Rows("check_res.csv"))
        {
            Rng.Seed = (uint)r["seed"];
            var damage = (int)r["damage"];
            var result = Underworld.damage.CheckResistanceUW2((int)r["resist"], ref damage, (int)r["type"]);
            Assert.True(r["result"] == result, $"resist {r["resist"]} damage {r["damage"]} type {r["type"]} seed {r["seed"]}: {result}, UW2 {r["result"]}");
            Assert.Equal((uint)r["seed_out"], Rng.Seed);
        }
    }
}
