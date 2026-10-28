using Godot;

namespace Underworld
{
    /// <summary>
    /// For managing the "eyes" ui element for showing enemy NPC health.
    /// </summary>
    public partial class uimanager : Node2D
    {
        [ExportGroup("Eyes")]
        [Export] public TextureRect Eyes;


        /// <summary>The level shown, 1 to 3 (3 the worst wounded), 0 for the closed eyes.</summary>
        public static int EyeLevel = 0;
        /// <summary>adjust_eyes' place in eyes_seq, -1 while the eyes are closed.</summary>
        static int EyeFrame = -1;
        /// <summary>How long the eyes have stayed open at the middle of the sequence.</summary>
        static int EyeWait = 0;
        static double EyeTimer;
        /// <summary>eyes_seq: open, opener, fully open (held), opener, open; each level has three pictures after the closed one.</summary>
        static readonly int[] EyeSequence = { 1, 2, 3, 2, 1 };

        private void InitEyes()
        {
            grEyes = new GRLoader(GRLoader.EYES_GR, GRLoader.GRShaderMode.UIShader);
            if (UWClass._RES == UWClass.GAME_UW2)
            {
                Eyes.Position = new Vector2(440, 16);
            }
            else
            {
                Eyes.Position = new Vector2(512, 16);
            }
        }

        /// <summary>
        /// A blow by the player on a critter (COMBAT.C, set_screen_frame(SCR_EYES, 3 - hp * 3 / avghit), at most 2 off):
        /// the gargoyle's eyes open on the critter's health. A blow while they are open opens them fully again.
        /// </summary>
        public static void SetEyeLevel(int _hp, int _maxhp)
        {
            var fx = _maxhp != 0 ? _hp * 3 / _maxhp : 0;
            if (fx >= 3)
            {
                fx = 2;
            }
            EyeLevel = 3 - fx;
            if (EyeFrame < 0)
            {
                EyeFrame = 0;
            }
            else
            {//adjust_eyes: back to the fully open picture
                EyeFrame = 2;
            }
            EyeWait = 0;
        }

        private void _ProcessEyeAnims(double delta)
        {
            if (!InGame || EyeFrame < 0)
            {
                return;
            }
            EyeTimer += delta;
            if (EyeTimer < 0.1f)
            {
                return;
            }
            EyeTimer = 0;
            if (EyeFrame == 3 && EyeWait < 0x10)
            {//held fully open
                EyeWait++;
                return;
            }
            if (EyeFrame > 4)
            {//done: closed again
                EyeFrame = -1;
                EyeWait = 0;
                EyeLevel = 0;
                instance.Eyes.Texture = grEyes.LoadImageAt(0);
                return;
            }
            var picture = EyeSequence[EyeFrame++] + (EyeLevel - 1) * 3;
            instance.Eyes.Texture = grEyes.LoadImageAt(System.Math.Min(picture, grEyes.ImageCache.GetUpperBound(0)));
        }

    }//end class
}//end namespace