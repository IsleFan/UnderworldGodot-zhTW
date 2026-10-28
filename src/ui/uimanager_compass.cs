using Godot;

namespace Underworld
{
    public partial class uimanager : Node2D
    {
        [ExportGroup("CompassDisplay")]
        [Export]
        public TextureRect[] CompassBgUW1 = new TextureRect[4];
        [Export]
        public TextureRect[] CompassBgUW2 = new TextureRect[4];
        [Export] 
        TextureRect[] CompassPointsUW1 = new TextureRect[16];
        [Export] 
        TextureRect[] CompassPointsUW2 = new TextureRect[16];
        
        [Export]
        public Panel CompassPanelUW2 = new Panel();


        public static void InitCompass()
        {
            if (UWClass._RES != UWClass.GAME_UW2)
            {
                for (int i = 0; i<=instance.CompassBgUW1.GetUpperBound(0);i++)
                {
                    instance.CompassBgUW1[i].Texture = grCompass.LoadImageAt(i);
                }    
                for (int i = 0; i <= instance.CompassPointsUW1.GetUpperBound(0); i++)
                {
                    instance.CompassPointsUW1[i].Texture = grCompass.LoadImageAt(4+i);
                }            
            }
            else
            {
                for (int i = 0; i<=instance.CompassBgUW1.GetUpperBound(0);i++)
                {
                    instance.CompassBgUW2[i].Texture = grCompass.LoadImageAt(i);
                }   
                for (int i = 0; i <= instance.CompassPointsUW2.GetUpperBound(0); i++)
                {
                    instance.CompassPointsUW2[i].Texture = grCompass.LoadImageAt(4+i);
                }  
            }
        }

        public static void UpdateCompass()
        {    
            var heading = (playerdat.playerObject.heading<< 5) + playerdat.playerObject.npc_heading;//get full heading into a range of 0-f
            heading += 8;
            heading = (heading & 0xFF);
            PointCompassInDirection((heading>>4));
        }

        public static void PointCompassInDirection(int heading)
        {
            if (UWClass._RES != UWClass.GAME_UW2)
            {
                for (int i = 0; i <= instance.CompassBgUW1.GetUpperBound(0); i++)
                {
                    EnableDisable(instance.CompassBgUW1[i], (heading % 4 == i));
                }
                for (int i = 0; i <= instance.CompassPointsUW1.GetUpperBound(0); i++)
                {
                    EnableDisable(instance.CompassPointsUW1[i], (heading % 16 == i));
                }
            }
            else
            {
                for (int i = 0; i <= instance.CompassBgUW2.GetUpperBound(0); i++)
                {
                    EnableDisable(instance.CompassBgUW2[i], (heading % 4 == i));
                }
                for (int i = 0; i <= instance.CompassPointsUW2.GetUpperBound(0); i++)
                {
                    EnableDisable(instance.CompassPointsUW2[i], (heading % 16 == i));
                }
            }
        }


        private void _on_compass_click(InputEvent @event)
        {
            if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed
                && ((eventMouseButton.ButtonIndex == MouseButton.Left) || (eventMouseButton.ButtonIndex == MouseButton.Right)) && !InConversation)
            {//print_info: only in the game (not in conversation); the report goes below what is already in the scroll
                //the three step arrows over it come first (PLAYER.C's player_simple_move areas, in the original's 320x200 with
                //y up from the bottom): turn left, step, turn right
                var x = (int)(eventMouseButton.GlobalPosition.X / 4);
                var y = 200 - (int)(eventMouseButton.GlobalPosition.Y / 4);
                int? turn = null;
                if (x >= 0x6B && x <= 0x7B && y >= 0x21 && y <= 0x2F) { turn = -1; }
                else if (x >= 0x82 && x <= 0x92 && y >= 0x1F && y <= 0x2C) { turn = 0; }
                else if (x >= 0x9B && x <= 0xAA && y >= 0x21 && y <= 0x2F) { turn = 1; }
                if (turn.HasValue)
                {
                    main.PlayerSimpleMove(turn.Value);
                    return;
                }
                AddToMessageScroll("\n");
                if (UWClass._RES == UWClass.GAME_UW2)
                {
                    StatusMessageUW2();
                }
                else
                {
                    StatusMessageUW1();
                }
            }
        }

