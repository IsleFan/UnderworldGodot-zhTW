using Xunit;

namespace Underworld.Save.Tests;

/// <summary>
/// UW1's animation list is a LEV.ARK block of its own, 64 entries of 6 bytes; DOS (EFFECT.C Anim_Load) counts the
/// entries up to the first zero object index. The port frees an entry by running its frames down to 0 or by clearing
/// its slot, so before writing, the running entries go first and everything after them is zero (Anim_Save).
/// </summary>
public class OverlayCompactionTests
{
    private static void Entry(byte[] data, int slot, int link, int frames)
    {
        var word = link << 6;
        data[slot * 6] = (byte)word;
        data[slot * 6 + 1] = (byte)(word >> 8);
        data[slot * 6 + 2] = (byte)frames;
        data[slot * 6 + 3] = (byte)(frames >> 8);
    }

    private static int Link(byte[] data, int slot) => (data[slot * 6] | (data[slot * 6 + 1] << 8)) >> 6;

    [Fact]
    public void Uw1OverlayBlock_RunningEntriesComeFirst_TheRestIsZero()
    {
        var data = new byte[384];
        Entry(data, 0, link: 300, frames: 0);   // finished, link left behind
        Entry(data, 2, link: 301, frames: 4);   // after a hole
        Entry(data, 5, link: 302, frames: 0xFFFF);

        var written = LevArkWriter.SerializeOverlayBlock(new UWBlock { Data = data });

        Assert.Equal(301, Link(written, 0));
        Assert.Equal(302, Link(written, 1));
        Assert.Equal(0, Link(written, 2));   // where DOS stops counting
        for (int i = 12; i < written.Length; i++)
        {
            Assert.Equal(0, written[i]);
        }
    }
}
