using Underworld;

namespace Underworld.Save.Tests
{
    /// <summary>
    /// Where a message's final piece starts (SCROLL.C scroll_print, scroll_print1, scroll_print2): only a line that
    /// starts in it may take the extra line before a [MORE], so the cut must follow the original's.
    /// </summary>
    public class MessageScrollPieceTests
    {
        [Fact]
        public void ShortMessage_IsAllOnePiece()
        {
            Assert.Equal(0, MessageDisplay.FinalPieceStart("You see a sword."));
        }

        [Fact]
        public void Newline_StartsTheFinalPiece()
        {//scroll_print2 keeps each newline at the end of its piece
            Assert.Equal(6, MessageDisplay.FinalPieceStart("First\nSecond"));
        }

        [Fact]
        public void Escape_StartsTheFinalPiece()
        {//scroll_print1 cuts before each backslash, so the escape begins the last piece
            Assert.Equal(5, MessageDisplay.FinalPieceStart("Text \\2coloured"));
        }

        [Fact]
        public void LongMessage_FinalChunkStartsAtTheLastSpaceOfEach49Characters()
        {//the first 49 characters end in "ccc"; the cut is at the space before it, which starts the next chunk
            var text = new string('a', 30) + " " + new string('b', 14) + " ccccc dddd";
            Assert.Equal(45, MessageDisplay.FinalPieceStart(text));
        }

        [Fact]
        public void LongWordWithoutSpaces_IsCutEvery49Characters()
        {
            Assert.Equal(98, MessageDisplay.FinalPieceStart(new string('x', 100)));
        }
    }
}
