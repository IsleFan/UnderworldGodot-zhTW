namespace Underworld
{
    public partial class ConversationVM : UWClass
    {
        public static void set_inv_quality()
        {
            var index = GetConvoStackValueAtPtr(stack + stackptr - 2);
            var newQuality = GetConvoStackValueAtPtr(stack + stackptr - 1);
            var obj = UWTileMap.current_tilemap.LevelObjects[index];
            obj.quality = (short)(newQuality & 0x3F);//six bits; it returns 1 (set_inv_quality)
            result_register = 1;
        }
    }//end class
}//end namespace