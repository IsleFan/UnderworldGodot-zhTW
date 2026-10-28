using System.Diagnostics;

namespace Underworld
{
    public partial class scd:UWClass
    {
        /// <summary>
        /// Event 4: sets quest flag row[5] (0-127, four to a long) to row[6] (ev_set_qbit). The original
        /// adds the value at the flag's bit unmasked; SCD.ARK only ever sets 1 (block 1, quest 56).
        /// </summary>
        public static int ChangeQuest(byte[] currentblock, int eventOffset)
        {
            Debug.Print ("Change Quest in SCD");
            PrintRow(
                currentblock: currentblock,
                eventOffset: eventOffset);
            playerdat.SetQuest(currentblock[eventOffset + 5], currentblock[eventOffset + 6]);
            return 0;
        }
    }//end class
}//end namespace
