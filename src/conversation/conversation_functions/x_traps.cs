using System.Diagnostics;

namespace Underworld
{
    public partial class ConversationVM:UWClass
    {
        public static void x_traps()
        {
            Debug.Print($"X_traps {GetConvoStackValueAtPtr(stack + stackptr-1)},{GetConvoStackValueAtPtr(stack + stackptr-2)}");

            var di_arg1 = GetConvoStackValueAtPtr(stack + stackptr-2);
            var si_arg0 = GetConvoStackValueAtPtr(stack + stackptr-1);

            if ((si_arg0 >= 0) && (si_arg0 <= 0x3FF))//the value is checked, 0 to 0x3FF (x_traps)
            {
                //sets variable di to value in si
                a_set_variable_trap.VariableOperationUW2(
                    LeftVariable: di_arg1, 
                    Operation: 2, 
                    RightVariable: si_arg0);
            }

            result_register = a_check_variable_trap.GetVarQuestOrClockValue(di_arg1);
        }
    }//end class
}//end namespace