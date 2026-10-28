namespace Underworld
{
    public class lockpick : objectInstance
    {

        public static bool Use(uwObject obj, bool WorldObject)
        {
            if (!WorldObject)
            {
                useon.CurrentItemBeingUsed = new useon(obj, WorldObject);
                //print use message
                //8	What lock would you like to try to pick?
                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 8));
            }
            return true;
        }

        /// <summary>
        /// Picking a lock (UseLockpickOn): checkLock with minus the Picklock skill (UW2 plus one). In UW2 a fumble breaks
        /// the pick unless a dexterity check against 20 succeeds, and with no Picklock skill an unlocked lock reports a
        /// failed attempt. UW1 picks never break. Picking only unlocks; it does not open the door.
        /// </summary>
        public static bool UseOn(uwObject LockpickObject, uwObject targetObject, bool WorldObject = true)
        {
            var skill = (_RES == GAME_UW2) ? -(playerdat.PickLock + 1) : -playerdat.PickLock;
            var result = a_lock.CheckLock(playerdat.playerObject, targetObject, skill, WorldObject);
            if (_RES == GAME_UW2)
            {
                if (((result == 5) && (playerdat.SkillCheck(playerdat.DEX, 20) > 0)) || ((result == 4) && (skill == -1)))
                {
                    result = 0;
                }
            }
            switch (result)
            {
                case 1:
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 3));//There is no lock on that.
                    break;
                case 5:
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x86));//You broke your pick.
                    ObjectCreator.Consume(LockpickObject, true);
                    break;
                case 0:
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, _RES == GAME_UW2 ? 0x85 : 0x78));//Your lockpicking attempt failed.
                    break;
                case 4:
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, _RES == GAME_UW2 ? 0x88 : 0x7A));//That is not locked.
                    break;
                default:
                    UWsoundeffects.PlaySoundEffectAtAvatar(UWsoundeffects.SoundEffectLockPick, 0x40, 0);
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, _RES == GAME_UW2 ? 0x87 : 0x79));//You succeed in picking the lock.
                    break;
            }
            return true;
        }
    }//end class
}//end namespace