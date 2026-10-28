namespace Underworld
{
    public partial class ConversationVM : UWClass
    {
        /// <summary>
        /// val(arg1): the string's value as a decimal number: leading spaces skipped, an optional minus, then digits (DoVal).
        /// </summary>
        public static void val()
        {
            var text = getString(GetConvoStackValueAtPtr(stack + stackptr - 1));
            var i = 0;
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            if (text.StartsWith("-"))
            {//(the original tests the very first character for the sign)
                i++;
            }
            var n = 0;
            while (i < text.Length && text[i] >= '0' && text[i] <= '9')
            {
                n = n * 10 + (text[i] - '0');
                i++;
            }
            result_register = text.StartsWith("-") ? -n : n;
        }
    }//end class
}//end namespace
