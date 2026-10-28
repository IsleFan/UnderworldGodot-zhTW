namespace Underworld
{
    public class pocketwatch:objectInstance
    {
        public static bool Use(uwObject obj, bool WorldObject)
        {
            if (!WorldObject)
            {//UseWatch: a 12-hour dial, so noon and midnight read 12
                var hours = playerdat.TwelveHourClock == 0 ? 12 : playerdat.TwelveHourClock;
                uimanager.AddToMessageScroll(ZhText.Enabled
                    ? ZhText.Compose(GameStrings.GetDisplayString(1, 39), $" {hours}:{playerdat.Minute:d2}。")
                    : GameStrings.GetString(1, 39) + hours + ":" + playerdat.Minute.ToString("d2") + GameStrings.GetString(1, 0x60));
            }            
            return true;
        }
    }//end class
}//end namespace