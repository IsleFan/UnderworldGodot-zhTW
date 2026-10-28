namespace Underworld
{
    public partial class ConversationVM : UWClass
    {
        /// <summary>
        /// copy(arg1): a new string holding a copy of arg1's (STRING_COPY).
        /// </summary>
        public static void copy()
        {
            result_register = GameStrings.AddString(currentConversation.StringBlock, getString(GetConvoStackValueAtPtr(stack + stackptr - 1)));
        }
    }//end class
}//end namespace
