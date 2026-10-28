using Godot;
using Peaky.Coroutines;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;

namespace Underworld
{

    /// <summary>
    /// Class for processing message output being sent to the screen.
    /// </summary>
    public class MessageDisplay
    {
        /// <summary>
        /// DOS's seven message scroll escape colours, \0 to \6, as palette 0 indices.
        ///
        /// UW1 writes them as consecutive immediates through its font colour global in the
        /// scroll parser, UW.EXE 0x3976E through 0x397AA. UW2 looks them up in a table at
        /// UW2.EXE 0x69407, bytes 75 68 01 02 21 50 48.
        ///
        /// Three of these used to be hardcoded hex here, which is why they drifted: #883E14
        /// for \1 where DOS has (132,68,32) in UW1 and (124,64,36) in UW2, so it did not vary
        /// by game at all; white for \3 where DOS has (252,252,252); and 0x76 for \0 in UW2
        /// where the table says 0x75. Only the black for \2 was right.
        /// </summary>
        private static readonly byte[] ScrollColoursUw1 = { 0x2E, 0x26, 0xF1, 0x60, 0xB4, 0xC4, 0xD4 };
        private static readonly byte[] ScrollColoursUw2 = { 0x75, 0x68, 0x01, 0x02, 0x21, 0x50, 0x48 };

        /// <summary>The colour for one of DOS's scroll escapes, falling back to \0.</summary>
        public static string ScrollColour(int escape)
        {
            var table = UWClass._RES == UWClass.GAME_UW2 ? ScrollColoursUw2 : ScrollColoursUw1;
            if (escape < 0 || escape >= table.Length) { escape = 0; }
            return PaletteLoader.ToBBCode(0, table[escape]);
        }

        public enum MessageDisplayMode
        {
            NormalMode = 0,
            TypedInput = 1,
            TemporaryMessage = 2
        }
        /// <summary>
        /// Player has been prompted with a [MORE] wait
        /// </summary>
        public static bool WaitingForMore = false;

        /// <summary>
        /// Player has to type an input string
        /// </summary>
        public static bool WaitingForTypedInput = false;
        /// <summary>wdialog's negative position: the offered text is still untouched, and the first character typed replaces it.</summary>
        public static bool TypedInitialPending = false;

        public static bool WaitingForYesOrNo = false;
        public static string YesNoOption = "Yes";

        /// <summary>
        /// The stack a "Move how many?" prompt is for (AskHowMany), 0 for other typed input: a click answers it, the left
        /// button with one, the right with the whole stack, both with none.
        /// </summary>
        public static int QuantityPrompt = 0;

        /// <summary>Yes or No as the scroll shows it.</summary>
        public static string YesNoText(string option) => ZhText.Enabled ? (option == "Yes" ? "是" : "否") : option;

        /// <summary>
        /// Writes the answer into the prompt's line for good (wd_bool, wd_replace): the question and its answer stay on the
        /// scroll, as the original prints them into it.
        /// </summary>
        public void FreezeTypedInput(string answer)
        {
            for (int i = 0; i <= Lines.GetUpperBound(0); i++)
            {
                if (Lines[i].LineText.Contains("{TYPEDINPUT}"))
                {
                    Lines[i].SetLine(Lines[i].LineText.Replace("{TYPEDINPUT}|", answer).Replace("{TYPEDINPUT}", answer), Lines[i].OptionNo);
                }
            }
            UpdateMessageDisplay();
        }

        public RichTextLabel[] OutputControl;

        public MessageScrollLine[] Lines = new MessageScrollLine[5];

        /// <summary>
        /// The number of characters including spaces to print on a single line
        /// </summary>
        public int Columns = 64;

        /// <summary>
        /// The number of rows of text to print.
        /// </summary>
        public int Rows = 5;

        public int LinePtr = 0;
        public void Clear()
        {
            for (int i = 0; i <= Lines.GetUpperBound(0); i++)
            {
                Lines[i].SetLine("");
            }
            LinePtr = 0;
            HighlightOption = 0;
            UpdateMessageDisplay();
        }

        /// <summary>The conversation answer drawn highlighted (the one under the mouse), 0 for none.</summary>
        public int HighlightOption = 0;

        /// <summary>The highlight: a bright gold, so the answer that a click would pick stands out.</summary>
        const string HighlightColour = "#f8d860";

        static readonly System.Text.RegularExpressions.Regex Tags = new(@"\[[^\]]*\]");
        static readonly System.Text.RegularExpressions.Regex AnswerStart = new(@"^\s*(\d+)\.\s");

