namespace Underworld
{
    public partial class ConversationVM : UWClass
    {
        /// <summary>
        /// plural(arg3 count, arg2 singular, arg1 plural): the plural string's id when the count is above 1 (babPluralize).
        /// </summary>
        public static void plural()
        {
            var count = GetConvoStackValueAtPtr(stack + stackptr - 3);
            var singular = GetConvoStackValueAtPtr(stack + stackptr - 2);
            var pluralString = GetConvoStackValueAtPtr(stack + stackptr - 1);
            result_register = count > 1 ? pluralString : singular;
        }
    }//end class
}//end namespace
