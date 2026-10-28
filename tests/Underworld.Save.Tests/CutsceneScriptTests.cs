using System;
using System.IO;
using System.Linq;
using Underworld;
using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// A cutscene script (csNNN.n00) is a stream of words with no length fields: each record is its frame, its opcode
/// and as many arguments as the opcode's handler uses (CUTS.C cutsop_). palrange (16) and palfade (17) carry
/// their colours in the script, so their length comes from their own count argument; UW1's next (5) skips a word
/// that UW2's does not. A wrong count shifts every record after it.
/// </summary>
[Collection("UWClassState")]
public class CutsceneScriptTests : IDisposable
{
    private readonly byte _origRes = UWClass._RES;

    public void Dispose()
    {
        UWClass._RES = _origRes;
    }

    private static byte[] Words(params int[] words)
    {
        var data = new byte[words.Length * 2];
        for (int i = 0; i < words.Length; i++)
        {
            data[i * 2] = (byte)(words[i] & 0xFF);
            data[i * 2 + 1] = (byte)(words[i] >> 8);
        }
        return data;
    }

    [Fact]
    public void Palrange_takes_three_words_per_colour()
    {
        UWClass._RES = UWClass.GAME_UW2;
        //frame 1 palrange(first 2, count 2, two colours), then frame 5 end
        var commands = cutsplayer.LoadCutsceneData(Words(1, 16, 2, 2, 10, 11, 12, 20, 21, 22, 5, 6));
        Assert.Equal(2, commands.Count);
        Assert.Equal(8, commands[0].NoOfParams);
        Assert.Equal(6, commands[1].functionNo);
        Assert.Equal(5, commands[1].frame);
    }

    [Fact]
    public void Palfade_takes_its_timing_then_three_words_per_colour()
    {
        UWClass._RES = UWClass.GAME_UW2;
        //frame 1 palfade(period 4, total 8, first 0, count 1, one colour), then frame 3 end
        var commands = cutsplayer.LoadCutsceneData(Words(1, 17, 4, 8, 0, 1, 30, 31, 32, 3, 6));
        Assert.Equal(2, commands.Count);
        Assert.Equal(7, commands[0].NoOfParams);
        Assert.Equal(6, commands[1].functionNo);
        Assert.Equal(3, commands[1].frame);
    }

    [Theory]
    [InlineData(UWClass.GAME_UW1, 1)]
    [InlineData(UWClass.GAME_UW2, 0)]
    public void Next_skips_a_word_in_UW1_only(byte res, int args)
    {
        UWClass._RES = res;
        var commands = cutsplayer.LoadCutsceneData(
            res == UWClass.GAME_UW1 ? Words(20, 5, 1, 9, 6) : Words(20, 5, 9, 6));
        Assert.Equal(args, commands[0].NoOfParams);
        Assert.Equal(6, commands[1].functionNo);
        Assert.Equal(9, commands[1].frame);
    }

    /// <summary>
    /// Every shipped script parses into known opcodes on frames the player uses (up to 999, the frame after the
    /// file) and ends the cutscene somewhere: a record read out of step would show as an opcode beyond the table
    /// or a wild frame.
    /// </summary>
    [GameDataTheory]
    [InlineData(UWClass.GAME_UW1, "UW1", 16)]
    [InlineData(UWClass.GAME_UW2, "UW2", 28)]
    public void Shipped_scripts_parse_in_step(byte res, string game, int opcodes)
    {
        UWClass._RES = res;
        var dir = Path.Combine(TestData.UW2GogRoot, game, "CUTS");
        var scripts = Directory.GetFiles(dir).Where(f => f.EndsWith(".N00", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(scripts);
        foreach (var script in scripts)
        {
            var commands = cutsplayer.LoadCutsceneData(File.ReadAllBytes(script));
            Assert.All(commands, c =>
            {
                Assert.InRange(c.functionNo, 0, opcodes - 1);
                Assert.InRange(c.frame, 0, 999);
            });
            Assert.Contains(commands, c => c.functionNo == 6);
        }
    }
}
