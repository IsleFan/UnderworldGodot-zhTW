namespace Underworld
{
    public partial class ConversationVM:UWClass
    {
        public static void switch_pic()
        {
            var WhoAmI = at(at(stack + stackptr - 1));
            uimanager.instance.NPCPortrait.Texture = NPCPortrait(WhoAmI, 0);
            var newname =GameStrings.GetString(7, WhoAmI + 16);
            if (newname!="")
            {
                uimanager.instance.NPCNameLabel.Text = GameStrings.GetDisplayString(7, WhoAmI + 16);
            }            
        }
    }   //end class
}//end namespace