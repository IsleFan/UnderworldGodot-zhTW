using System.Diagnostics;

namespace Underworld
{
    public partial class ConversationVM:UWClass
    {
        public static void teleport_talker(uwObject talker)
        {
            var Y = GetConvoStackValueAtPtr(stack + stackptr-1);
            var X = GetConvoStackValueAtPtr(stack + stackptr-2);
            Debug.Print($"Teleport Talker to {X},{Y}");          
            TeleportTalkerX = X; TeleportTalkerY = Y;//moved when the conversation ends
            result_register = 1;
        }
    }//end class
}//end namespace