using System.Collections;
using Peaky.Coroutines;

namespace Underworld
{
    /// <summary>
    /// Code for Input and output relating to conversation VM.
    /// </summary>
    public partial class ConversationVM : UWClass
    {
        public static int PlayerNumericAnswer;
        public static bool WaitingForInput;

        /// <summary>
        /// menus_active: a menu of answers is on the scroll. Another message printed there clears it (scroll_print),
        /// and the menu waits 500 ticks (or a key or click) and shows the answers again (converse_event_loop).
        /// </summary>
        public static bool MenusActive;

        /// <summary>
        /// Waits for the player's answer to the menu on the scroll, showing the answers again whenever another
        /// message has cleared them.
        /// </summary>
        static IEnumerator WaitForMenuAnswer(string menutext)
        {
            MenusActive = true;
            WaitingForInput = true;
            while (WaitingForInput)
            {
                if (!MenusActive)
                {
                    yield return MessageDisplay.ScrollWait(0x1F4);
                    if (WaitingForInput)
                    {
                        uimanager.instance.scroll.Clear();
                        uimanager.AddToMessageScroll(menutext);
                        MenusActive = true;
                    }
                    continue;
                }
                yield return new WaitOneFrame();
            }
            MenusActive = false;
        }

        /// <summary>
        /// Gets the specified string for the currently referenced conversation.
        /// </summary>
        /// <param name="stringno"></param>
        /// <returns></returns>
        public static string getString(int stringno, bool replaceValues = false)
        {
            if (replaceValues)
            {//only ever shown, so the template and what fills it are the translations when there are any
                return TextSubstitute(GameStrings.GetDisplayString(currentConversation.StringBlock, stringno));
            }
            else
            {
                if (stringno >= 512)
                {//get string by direct full reference
                    return GameStrings.GetString(stringno);
                }
                else
                {
                    return GameStrings.GetString(currentConversation.StringBlock, stringno);
                }
            }
        }


        /// <summary>
        /// getString for text that is only shown: the translation when one is loaded. Object
        /// names and descriptions stay English, as the game composes those (see Localization).
        /// </summary>
        static string getDisplayString(int stringno)
        {
            if (stringno >= 512)
            {
                var block = stringno >> 9;
                if (block == 4 || block == 5)
                {
                    return GameStrings.GetString(stringno);
                }
                return GameStrings.GetDisplayString(stringno);
            }
            return GameStrings.GetDisplayString(currentConversation.StringBlock, stringno);
        }

        /// <summary>
        /// For substituting calculated values into strings (convert_string): each @-variable is @, the kind
        /// (G a global, S a stack slot, P a parameter, C a constant), then for all but C the type (I an integer,
        /// anything else a string number), then the number, optionally followed by another variable giving a
        /// 1-based index. "@@" is a literal @. A substituted string is substituted in turn.
        /// G reads mem[number + index - 1], S the local stack[bp + number + index - 1], P the address held in
        /// stack[bp + number] plus index - 1, and C is the number itself.
        /// </summary>
        public static string TextSubstitute(string input, int depth = 0)
        {
            //Remove \1 and \0 which wraps the reprinting of player typed input in conversations.
            input = input.Replace("\\1", "").Replace("\\0", "");

            if (!input.Contains('@'))
            {
                return input;
            }
            var output = new System.Text.StringBuilder();
            var pos = 0;
            while (pos < input.Length)
            {
                if (input[pos] == '@' && pos + 1 < input.Length)
                {
                    pos++;
                    if (input[pos] != '@')
                    {
                        var value = ReadAtVariable(input, ref pos, false, out char type);
                        if (type == 'I')
                        {
                            output.Append(value);
                        }
                        else if (depth < 8)
                        {//(the original has no depth limit)
                            output.Append(TextSubstitute(getDisplayString(value), depth + 1));
                        }
                        continue;
                    }
                }
                output.Append(input[pos++]);
            }
            return output.ToString();
        }

        /// <summary>
        /// Reads one @-variable after its @ (kind, type, number and any index variable) and returns its value.
        /// </summary>
        static short ReadAtVariable(string input, ref int pos, bool isIndex, out char type)
        {
            var kind = input[pos++];
            type = 'I';
            if (kind != 'C' && pos < input.Length)
            {
                type = input[pos++];
            }
            if (isIndex && type != 'I')
            {//AtIndex: only an integer index counts; its number is left in the text
                return 0;
            }
            //atoi: an optional sign then digits; then every digit and '-' is skipped
            var numstart = pos;
            if (pos < input.Length && (input[pos] == '-' || input[pos] == '+')) pos++;
            while (pos < input.Length && char.IsAsciiDigit(input[pos])) pos++;
            int.TryParse(input.Substring(numstart, pos - numstart), out int number);
            while (pos < input.Length && (char.IsAsciiDigit(input[pos]) || input[pos] == '-')) pos++;

            var extra = 0;
            if (pos < input.Length && (input[pos] == 'G' || input[pos] == 'S' || input[pos] == 'P' || input[pos] == 'C'))
            {//an index variable (AtIndex)
                extra = ReadAtVariable(input, ref pos, true, out _) - 1;
            }
            switch (kind)
            {
                case 'G':
                    return at(number + extra);
                case 'P':
                    return at(at(stack + basep + number) + extra);
                case 'S':
                    return at(stack + basep + number + extra);
                default:
                    return (short)number;
            }
        }
    }//end class
}//end namespace