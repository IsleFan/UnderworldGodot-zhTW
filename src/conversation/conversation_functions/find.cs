namespace Underworld
{
    public partial class ConversationVM : UWClass
    {
        /// <summary>
        /// find(arg3 array, arg2 count, arg1 value): the 1-based position of the value in the array's first count words,
        /// or 0 (conv_find). The array is passed by address.
        /// </summary>
        public static void find()
        {
            var value = GetConvoStackValueAtPtr(stack + stackptr - 1);
            var count = GetConvoStackValueAtPtr(stack + stackptr - 2);
            var start = at(stack + stackptr - 3);
            result_register = 0;
            for (int i = 0; i < count; i++)
            {
                if (at(start + i) == value)
                {
                    result_register = i + 1;
                    return;
                }
            }
        }
    }//end class
}//end namespace
