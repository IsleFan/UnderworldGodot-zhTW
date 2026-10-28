using System.Diagnostics;
using Godot;
namespace Underworld
{
    public partial class ConversationVM : UWClass
    {
        public static uwObject currentTalker;

        static Vector2 OriginalUW2ScrlEdgeLeft;
        static Vector2 OriginalUW2ScrlEdgeRight;

        static bool SpecialCaseDyingNPC = false;
        public static void StartConversation(uwObject talker, bool dyingNPC = false)
        {
            currentTalker=talker;
            SpecialCaseDyingNPC = dyingNPC;
            //talker.npc_whoami = 46; jerry the rat
            //Try and load the conversation from the ark files.
            if (!cnvArkLoader.Loaded)
            {
                switch (_RES)
                {
                    case GAME_UW2:
                        cnvArkLoader.LoadCnvArkUW2(System.IO.Path.Combine(BasePath, "DATA", "CNV.ARK")); break;
                    default:
                        cnvArkLoader.LoadCnvArkUW1(System.IO.Path.Combine(BasePath, "DATA", "CNV.ARK")); break;
                }
            }

            if (conversations != null)
            {                
                int conversationNo = GetConversationNumber(talker);
                Debug.Print($"ConversationNo is {conversationNo}");
                currentConversation = null;
                if(conversations[conversationNo] == null)
                {
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(7, 1));
                    return;
                }
                //Check if npc can be talked to
                if ((conversations[conversationNo].CodeSize == 0) || (talker.npc_whoami == 255))
                {
                    //006~007~001~You get no response.
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(7, 1));
                    return;
                }
                else
                { 
                    //a conversation can be had
                    currentConversation = conversations[conversationNo];
                    uimanager.CurrentGameMode = uimanager.GameModes.CONVERSATION;
                    DoTeleport = false;
                    TeleportToLevel = -1;
                    TeleportTileX = - 1;
                    TeleportTileY = -1;
                    TeleportTalkerX = -1;

                    SetupConversationUI(talker);
                    if (_RES == GAME_UW2)
                    {//converse: the effects stop as the conversation screen comes up
                        UWsoundeffects.KillAllEffects();
                    }

                    if (_RES != GAME_UW2)
                        {
                            XMIMusic.ChangeThemeMusic(XMIMusic.MapsAndLegends);
                        }
                    else
                    {
                        if (playerdat.play_drawn == 1)
                        {
                            XMIMusic.PickLevelThemeMusic(0);
                        }
                    }

                    InitialiseConversationMemory();
                    stack = currentConversation.NoOfMemorySlots;

                    ImportVariables(talker);
                    if (!TemporaryTalker)
                    {//Converse: an NPC without an inventory gets one before the script runs (not the stand-in for a door or wisp, which is deleted afterwards)
                        npc.spawnloot(talker);
                    }

                    
                    //Launch conversation VM co-routine 
                    _ = Peaky.Coroutines.Coroutine.Run(
                        RunConversationVM(talker),
                        main.instance);
                }
            }
        }

        private static void SetupConversationUI(uwObject talker)
        {

            if (_RES==GAME_UW2)
            {//set up the unique ui for uw2 conversations
                uimanager.instance.mainwindowUW2.Texture = uimanager.bitmaps.LoadImageAt(BytLoader.CONV_BYT, false);
                for (int i=0; i<=uimanager.instance.SelectedRunes.GetUpperBound(0);i++)
                {
                uimanager.EnableDisable(uimanager.instance.SelectedRunes[i],false);
                }
                for (int i=0; i<=uimanager.instance.InteractionButtonsUW2.GetUpperBound(0);i++)
                {//disable interaction buttons
                    uimanager.EnableDisable(uimanager.instance.InteractionButtonsUW2[i],false);
                }
                for (int i = 0; i<3; i++)
                {//hide active spells
                    uimanager.EnableDisable(uimanager.instance.ActiveSpellIcons[i], false);
                }

                uimanager.instance.scroll.Columns = 100;
                uimanager.instance.messageScrollUW2.SetSize(new Godot.Vector2(1160,140));
                uimanager.EnableDisable(uimanager.instance.CompassPanelUW2,false);
                uimanager.EnableDisable(uimanager.instance.PowerGemUW2,false);

                OriginalUW2ScrlEdgeLeft = uimanager.instance.scrollEdgeLeft.Position;
                OriginalUW2ScrlEdgeRight = uimanager.instance.scrollEdgeRight.Position;
                //move positions
                uimanager.instance.scrollEdgeLeft.Position = new Vector2(20,676);
                uimanager.instance.scrollEdgeRight.Position = new Vector2(1248,676);
            }
            else
            {
                //UW1 specific.
            }

            Likes = 0;
            Dislikes = 0;
            
            for (int i=0;i<uimanager.NoOfTradeSlots;i++)
            {
               uimanager.SetPlayerTradeSlot(i,-1,false);
               uimanager.SetNPCTradeSlot(i,-1,false);
            }
            
            //Clear existing text
            uimanager.instance.scroll.Clear();
            uimanager.instance.convo.Clear();
            //uimanager.instance.ConversationText.Text = "";
            
            var head = new GRLoader(GRLoader.HEADS_GR, GRLoader.GRShaderMode.UIShader);
            //set up relevant UI
            uimanager.EnableDisable(uimanager.instance.ConversationPanelUW1, _RES!=GAME_UW2);
            uimanager.EnableDisable(uimanager.instance.ConversationPanelUW2, _RES == GAME_UW2);

            //Player name and portrait
            if (playerdat.isFemale)
            {
                uimanager.instance.PlayerPortrait.Texture = head.LoadImageAt(playerdat.Body + 5);
            }
            else
            {
                uimanager.instance.PlayerPortrait.Texture = head.LoadImageAt(playerdat.Body);
            }
            if (_RES == GAME_UW2)
            {//charname is right aligned in UW2
                uimanager.instance.PlayerNameLabel.Text = $"[right][color={uimanager.CharNameColour}]{playerdat.CharName}[/color][/right]";
            }
            else
            {
                uimanager.instance.PlayerNameLabel.Text = $"[color={uimanager.CharNameColour}]{playerdat.CharName}[/color]";
            }
            
            if(uimanager.PanelMode!=0)
            {
                uimanager.SetPanelMode(0);//make sure inventory paperdoll is displayed
            }
          

            //npc name and portrait
            var talkername = talker.a_name;
            if (talker.majorclass == 1 && talker.npc_whoami != 0)
            {//a translated name only replaces the one a_name took from the names block
                talkername = Localization.Lookup(7, talker.npc_whoami + 16, talkername);
                if ((_RES != GAME_UW2) && (talker.npc_whoami < 0xF0) && (GameStrings.GetString(7, talker.npc_whoami + 16) == ""))
                {//UW1's get_name gives nothing for a whoami with no name, and no name is shown (UW2 falls back on the kind)
                    talkername = "";
                }
            }
            uimanager.instance.NPCNameLabel.Text = $"[color={uimanager.CharNameColour}]{talkername}[/color]";
            uimanager.instance.NPCPortrait.Texture = NPCPortrait(talker.npc_whoami, talker.item_id);

            //Init conversation trade globals
            //barter_init: seeded with the NPC's object index, so the same NPC always gets the same temper
            Rng.Seed = (uint)talker.index;
            BablTradeBonus = 0;
            TradeThreshold = Rng.RandomOffset(critterObjectDat.TradeThreshold(talker.item_id) * 6, -25, +25);//greed, from haggle
            TradePatience = Rng.RandomOffset(critterObjectDat.TradePatience(talker.item_id), -20, +100); 
            NPCAppraisalAccuracy = Rng.RandomOffset((15 - critterObjectDat.TradeAppraisal(talker.item_id)) * 6, -25, +50);//from shrewd
            Rng.RandomOffset(critterObjectDat.level(talker.item_id), -20, 20);//npc_wit: worked out, never used, but it draws
            //charisma: the NPC wants less and puts up with more
            TradeThreshold -= playerdat.Charm * 2;
            TradePatience += playerdat.Charm >> 1;
            Rng.InitRng();//reseeded from the clock
            PreviousEvaluation = 0;
            TradeResult = 0;

        }

        private static int GetConversationNumber(uwObject talker)
        {
            int conversationNo;
            if (talker.npc_whoami == 0)
            {
                conversationNo = 256 + talker.classindex;
            }
            else
            {
                conversationNo = talker.npc_whoami;
                if (_RES == GAME_UW2)
                {
                    conversationNo++;
                }
            }
            return conversationNo;
        }

        /// <summary>
        /// Gets the appropiate portrait for an NPC
        /// </summary>
        /// <param name="whoami"></param>
        /// <returns></returns>
        private static Texture2D NPCPortrait(int whoami, int item_id)
        {
            //Assume generic head first.

            bool UseGenericHead = true;
            switch (_RES)
            {
                case GAME_UW2:
                    if (whoami != 0)
                    {
                        UseGenericHead = false;
                    }
                    break;
                default:
                    {
                        if ((whoami > 0) && (whoami <= 28))
                        {
                            UseGenericHead = false;
                        }
                        break;
                    }
            }
            if (UseGenericHead)
            {
                var ghead = new GRLoader(GRLoader.GENHEAD_GR, GRLoader.GRShaderMode.UIShader);
                return ghead.LoadImageAt(item_id & 0x3F);
            }
            else
            {
                var chead = new GRLoader(GRLoader.CHARHEAD_GR, GRLoader.GRShaderMode.UIShader);
                if (_RES == GAME_UW2)
                {//some special portrait cases due to weirdness with charhead.gr
                    switch (whoami)
                    {
                        case 0x11://Janar (for some reason his portrait loads as molloy)
                            return chead.LoadImageAt(2);
                    }
                }
                return chead.LoadImageAt(whoami - 1);
            }
        }

    }//end class
}//end namespace