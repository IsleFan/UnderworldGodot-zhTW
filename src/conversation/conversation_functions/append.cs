namespace Underworld
{
    public partial class ConversationVM : UWClass
    {
        /// <summary>
        /// append(arg2, arg1): a new string, arg2's followed by arg1's (babl_str_append).
        /// </summary>
        public static void append()
        {
            var first = getString(GetConvoStackValueAtPtr(stack + stackptr - 2));
            var second = getString(GetConvoStackValueAtPtr(stack + stackptr - 1));
            result_register = GameStrings.AddString(currentConversation.StringBlock, first + second);
        }
    }//end class
}//end namespace
