namespace Underworld
{
    /// <summary>
    /// Storing and retreval of info about automap tiles
    /// </summary>
    public class automaptileinfo : UWClass
    {
        public const int DisplayTypeClear = 0;

        //The high nibble holds two fields: the kind (GRIDDB's curautocode, from the objects drawn on the tile)
        //and the floor's terrain class. UW2 keeps the kind in bits 4-5 and the terrain in bits 6-7 (DoTile);
        //UW1 the other way round, terrain in bits 4-5 and kind in bits 6-7.
        public const int KindDoor = 1;
        public const int KindBridge = 2;
        public const int KindStair = 3;//a stairs texture map, drawn dark
        public const int TerrainWater = 1;
        public const int TerrainLava = 2;
        public const int TerrainIce = 3;//UW2 only

        public const int TILE_SOLID = 0;
        public const int TILE_OPEN = 1;
        public const int TILE_DIAG_SE = 2;
        public const int TILE_DIAG_SW = 3;
        public const int TILE_DIAG_NE = 4;
        public const int TILE_DIAG_NW = 5;
        public const int TILE_SLOPE_N = 6;
        public const int TILE_SLOPE_S = 7;
        public const int TILE_SLOPE_E = 8;
        public const int TILE_SLOPE_W = 9;

        public static byte[] UndiscoveredTiles = new byte[] { 0xA, 0xB, 0xC, 0xD, 0xE, 0xF, 0xB, 0xB, 0xB, 0xB, 0xA, 0xB, 0xC, 0xD, 0xE, 0xF };

        /// <summary>
        /// Location of data in the buffer
        /// </summary>
        public int PTR;

        /// <summary>
        /// Reference to the data buffer where the tile data is held
        /// </summary>
        public byte[] buffer;
        /// <summary>
        /// How the tile is rendered
        /// </summary>
        public short DisplayType
        {
            get
            {
                return (short)((buffer[PTR] >> 4) & 0xf);
            }
            set
            {
                buffer[PTR] &= 0x0F;
                buffer[PTR] |= (byte)(value << 4);
            }
        }

        /// <summary>
        /// The type of tile. When set means tile has been visited
        /// </summary>
        public short tileType
        {
            get
            {
                return (short)(buffer[PTR] & 0xF);
            }
            set
            {
                buffer[PTR] &= 0xF0;
                buffer[PTR] |= (byte)(value & 0xF);
            }
        }

        public automaptileinfo(int _ptr, ref byte[] _buffer)
        {
            PTR = _ptr;
            buffer = _buffer;
        }

        public bool visited
        {
            get
            {
                switch (tileType)
                {
                    case TILE_SOLID:
                        return false;
                    default:
                        return true;
                }
            }
        }

        /// <summary>
        /// The kind of thing on the tile: 1 a door, 2 a bridge, 3 a stairs texture.
        /// </summary>
        public int Kind
        {
            get
            {
                return _RES == GAME_UW2 ? DisplayType & 3 : DisplayType >> 2;
            }
        }

        /// <summary>
        /// The floor's terrain class: 1 water, 2 lava, 3 ice.
        /// </summary>
        public int Terrain
        {
            get
            {
                return _RES == GAME_UW2 ? DisplayType >> 2 : DisplayType & 3;
            }
        }

        /// <summary>
        /// The bits a kind takes in the automap byte (curautocode shifted into place).
        /// </summary>
        public static byte KindBits(int kind)
        {
            return (byte)(kind << (_RES == GAME_UW2 ? 4 : 6));
        }

        /// <summary>
        /// The automap byte of a tile seen close up: its type and its floor's terrain bits (grdb_elem).
        /// UW2 takes the terrain class, bits 6-7 of the floor's terrain; UW1 the floor's terrain as it is.
        /// </summary>
        public static byte TileCode(TileInfo t)
        {
            if (_RES == GAME_UW2)
            {
                return (byte)(t.tileType | TerrainDatLoader.GetTerrainDataBit67_unshifted(t));
            }
            return (byte)(t.tileType | (short)TerrainDatLoader.getTerrain(t.terrain));
        }

        public bool IsBridge
        {
            get
            {
                return Kind == KindBridge;
            }
        }

        public bool IsWater
        {
            get
            {
                return Terrain == TerrainWater;
            }
        }

        public bool IsLava
        {
            get
            {
                return Terrain == TerrainLava;
            }
        }

        public bool IsStair
        {
            get
            {
                return Kind == KindStair;
            }
        }

        public bool IsIce
        {
            get
            {
                return _RES == GAME_UW2 && Terrain == TerrainIce;
            }
        }

        public bool IsOpen
        {
            get
            {
                switch (tileType)
                {
                    case TILE_OPEN:
                    case TILE_SLOPE_N:
                    case TILE_SLOPE_S:
                    case TILE_SLOPE_E:
                    case TILE_SLOPE_W:
                        {
                            return true;
                        }
                    default:
                        {
                            return false;
                        }
                }
            }
        }

        public bool IsDoor
        {
            get
            {
                return Kind == KindDoor;
            }
        }


    }//end class
}//end namespace