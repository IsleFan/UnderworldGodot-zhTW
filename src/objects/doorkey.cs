namespace Underworld
{
    public class doorkey : objectInstance
    {
        public static bool Use(uwObject obj, bool WorldObject)
        {
            if (!WorldObject)
            {
                //flag we are using the key
                useon.CurrentItemBeingUsed = new useon(obj, WorldObject);
                //print use message
                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 7));
            }
            return true;
        }

        /// <summary>
        /// A key used on a door or container (UseKeyOn): checkLock with the key's owner as the lock number, then message
        /// result + 2 ("The key does not fit.", "There is no lock on that.", "The key locks the lock.", "The key unlocks
        /// the lock.", "That is already open."). Unlocking does not open the door.
        /// </summary>
        public static bool UseOn(uwObject KeyObject, uwObject targetObject, bool WorldObject = true)
        {
            var result = a_lock.CheckLock(playerdat.playerObject, targetObject, KeyObject.owner, WorldObject);
            uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, result + 2));
            return true;
        }
    }// end class
}//end namespace