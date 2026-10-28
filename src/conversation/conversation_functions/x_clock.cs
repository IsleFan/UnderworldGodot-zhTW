using System.Diagnostics;

namespace Underworld
{
    public partial class ConversationVM:UWClass
    {
        public static void x_clock()
        {
            var xvalue = at(at(stack + stackptr-1));
            var xnumber = at(at(stack + stackptr-2));            
           
            if (xvalue>0x100)
            {                
                result_register = playerdat.GetXClock(xnumber);
                Debug.Print($"getting xclock {xnumber} {result_register}");
            }
            else
            {
                if (xnumber==0)
                {//the time of day: the game clock moves 20 minutes (0x4B000) for each step it changes by
                    playerdat.ClockValue += (xvalue - playerdat.GetXClock(0)) * 0x4B000;
                }
                Debug.Print($"setting xclock {xnumber} to {xvalue}");
                playerdat.SetXClock(xnumber,xvalue);
                result_register = 0;              
            }
        }
    } //end class
}//end namespace