        /// <summary>The answer number a line starts ("2. ..." gives 2), 0 for any other line.</summary>
        static int AnswerNumber(string lineText)
        {
            var m = AnswerStart.Match(Tags.Replace(lineText ?? "", ""));
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        /// <summary>
        /// The answer line <paramref name="line"/> belongs to while a conversation menu is shown: the
        /// last answer number at or above it, as a long answer goes on over the lines below its number.
        /// </summary>
        public int AnswerOfLine(int line)
        {
            int answer = 0;
            for (int i = 0; i <= line && i <= Lines.GetUpperBound(0); i++)
            {
                int n = AnswerNumber(Lines[i].LineText);
                if (n > 0) { answer = n; }
                else if (string.IsNullOrWhiteSpace(Tags.Replace(Lines[i].LineText ?? "", ""))) { answer = 0; }
            }
            return answer;
        }

        private IEnumerator AddLineWithMore(string newText, int Option, int Colour = 0)
        {
            if (newText.Trim() == "") { yield return 0; }
            //[MORE] is drawn in UW2's colour 0x12 (scroll_more); UW1's 0x60 is \3, the colour it is given
            var colourCode = (newText == "[MORE]" && UWClass._RES == UWClass.GAME_UW2) ? PaletteLoader.ToBBCode(0, 0x12) : ScrollColour(Colour);
            if (newText == "[MORE]" && ZhText.Enabled)
            {//shown in the Chinese font, the size of the text around it; "[MORE]" stays the marker AddText and AddLine split on
                newText = ZhText.Compose("［繼續］");
            }
            newText = $"[color={colourCode}]{newText}[/color]";
            if (LinePtr <= Lines.GetUpperBound(0))
            {
                Lines[LinePtr++].SetLine(newText, Option);
            }
            else
            {
                //shift all lines up.
                for (int i = 1; i <= Lines.GetUpperBound(0); i++)
                {
                    Lines[i - 1].SetLine(Lines[i].LineText, Lines[i].OptionNo);
                    //TODO associate option no with a click event index.
                }
                Lines[Rows - 1].SetLine(newText, Option);
            }

            UpdateMessageDisplay();
            yield return 0;
        }


        public IEnumerator AddLine(string newText, int Option, int Colour = 0, bool ForceMore = false)
        {
            if (newText.Trim() != "")
            {
                if (ForceMore)
                {//causes [more] to happen without splitting.
                    yield return AddLineWithMore(newText: newText, Option: Option, Colour: ConversationVM.UI_SAY);
                    WaitingForMore = true;
                    while (WaitingForMore)
                    {
                        yield return new WaitOneFrame();
                    }
                    LinePtr--;//overwrite last line
                }
                else
                {
                    var newTextSplit = newText.Split("[MORE]");
                    for (int i = 0; i <= newTextSplit.GetUpperBound(0); i++)
                    {
                        if (i > 0)
                        {
                            yield return AddLineWithMore(newText: "[MORE]", Option: Option, Colour: ConversationVM.UI_SAY);
                            WaitingForMore = true;
                            while (WaitingForMore)
                            {
                                yield return new WaitOneFrame();
                            }
                            LinePtr--;//overwrite last line
                        }
                        yield return AddLineWithMore(newText: newTextSplit[i], Option: Option, Colour: Colour);
                    }
                    yield return 0;
                }
            }


        }

        /// <summary>
        /// Outputs all text to the control
        /// </summary>
        public void UpdateMessageDisplay()
        {
            //output all text 
            var output = "";
            for (int i = 0; i <= Lines.GetUpperBound(0); i++)
            {
                if (output.Length > 0)
                {
                    output += "\n";
                }
                var line = Lines[i].LineText;
                if (HighlightOption > 0 && AnswerOfLine(i) == HighlightOption)
                {
                    line = System.Text.RegularExpressions.Regex.Replace(line, @"\[color=[^\]]*\]", $"[color={HighlightColour}]");
                }
                output += line;
            }
            //Replace special characters
            if (MessageDisplay.WaitingForYesOrNo)
            {
                uimanager.instance.TypedInput.Text = MessageDisplay.YesNoOption;
                output = output.Replace("{TYPEDINPUT}", YesNoText(MessageDisplay.YesNoOption));
            }
            else
            {
                if (MessageDisplay.WaitingForTypedInput)
                {
                    output = output.Replace("{TYPEDINPUT}", uimanager.instance.TypedInput.Text);
                }
                else if (uimanager.SaveDescriptionPromptActive)
                {
                    // The save description prompt keeps its own state rather than the shared
                    // WaitingForTypedInput, because it has to tell an empty commit from a
                    // cancel and the two mean opposite things.
                    output = output.Replace("{TYPEDINPUT}", uimanager.SaveDescriptionText);
                }
            }

            OutputControl[0].Text = output;
        }

        /// <summary>
        /// Columns a Chinese character takes. Columns counts the original font's letters, and a
        /// Chinese character is drawn an em half a row shorter than them (see CjkGlyphs), about
        /// as wide as one and a third.
        /// </summary>
        const float WideColumns = 1.3f;
        /// <summary>Columns a gloss's small Latin character takes (CjkGlyphs.ShrinkGlosses).</summary>
        const float SmallColumns = 0.6f;

        /// <summary>Punctuation that may not start a line, and that may not end one.</summary>
        const string NoLineStart = "，。、；：！？）」』】…—";
        const string NoLineEnd = "（「『【";

        internal static float Width(string text)
        {
            float w = 0;
            foreach (var c in text)
            {
                w += CjkGlyphs.IsSmall(c) ? SmallColumns : CjkGlyphs.IsWide(c) ? WideColumns : 1;
            }
            return w;
        }

        /// <summary>
        /// The units a line may break between, each with what follows it: the words between
        /// spaces, as before, and inside a word each Chinese character on its own, so Chinese
        /// text, which has no spaces, can wrap. Punctuation stays with the character it follows
        /// or, for opening brackets, the one it precedes.
        /// </summary>
        internal static List<string> Tokenise(string line, out List<string> seps)
        {
            var words = new List<string>();
            seps = new List<string>();
            foreach (var word in line.Split(' '))
            {
                int first = words.Count;
                var piece = new StringBuilder();
                bool pieceWide = false;
                foreach (var c in word)
                {
                    bool wide = CjkGlyphs.IsWide(c) && !CjkGlyphs.IsSmall(c); //a gloss is a word
                    bool joinPrevious = NoLineStart.IndexOf(c) >= 0
                        || (!wide && !pieceWide)
                        || (piece.Length > 0 && NoLineEnd.IndexOf(piece[^1]) >= 0);
                    if (piece.Length > 0 && !joinPrevious)
                    {
                        words.Add(piece.ToString());
                        seps.Add("");
                        piece.Clear();
                    }
                    piece.Append(c);
                    pieceWide = wide;
                }
                if (piece.Length > 0 || words.Count == first)
                {
                    words.Add(piece.ToString());
                    seps.Add("");
                }
                seps[^1] = " ";
            }
            return words;
        }

        const string PauseLong = "\u0001";//\p
        const string PauseShort = "\u0002";//\P
        static readonly System.Text.RegularExpressions.Regex ColourCode = new(@"\\([0-6])");

        /// <summary>
        /// Prints text to the scroll (scroll_print), wrapping it at Columns and splitting it at newlines. The original's
        /// escapes: \m shows [MORE]; \0 to \6 change the colour (from the line they are on); \p waits 600 ticks and
        /// \P 200, or until a key or click, and the text then goes on along the same line. A message shows Rows lines
        /// before a [MORE] (the lines on screen before it may scroll away unseen, the message's own may not), and Rows - 1
        /// after each one. A line that starts in the message's final piece may take one more (scroll_print3's flag 0).
        /// </summary>
        public IEnumerator AddText(string newText, int option = -1, int colour = 0)
        {
            if (newText.EndsWith("\n"))
            {//trims an ending new line
                newText = newText.Substring(0, newText.Length - 1);
            }
            int NoOfRowsNeeded = 0;
            int limit = Rows;
            //the visible characters (not counting spaces) still to show, and those of the final piece
            int unshown = NonSpace(newText);
            int finalPiece = NonSpace(newText.Substring(FinalPieceStart(newText)));
            int lastLineCount = 0;
            //split by new lines
            newText = newText.Replace("\\m", " [MORE] ").Replace("\\p", $" {PauseLong} ").Replace("\\P", $" {PauseShort} ");
            var TextLines = newText.Split('\n');
            foreach (var rawline in TextLines)
            {//then split by whitespace into words, and Chinese into characters
                var textline = ColourCode.Replace(rawline, m =>
                {
                    colour = m.Groups[1].Value[0] - '0';
                    return "";
                });
                var Words = Tokenise(textline, out var Seps);
                string LineToAdd = "";
                bool shown = false;//the start of LineToAdd is on screen already (it was shown before a pause)
                for (int WordPtr = 0; WordPtr < Words.Count; WordPtr++)
                {
                    var word = Words[WordPtr];
                    if (word == PauseLong || word == PauseShort)
                    {
                        if (LineToAdd.Trim() != "")
                        {
                            yield return EmitLine(LineToAdd, shown);
                            shown = true;
                        }
                        yield return ScrollWait(word == PauseLong ? 600 : 200);
                        continue;
                    }
                    if ((LineToAdd != "") && (Width(LineToAdd) + Width(word) > Columns))
                    {//no space for the next word: new line
                        yield return EmitLine(LineToAdd, shown);
                        shown = false;
                        LineToAdd = "";
                    }
                    LineToAdd += word + Seps[WordPtr];
                }
                if (LineToAdd.Trim() != "")
                {//Store remaining data
                    yield return EmitLine(LineToAdd, shown);
                }
            }
            yield return 0;

            //Shows a line, or completes the one shown before a pause, with a [MORE] first when the message has
            //shown as many lines as it may.
            IEnumerator EmitLine(string line, bool replace)
            {
                if (replace && LinePtr > 0)
                {
                    var last = System.Math.Min(LinePtr, Rows) - 1;
                    Lines[last].SetLine($"[color={ScrollColour(colour)}]{line}[/color]", option);
                    UpdateMessageDisplay();
                    unshown -= NonSpace(line) - lastLineCount;
                    lastLineCount = NonSpace(line);
                    yield break;
                }
                if (NoOfRowsNeeded >= limit + (unshown <= finalPiece ? 1 : 0))
                {//To force a [more] to appear when text is longer than the control
                    yield return AddLine(newText: "[MORE]", Option: -1, Colour: ConversationVM.UI_SAY, ForceMore: true);
                    NoOfRowsNeeded = 0;
                    limit = Rows - 1;
                }
                yield return AddLine(newText: line, Option: option, Colour: colour);
                NoOfRowsNeeded++;
                lastLineCount = NonSpace(line);
                unshown -= lastLineCount;
                if (line.Contains("[MORE]"))
                {//a \m: the lines after it start a new count
                    NoOfRowsNeeded = 1;
                    limit = Rows - 1;
                }
            }
        }

        static readonly System.Text.RegularExpressions.Regex Escape = new(@"\\[mpP0-6]");

        /// <summary>
        /// The characters of text the scroll shows, not counting spaces (a [MORE] and the escapes do not count).
        /// </summary>
        static int NonSpace(string text)
        {
            var n = 0;
            foreach (var c in Escape.Replace(text, "").Replace("[MORE]", ""))
            {
                if (!char.IsWhiteSpace(c))
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// Where the final piece of a message starts: scroll_print cuts it into chunks of at most 49 characters at
        /// spaces, then scroll_print1 at each backslash and scroll_print2 after each newline, and only the last piece
        /// is printed with flag 0.
        /// </summary>
        public static int FinalPieceStart(string text)
        {
            int start = 0;
            while (text.Length - start > 0x31)
            {
                var space = text.LastIndexOf(' ', start + 0x30, 0x31);
                start += space > start ? space - start : 0x31;
            }
            start = System.Math.Max(start, text.LastIndexOf('\\'));
            return System.Math.Max(start, text.LastIndexOf('\n') + 1);
        }

        /// <summary>
        /// scroll_wait: holds the scroll for ticks of the 1/256 s clock, or until a key or click (which the [MORE]
        /// handling in main takes, as for [MORE]).
        /// </summary>
        public static IEnumerator ScrollWait(int ticks)
        {
            WaitingForMore = true;
            var end = Time.GetTicksMsec() + (ulong)(ticks * 1000 / 256);
            while (WaitingForMore && Time.GetTicksMsec() < end)
            {
                yield return new WaitOneFrame();
            }
            WaitingForMore = false;
        }

        public IEnumerator RestoreLinesAfterWait(MessageScrollLine[] linesToRestore, float waittime)
        {
            yield return new WaitForSeconds(waittime);
            //restore
            for (int i = 0; i <= Lines.GetUpperBound(0); i++)
            {
                Lines[i] = new MessageScrollLine(linesToRestore[i].OptionNo, linesToRestore[i].LineText);
            }
            UpdateMessageDisplay();
            uimanager.MessageScrollIsTemporary = false;
            yield return 0;
        }

    }//end class
}//end namespace
