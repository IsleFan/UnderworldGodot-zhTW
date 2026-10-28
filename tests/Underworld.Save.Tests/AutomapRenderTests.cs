using Underworld;

namespace Underworld.Save.Tests
{
    /// <summary>
    /// AUTOMAP.C ShowDungeonMap draws each seen square three pixels across at (x - 1) * 3 + 10, (y - 1) * 3 + 7 on the
    /// 320x200 screen, y counted up from the bottom, and edges a side unless the square beyond is a seen type (1-9).
    /// </summary>
    [Collection("UWClassState")]
    public class AutomapRenderTests
    {
        static int Row(int y) => (199 - y) * 320;

        static bool[] DrawOne(int x, int y, byte code, int nx = -1, int ny = -1, byte ncode = 0)
        {
            var res = UWClass._RES;
            UWClass._RES = UWClass.GAME_UW1;
            try
            {
                var map = new byte[64 * 64];
                map[(y << 6) + x] = code;
                if (nx >= 0) map[(ny << 6) + nx] = ncode;
                var background = new byte[320 * 200];
                for (int i = 0; i < background.Length; i++) background[i] = 0x40;
                AutomapRender.Draw(map, background);
                return (bool[])AutomapRender.Changed.Clone();
            }
            finally { UWClass._RES = res; }
        }

        [Fact]
        public void AnOpenSquare_FillsItsThreeByThreePixels()
        {
            var changed = DrawOne(5, 5, 1);
            int px = (5 - 1) * 3 + 10, py = (5 - 1) * 3 + 7;
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    Assert.True(changed[Row(py + j) + px + i]);
            Assert.False(changed[Row(py + 1) + px + 5]);//nothing past its edge column
        }

        [Fact]
        public void AnUnseenNeighbour_GetsAnEdge_ASeenOneDoesNot()
        {
            int px = (5 - 1) * 3 + 10, py = (5 - 1) * 3 + 7;
            //east of square 5 is the column px + 3; a diagonal (type 2) there leaves its own first column's middle row
            //undrawn, so only square 5's edge could touch it
            var unseen = DrawOne(5, 5, 1);
            Assert.True(unseen[Row(py + 1) + px + 3]);
            var seen = DrawOne(5, 5, 1, 6, 5, 2);
            Assert.False(seen[Row(py + 1) + px + 3]);
            Assert.True(unseen[Row(py - 1) + px + 1]);//south side edge (ShadeSide 2: py - 1)
        }
    }
}
