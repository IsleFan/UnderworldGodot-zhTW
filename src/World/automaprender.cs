using Godot;
namespace Underworld
{
    /// <summary>
    /// Draws a level's automap the way the original does (AUTOMAP.C ShowDungeonMap, DoTile, ShadeSide, DoDoorTile): on
    /// the palette indices of the blank map screen, each seen square is three pixels across at (x - 1) * 3 + 10,
    /// (y - 1) * 3 + 7 (y counted up from the bottom of the 320x200 screen), its colours darkened by adding to the
    /// index (PixelDarken) or set from the terrain's colours. The result is a 320x200 layer holding only the pixels the
    /// map changed, laid over the background, so an HD background still shows elsewhere.
    /// </summary>
    public class AutomapRender : UWClass
    {
        /// <summary>How each tile type is drawn, a 3x3 pattern ([type][row][column]): 1 the terrain's colour, 2 darkened.</summary>
        static readonly byte[,,] TilePattern =
        {
            { { 1, 1, 1 }, { 1, 1, 1 }, { 1, 1, 1 } },
            { { 2, 1, 1 }, { 0, 2, 1 }, { 0, 0, 2 } },
            { { 1, 1, 2 }, { 1, 2, 0 }, { 2, 0, 0 } },
            { { 0, 0, 2 }, { 0, 2, 1 }, { 2, 1, 1 } },
            { { 2, 0, 0 }, { 1, 2, 0 }, { 1, 1, 2 } },
        };
        static readonly int[] DiagSide = { 1, 2, 0, 3 };
        static readonly int[] DoorDy = { -1, 0, -1, 1 };
        static readonly int[] DoorDx = { 0, -1, -1, -1 };

        static byte[] map;
        static byte[] screen;
        static bool[] changed;

        static int Rand() => Rng.r.Next(0x8000);

        static int At(int x, int y) => (x >= 0 && x < 320 && y >= 0 && y < 200) ? (199 - y) * 320 + x : -1;

        static void SetPixel(int x, int y, int colour)
        {
            var i = At(x, y);
            if (i >= 0)
            {
                screen[i] = (byte)colour;
                changed[i] = true;
            }
        }

        /// <summary>PixelDarken: the colour at x, y plus base and a random 0 to n.</summary>
        static void PixelDarken(int x, int y, int b, int n)
        {
            var i = At(x, y);
            if (i < 0)
            {
                return;
            }
            var d = 0x7FFF / n;
            Rand();//the original draws a first value it does not use
            SetPixel(x, y, b + Rand() / d + screen[i]);
        }

        static int Square(int x, int y) => map[(y << 6) + x];

        /// <summary>
        /// Returns the automap of the level as an image the size of the screen.
        /// </summary>
        public static ImageTexture MapImage(int levelno)
        {
            var background = uimanager.bitmaps.RawScreen(BytLoader.BLNKMAP_BYT);
            var palette = uimanager.bitmaps.ScreenPalette(BytLoader.BLNKMAP_BYT);
            var output = Image.CreateEmpty(320, 200, false, Image.Format.Rgba8);
            if ((background == null) || (background.Length < 320 * 200) || (automap.automaps[levelno] == null))
            {
                return ImageTexture.CreateFromImage(output);
            }
            Draw(automap.automaps[levelno].buffer, background);

            //the player's mark: UW2 only while the player knows where they are, UW1 not in the Ethereal Void
            if ((levelno == playerdat.dungeon_level - 1)
                && (_RES == GAME_UW2 ? playerdat.AutomapEnabled : playerdat.dungeon_level != 9))
            {
                DrawPlayerMark(((playerdat.playerObject.tileX - 1) * 3) + 10 - 1, ((playerdat.playerObject.tileY - 1) * 3) + 12);
            }

            for (int i = 0; i < 320 * 200; i++)
            {
                if (changed[i])
                {
                    output.SetPixel(i % 320, i / 320, palette.ColorAtIndex(screen[i], false, false));
                }
            }
            map = null; screen = null; changed = null;
            return ImageTexture.CreateFromImage(output);
        }

        /// <summary>
        /// ShowDungeonMap on a copy of the background's indices: returns the drawn indices; Changed then marks the
        /// pixels the map set (row 0 is the top of the screen).
        /// </summary>
        public static byte[] Draw(byte[] mapBuffer, byte[] background)
        {
            map = mapBuffer;
            screen = (byte[])background.Clone();
            changed = new bool[320 * 200];
            ShowDungeonMap();
            return screen;
        }

        /// <summary>The pixels the last Draw changed.</summary>
        public static bool[] Changed => changed;

        /// <summary>
        /// pic_to_screen of the buttons' image 0x3F with transparency (colour 0 not drawn), its top left at px, py.
        /// </summary>
        static void DrawPlayerMark(int px, int py)
        {
            if (uimanager.grButtons == null || !uimanager.grButtons.RawImage(0x3F, out var pixels, out var w, out var h))
            {
                return;
            }
            for (int row = 0; row < h; row++)
            {
                for (int col = 0; col < w; col++)
                {
                    var c = pixels[row * w + col];
                    if (c != 0)
                    {
                        SetPixel(px + col, py - row, c);
                    }
                }
            }
        }

