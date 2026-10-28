namespace Underworld
{
    public class smallblackrockgem : objectInstance
    {

        public static bool Use(uwObject obj, bool WorldObject)
        {
            if (!WorldObject)
            {
                //flag we are using the key
                useon.CurrentItemBeingUsed = new useon(obj, WorldObject);
                //print use message
                uimanager.AddToMessageScroll(useon.CurrentItemBeingUsed.GeneralUseOnString);
            }
            return true;
        }

        public static bool UseOn(uwObject smallGem, uwObject itemUsedOn, bool WorldObject)
        {
            //assume small gem in inventory, largegem in world
            if ((itemUsedOn.item_id == 0x168) && WorldObject)
            {//used on large gem in the world
                if (smallGem.owner != 0)
                {//small gem has been treated (any owner)
                    var gemNo = smallGem.classindex - 7;
                    if (gemNo == 4)
                    {
                        playerdat.IncrementXClock(1);
                    }
                    special_effects.SpecialEffect(effecttype: 4, effectparam: playerdat.GetXClock(2) << 2);
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x152));
                    playerdat.IncrementXClock(2);
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x152 + playerdat.GetXClock(2)));
                    if ((gemNo & 0x6) == 6)
                    { 
                        //swap gems 6 and 7 around
                        gemNo = 0xD - gemNo;
                    }

                    var quest130 = playerdat.GetQuest(130);
                    quest130 = quest130 | (1 << (gemNo - 1));
                    playerdat.SetQuest(130, quest130);
                    playerdat.SetGameVariable(6, gemNo - 1);//vars[6] records the gem (the large gem's face colour reads it)

                    ObjectCreator.Consume(smallGem, true);
                    UWsoundeffects.PlaySoundEffectAtAvatar(0x12, 0x40, 0x28);
                    quest130 = playerdat.GetQuest(130);
                    if (quest130 == 0xFF)
                    {
                        //all gems used
                        //Debug.Print("Play Sound Effect 0x14h at 0x40,0x2c");
                        UWsoundeffects.PlaySoundEffectAtAvatar(UWsoundeffects.SoundEffectSpellRing2, 0x40, 0x14);
                    }
                    else
                    {
                        UWsoundeffects.PlaySoundEffectAtAvatar(UWsoundeffects.SoundEffectSpellRing1, 0x40, 0x14);
                    }
                    return true;
                }
            }
            uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 0x15B));//The key gem remains inert in your hand
            return true;
        }
    }//end calss
}//end namespace