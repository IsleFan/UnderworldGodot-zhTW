namespace Underworld
{
    public partial class ConversationVM:UWClass
    {
        /// <summary>
        /// switch_pic(n) (UW2): shows another portrait and name for the talker: the named NPC whoami n below 0x100,
        /// otherwise the generic head and creature name of creature type n - 0x100.
        /// </summary>
        public static void switch_pic()
        {
            var which = at(at(stack + stackptr - 1));
            string newname;
            if (which < 0x100)
            {
                uimanager.instance.NPCPortrait.Texture = NPCPortrait(which, 0);
                newname = GameStrings.GetString(7, which + 16) == "" ? "" : GameStrings.GetDisplayString(7, which + 16);
            }
            else
            {//past 0x140 the original falls back to generic head 0, still naming creature type n - 0x100
                var creature = (which - 0x100) & 0x3F;
                uimanager.instance.NPCPortrait.Texture = NPCPortrait(0, which <= 0x140 ? creature : 0);
                newname = GameStrings.GetObjectNounUW(0x40 + creature);
            }
            if (newname != "")
            {
                uimanager.instance.NPCNameLabel.Text = $"[color={uimanager.CharNameColour}]{newname}[/color]";
            }
            result_register = 1;
        }
    }   //end class
}//end namespace