        static void ShowDungeonMap()
        {
            var side = new bool[4];
            for (int y = 1; y < 0x3F; y++)
            {
                for (int x = 1; x < 0x3F; x++)
                {
                    var t = Square(x, y) & 0xF;
                    if (t <= 0 || t >= 10)
                    {
                        continue;
                    }
                    DoTile(t, x, y);
                    System.Array.Clear(side);
                    if (t >= 2 && t <= 5)
                    {//a diagonal: only the two sides of its open half
                        var d = DiagSide[t - 2];
                        side[d] = ShadeSide(d, x, y);
                        d = (d + 1) & 3;
                        side[d] = ShadeSide(d, x, y);
                    }
                    else
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            side[i] = ShadeSide(i, x, y);
                        }
                    }
                    var px = x * 3 + 10;
                    var py = y * 3 + 7;
                    for (int i = 0; i < 4; i++)
                    {
                        if (side[i] && side[(i + 1) & 3])
                        {
                            PixelDarken(px - ((i & 2) << 1), py - ((i & 2) << 1), 3, 2);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// ShadeSide: an edge along one side unless the square beyond is one of the seen types (1-9); beyond it an
        /// unexplored open square from a map piece (type 11) gets a lighter one. Returns whether it drew.
        /// </summary>
        static bool ShadeSide(int s, int x, int y)
        {
            var px = (x - 1) * 3 + 10;
            var py = (y - 1) * 3 + 7;
            switch (s)
            {
                case 0: y++; break;
                case 1: x++; break;
                case 2: y--; break;
                case 3: x--; break;
            }
            var t = Square(x, y) & 0xF;
            if (t > 0 && t < 10)
            {
                return false;
            }
            int a = 6, b = 2;
            if (t == 11)
            {
                a = 0; b = 4;
            }
            switch (s)
            {
                case 0:
                case 2:
                    py += (s == 0) ? 3 : -1;
                    for (int i = 0; i < 3; i++)
                    {
                        PixelDarken(px + i, py, a, b);
                    }
                    break;
                case 1:
                case 3:
                    px += (s == 1) ? 3 : -1;
                    for (int i = 0; i < 3; i++)
                    {
                        PixelDarken(px, py + i, a, b);
                    }
                    break;
            }
            return true;
        }

        /// <summary>
        /// DoTile: the pattern of the type (slopes drawn as open), coloured by the terrain, then the kind: dark (a stairs
        /// texture), the 0xE9 colours (a bridge) or a door. UW1 keeps the terrain in bits 4-5 and the kind in 6-7, UW2 the
        /// other way round, where a bridge or stairs takes no terrain colour and ice has colours of its own.
        /// </summary>
        static void DoTile(int type, int x, int y)
        {
            var b = Square(x, y);
            int shade, kind;
            if (_RES == GAME_UW2)
            {
                shade = b >> 6;
                kind = (b >> 4) & 3;
                if (kind == 3 || kind == 2)
                {
                    shade = 0;
                }
            }
            else
            {
                shade = (b >> 4) & 3;
                kind = b >> 6;
            }
            var px = (x - 1) * 3 + 10;
            var py = (y - 1) * 3 + 7;
            if (type >= 6)
            {
                type = 1;
            }
            type--;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    switch (TilePattern[type, j, i])
                    {
                        case 1:
                            switch (shade)
                            {
                                case 0:
                                    PixelDarken(px + i, py + j, 2, 3);
                                    break;
                                case 1:
                                    SetPixel(px + i, py + j, Rand() % (_RES == GAME_UW2 ? 3 : 2) + 0xB1);
                                    break;
                                case 2:
                                    SetPixel(px + i, py + j, Rand() % (_RES == GAME_UW2 ? 3 : 2) + 0xB5);
                                    break;
                                case 3:
                                    if (_RES == GAME_UW2)
                                    {
                                        SetPixel(px + i, py + j, Rand() % 3 + 0xB9);
                                    }
                                    break;
                            }
                            break;
                        case 2:
                            PixelDarken(px + i, py + j, 6, 2);
                            break;
                    }
                }
            }
            switch (kind)
            {
                case automaptileinfo.KindStair:
                    for (int i = 0; i < 3; i++)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            PixelDarken(px + i, py + j, 6, 3);
                        }
                    }
                    break;
                case automaptileinfo.KindBridge:
                    for (int j = 0; j < 3; j++)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            SetPixel(px + i, py + j, (int)((long)Rand() * 3 / 0x8000) + 0xE9);
                        }
                    }
                    break;
                case automaptileinfo.KindDoor:
                    DoDoorTile(x, y, px, py);
                    break;
            }
        }

        /// <summary>DoDoorTile: a dark centre and two dark pixels across the passage, facing open (type 1) neighbours.</summary>
        static void DoDoorTile(int x, int y, int px, int py)
        {
            PixelDarken(++px, ++py, 6, 3);
            for (int dir = 0; dir < 4; dir++)
            {
                if (((Square(x + DoorDx[dir], y + DoorDy[dir]) & 0xF) == 1)
                    || ((Square(x - DoorDx[dir], y - DoorDy[dir]) & 0xF) == 1))
                {
                    PixelDarken(px + DoorDy[dir], py + DoorDx[dir], 6, 3);
                    PixelDarken(px - DoorDy[dir], py - DoorDx[dir], 6, 3);
                    break;
                }
            }
        }
    } //end class
}
