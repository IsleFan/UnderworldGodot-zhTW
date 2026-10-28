using System.Diagnostics;

namespace Underworld
{
    public partial class ConversationVM : UWClass
    {
        /// <summary>
        /// x_obj_pos(arg5 object, arg4 mode, arg3 x, arg2 y, arg1 z), x, y and z passed by address; a variable holding
        /// -1 is left alone. Mode 1 sets the fine position and height (a height above 0x7F means the floor of square
        /// x, y); mode 2 reads back the square and height; any other mode the fine position and height.
        /// </summary>
        public static void x_obj_pos()
        {
            var xAddress = at(stack + stackptr - 3);
            var yAddress = at(stack + stackptr - 2);
            var zAddress = at(stack + stackptr - 1);
            var objindex = at(at(stack + stackptr - 5));
            var mode = at(at(stack + stackptr - 4));
            var x = at(xAddress); var y = at(yAddress); var z = at(zAddress);

            var obj = UWTileMap.current_tilemap.LevelObjects[objindex];

            switch (mode)
            {
                case 1:
                    if (x != -1)
                    {
                        obj.xpos = (short)x;
                    }
                    if (y != -1)
                    {
                        obj.ypos = (short)y;
                    }
                    if (z != -1)
                    {
                        if (z > 0x7F)
                        {
                            if (UWTileMap.ValidTile(x, y))
                            {
                                obj.zpos = (short)(UWTileMap.current_tilemap.Tiles[x, y].floorHeight << 3);
                            }
                        }
                        else
                        {
                            obj.zpos = (short)z;
                        }
                    }
                    objectInstance.Reposition(obj);
                    break;
                case 2:
                    if (x != -1)
                    {
                        Set(xAddress, obj.tileX);
                    }
                    if (y != -1)
                    {
                        Set(yAddress, obj.tileY);
                    }
                    if (z != -1)
                    {
                        Set(zAddress, obj.zpos);
                    }
                    break;
                default:
                    if (x != -1)
                    {
                        Set(xAddress, obj.xpos);
                    }
                    if (y != -1)
                    {
                        Set(yAddress, obj.ypos);
                    }
                    if (z != -1)
                    {
                        Set(zAddress, obj.zpos);
                    }
                    break;
            }
        }
    } //end class
}//end namespace
