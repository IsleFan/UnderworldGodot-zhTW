using System.Collections.Generic;

namespace Underworld
{
    public class CutSceneCommand : UWClass
    {
        public int offset;
        public int frame;
        public int functionNo;
        public List<int> functionParams;
        public int NoOfParams
        {
            get
            {
                return functionParams.Count;
            }
        }

        // Opcode names and meanings from the original's cutscene player (CUTS.C cuts_dispatch).
        // UW1 has opcodes 0 to 15 only.
        static string[] FunctionNames = {
            "txt",              // 0: subtitle
            "erase",            // 1: clears the subtitle
            "func",             // 2: does nothing
            "pause",            // 3: holds the frame
            "skip",             // 4: input skips ahead
            "next",             // 5: ends this animation file
            "end",              // 6: ends the cutscene after this file
            "loop",             // 7: replays the file up to this frame
            "data",             // 8: names the next animation file
            "fadeout",          // 9
            "fadein",           // 10
            "jump",             // 11: skips ahead without drawing
            "punt",             // 12: whether Escape ends the cutscene
            "say",              // 13: speech with its subtitle
            "wait",             // 14: holds the frame, longer with speech
            "clang",            // 15: UW1 sound effect
            "palrange",         // 16: palette entries from the script
            "palfade",          // 17: palette entries faded towards the script's
            "palset",           // 18: does nothing
            "palsimplefade",    // 19: whole palette faded towards PALS.DAT
            "vscreen",          // 20: virtual screen for panoramas
            "focus",            // 21: point of the virtual screen shown
            "lback",            // 22: LBACK background into the virtual screen
            "pan",              // 23: pans the focus
            "splity",           // 24: row where the subtitle bar begins
            "music",            // 25
            "test",             // 26: does nothing
            "wait_for_sound"    // 27: waits for the speech, then cuts it off
            };

        static string[] FunctionDescriptions =
        {
            "subtitle string arg[1] in colour arg[0]; FFFFh clears it (UW2)",
            "clears the subtitle",
            "does nothing, arguments are ignored",
            "holds this frame for arg[0] seconds or until input; 999 until input",
            "input from here until frame arg[0] skips there; without speech also pauses arg[1] before it",
            "ends this animation file and goes on to the next",
            "ends the cutscene after this file",
            "replays the file arg[0] more times up to this frame",
            "the next file is csXXX.nYY, XXX from arg[0] and YY from arg[1] in octal; UW2 file 996 is one of 034-037 at random",
            "fades out over arg[0], full screen only",
            "fades in over arg[0], full screen only",
            "skips ahead to frame arg[0] without drawing, unless it is the next frame",
            "arg[0] says whether Escape may end the cutscene",
            "speech BSP<arg[2]>.VOC with subtitle arg[1] in colour arg[0]; 999 text only, 998 no speech (UW2)",
            "holds the frame for arg[0] seconds, or with speech until it ends and then arg[1] seconds, ignoring input meanwhile",
            "plays sound effect 11h (UW1); nothing in UW2",
            "sets arg[1] palette entries from arg[0] to the colours that follow",
            "fades arg[3] palette entries from arg[2] towards the colours that follow, timed by arg[0] and arg[1]",
            "does nothing",
            "fades the palette towards PALS.DAT[arg[0]], timed by arg[1] and arg[2]",
            "virtual screen arg[0] by arg[1], subtitle bar from row arg[2] (999 none)",
            "shows the virtual screen at arg[0], arg[1]",
            "draws CUTS\\lback<arg[2]>.byt into the virtual screen at arg[0], arg[1]",
            "pans in direction arg[0] (0 down, 1 right, 2 up, 3 left) by arg[1] a frame for arg[2] frames",
            "the subtitle bar starts at row arg[0] (999 none)",
            "plays music theme arg[0], 0 stops it",
            "does nothing",
            "waits up to arg[0] seconds for the speech, then cuts it off"
        };
        public string FunctionName
        {
            get
            {
                if (functionNo > FunctionNames.GetUpperBound(0)) { return "OUTOFBOUNDS No " + functionNo + " "; }
                return FunctionNames[functionNo];
            }
        }
        public string FunctionDesc
        {
            get
            {
                if (functionNo > FunctionDescriptions.GetUpperBound(0)) { return "OUTOFBOUNDS"; }
                return FunctionDescriptions[functionNo];
            }
        }

        /// <summary>
        /// Returns the number of argument words of the command whose arguments start at byte offset argOffset of the
        /// script. palrange and palfade carry their colours in the script, so their length comes from their own
        /// arguments (CUTS.C: each cutsop_ returns the words it used).
        /// </summary>
        public static int GetArgumentCount(int cmd, byte[] data, int argOffset)
        {
            switch (cmd)
            {
                case 16: return Word(data, argOffset + 2) * 3 + 2;//palrange(first, count, colours)
                case 17: return Word(data, argOffset + 6) * 3 + 4;//palfade(period, total, first, count, colours)
            }
            return GetArgumentCount(cmd);
        }

        static int Word(byte[] data, int offset)
        {
            return offset + 2 <= data.Length ? (int)Loader.getAt(data, offset, 16) : 0;
        }

        static int GetArgumentCount(int cmd)
        {
            switch (cmd)
            {
                case 0: return 2;//displays text arg[1] with color arg[0]
                case 1: return 0;//sets some flag to 0
                case 2: return 2;//no-op, arguments are ignored
                case 3: return 1;//pause(seconds)
                case 4: return 2;//skip(frame, seconds)
                case 5: //next (UW1 skips a word, UW2 none)
                    if (_RES == GAME_UW2)
                    {
                        return 0;
                    }
                    else
                    {
                        return 1;
                    }
                case 6: return 0;//ends cutscene
                case 7: return 1;//repeat segment arg[0] times
                case 8: return 2;//opens a new file csXXX.nYY, with XXX from arg[0] and YY from arg[1]; XXX and YY are formatted octal values
                case 9: return 1;//fades out at rate arg[0] (higher is faster)
                case 10: return 1;//fades in at rate arg[0] (higher is faster)
                case 11: return 1;//jump(frame)
                case 12: return 1;//punt(flag)
                case 13: return 3;//displays text arg[1] with color arg[0] and plays audio arg[2]
                case 14: return 2;//wait arg[0] or arg[1] seconds
                case 15: return 0;//plays 'klang' sound (UW1 only)
                case 18: return 4;//does nothing, not used in uw2
                case 19: return 3;//unknown
                case 20: return 3;//unknown
                case 21: return 2;//unknown
                case 22: return 3;//unknown
                case 23: return 3;//unknown
                case 24: return 1;//unknown, usually at the start of cutscene
                case 25: return 1;//music
                case 26: return 1;//does nothing, not used in uw2
                case 27: return 1;//unknown
            }
            return 0;
        }
    }//end namespace
}// end class