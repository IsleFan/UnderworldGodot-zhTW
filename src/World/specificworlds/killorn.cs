namespace Underworld
{
    /// <summary>
    /// Specific code for the killorn
    /// </summary>
    public class killorn:UWClass
    {

        public static void KilornIsCrashing(bool isEnteringLevel)
        {
            special_effects.SpecialEffect(effecttype: 4, effectparam: 0);

            playerdat.SetQuest(54,1);
            playerdat.IncrementXClock(15);
            if (!isEnteringLevel)
            {//the schedules catch up, unless the player is changing level (Killorn_just_crashed)
                scd.ProcessSCDArk(1);
            }
        }
    }
}