        private static void StatusMessageUW2()
        {
            var hunger = 117 + playerdat.play_hunger / 30;
            var output =
                GameStrings.GetDisplayString(1, GameStrings.str_you_are_currently_)
                +
                GameStrings.GetDisplayString(1, hunger)
                +
                GameStrings.GetDisplayString(1, GameStrings.str__and_);

            if (playerdat.DreamingInVoid)
            {
                output += GameStrings.GetDisplayString(1, 0x128);//sleeping
            }
            else
            {
                //general fatigue string
                var fatigue = playerdat.play_fatigue / 23;
                if (fatigue > 5) { fatigue = 5; }
                fatigue = 131 - fatigue;
                output += GameStrings.GetDisplayString(1, fatigue);
            }
            AddToMessageScroll(Localization.EndSentence(output));

            var worldsKnown = playerdat.GetQuest(131);
            var worldLine = worldsKnown != 0;
            if (worldsKnown == 0)
            {//other world names are unknown
             //do nothing
            }
            else
            {//other world names are known
                var world = worlds.GetWorldNo(playerdat.dungeon_level);
                worldsKnown = 1 + (worldsKnown << 1);
                var testval = 1 << world;
                int di;
                if ((testval & worldsKnown) != 0) //check if we know the name of this world.
                {
                    di = world + 2;
                }
                else
                {
                    di = 1;
                }

                output =
                    GameStrings.GetDisplayString(1, 0x49)
                    +
                    GameStrings.GetDisplayString(1, 0x49 + di);
                output = Localization.EndSentence(output);

                AddToMessageScroll(output);
            }

            var timeofday = (playerdat.ClockValue / 0x1c2000);
            timeofday = timeofday % 12;//twelve two-hour periods

            output =
                GameStrings.GetDisplayString(1, GameStrings.str_you_guess_that_it_is_currently_)
                +
                GameStrings.GetDisplayString(1, timeofday + 84);

            AddToMessageScroll(Localization.EndSentence(output));
            //the report always takes the same number of lines: a blank line, and another when there was no world line
            AddToMessageScroll("\n");
            if (!worldLine)
            {
                AddToMessageScroll("\n");
            }

        }

        private static void StatusMessageUW1()
        {
            var hunger = 104 + playerdat.play_hunger / 30;
            var fatigue = playerdat.play_fatigue / 23;
            if (fatigue > 5) { fatigue = 5; }
            fatigue = 118 - fatigue;

            //hunger and fatigue line
            var output =
                GameStrings.GetDisplayString(1, GameStrings.str_you_are_currently_)
                +
                GameStrings.GetDisplayString(1, hunger)
                +
                GameStrings.GetDisplayString(1, GameStrings.str__and_)
                +
                GameStrings.GetDisplayString(1, fatigue);
            output = Localization.EndSentence(output);

            AddToMessageScroll(output);

            var level = playerdat.dungeon_level + 410;
            output =
                GameStrings.GetDisplayString(1, GameStrings.str_you_are_on_the_)
                +
                GameStrings.GetDisplayString(1, level)
                +
                GameStrings.GetDisplayString(1, GameStrings.str__level_of_the_abyss_);

            AddToMessageScroll(output);

            var timeofday = (playerdat.ClockValue / 0x1c2000);
            var noOfDays = timeofday / 12;
            timeofday = timeofday % 12;

            if (noOfDays > 100)
            {
                output = GameStrings.GetDisplayString(1, GameStrings.str_it_has_been_an_uncountable_number_of_days_since_you_entered_the_abyss_);
            }
            else
            {
                output =
                    GameStrings.GetDisplayString(1, GameStrings.str_it_is_the_)
                    +
                    GameStrings.GetDisplayString(1, noOfDays + 411)
                    +
                    GameStrings.GetDisplayString(1, GameStrings.str__day_of_your_imprisonment_);

            }
            AddToMessageScroll(output);

            output =
                GameStrings.GetDisplayString(1, GameStrings.str_you_guess_that_it_is_currently_)
                +
                GameStrings.GetDisplayString(1, timeofday + 71);

            AddToMessageScroll(Localization.EndSentence(output));
        }
    }//end class
}//end namespace