using System.Diagnostics;

namespace Underworld
{
    public partial class ConversationVM : UWClass
    {
        public static void contains()
        {
            int[] args = new int[2];
            args[0] = at(stack + stackptr - 1);
            args[1] = at(stack + stackptr - 2);

            //StringContains: arg2's string as a whole word of arg1's, bounded by the ends, white space or punctuation,
            //after @-substitution. The original lowercases the source strings but searches the substituted copies,
            //so case is ignored only in a string with nothing to substitute. (It lowercases them in place; this does not.)
            var source1 = getString(at(args[0]));
            var source2 = getString(at(args[1]));
            var string1 = source1.Contains('@') ? TextSubstitute(source1) : TextSubstitute(source1).ToLowerInvariant();
            var string2 = source2.Contains('@') ? TextSubstitute(source2) : TextSubstitute(source2).ToLowerInvariant();

            Debug.Print($"Check if string {string1} contains {string2}");
            result_register = 0;
            if (string2.Length == 0)
            {
                return;
            }
            var from = 0;
            while ((from = string1.IndexOf(string2, from, System.StringComparison.Ordinal)) >= 0)
            {
                var end = from + string2.Length;
                var startsWord = from == 0 || char.IsWhiteSpace(string1[from - 1]) || char.IsPunctuation(string1[from - 1]) || char.IsSymbol(string1[from - 1]);
                var endsWord = end == string1.Length || char.IsWhiteSpace(string1[end]) || char.IsPunctuation(string1[end]) || char.IsSymbol(string1[end]);
                if (startsWord && endsWord)
                {
                    result_register = 1;
                    return;
                }
                from = end;
            }
        }
    }//end class
}//end namespace