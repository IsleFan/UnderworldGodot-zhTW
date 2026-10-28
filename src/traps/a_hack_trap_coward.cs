namespace Underworld
{
    /// <summary>
    /// Trap that handles the avatar or a dueling npc fleeing from an arena in the Pits of Carnage.
    /// </summary>
    public class a_hack_trap_coward : trap
    {
        public static bool Activate(int character)
        {//hack 30: only while the avatar is fighting in the pits; the chain goes on either way
            if (playerdat.IsFightingInPit)
            {
                if (character == playerdat.playerObject.index)
                {//Avatar has fled.
                    pitsofcarnage.AvatarIsACoward();
                }
                else
                {
                    pitsofcarnage.DefeatLivingPitFighter(character);
                }
            }
            return true;
        }
    }//end class
}//end namespace
