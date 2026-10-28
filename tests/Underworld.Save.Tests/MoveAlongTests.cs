using Underworld;

namespace Underworld.Save.Tests
{
    /// <summary>
    /// UTIL.C move_along, which do_gem uses to scatter a resurrection near the blackrock gem: the heading is the
    /// game's (0 north, turning clockwise, 256 to the turn) and each nonzero step is rounded away from zero.
    /// </summary>
    public class MoveAlongTests
    {
        [Theory]
        [InlineData(0x00, 0, 8)]   //north: y up by the whole distance (7 from the scaled sine, plus the rounding)
        [InlineData(0x40, 8, 0)]   //east
        [InlineData(0x80, 0, -8)]  //south
        [InlineData(0xC0, -8, 0)]  //west
        [InlineData(0x20, 6, 6)]   //north-east: 0x5A7F / 0x80 * 8 / 0x100 is 5, rounded out to 6
        public void MovesAlongTheHeading(int heading, int dx, int dy)
        {
            int x = 0x1C, y = 0x28;
            Teleportation.MoveAlong(heading, 8, ref x, ref y);
            Assert.Equal(0x1C + dx, x);
            Assert.Equal(0x28 + dy, y);
        }

        [Fact]
        public void OnlyTheLowByteOfTheHeadingCounts()
        {//do_gem passes rand() as it is
            int x1 = 0, y1 = 0, x2 = 0, y2 = 0;
            Teleportation.MoveAlong(0x1234, 8, ref x1, ref y1);
            Teleportation.MoveAlong(0x34, 8, ref x2, ref y2);
            Assert.Equal((x2, y2), (x1, y1));
        }
    }
}
