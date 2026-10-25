using Godot;

namespace Underworld
{
	/// <summary>
	/// The mouse cursor in UW
	/// </summary>
	public partial class mouseCursor : TextureRect
	{
		[Export] public TextureRect cross;
		// private static Vector2 UW1CursorPosition = new Vector2(552, 296);
		// private static Vector2 UW2CursorPosition = new Vector2(474, 314);
		// private static Vector2 UW1SubCursorPosition = new Vector2(366, 238);
		// private static Vector2 UW2SubCursorPosition = new Vector2(420, 250);

		/// <summary>
		/// Display position of the cursor when in mouselook
		/// </summary>
		// public static Vector2 CursorPosition
		// {
		// 	get
		// 	{
		// 		if (UWClass._RES == UWClass.GAME_UW2)
		// 		{
		// 			return UW2CursorPosition;
		// 		}
		// 		else
		// 		{
		// 			return UW1CursorPosition;
		// 		}
		// 	}
		// }


		/// <summary>
		/// Position of the mouse cursor for raycasts when in mouselook
		/// </summary>
		// public static Vector2 CursorPositionSub
		// {
		// 	get
		// 	{
		// 		if (UWClass._RES == UWClass.GAME_UW2)
		// 		{
		// 			return UW2SubCursorPosition;
		// 		}
		// 		else
		// 		{
		// 			return UW1SubCursorPosition;
		// 		}
		// 	}
		// }

		// Called every frame. 'delta' is the elapsed time since the previous frame.
		public override void _Process(double delta)
		{
			if (main.cameraPitchGimbal_world != null)
			{
				// var mouselook = (bool)main.cameraPitchGimbal.Get("MOUSELOOK");

				// if (mouselook)
				// {
				// 	this.Position = CursorPosition;
				// }
				// else
				// {//follow the mouse
				if (Texture != null)
				{
					if (uimanager.CurrentGameMode == uimanager.GameModes.AUTOMAP)
					{
						if (uimanager.CurrentAutomapAction == uimanager.automapactions.WRITING)
						{
							return; //don't move mouse while writing.
						}
					}
					cross.Position = GetViewport().GetMousePosition() ;
					var offset = new Vector2(-Texture.GetWidth() , -Texture.GetHeight() );
					var pos = GetViewport().GetMousePosition() + offset;
					this.Position = pos;
				}
			}
			//}
		}

		/// <summary>Whether leaving the window showed the system cursor, to hide again on the way back.</summary>
		bool shownOutside;

		/// <summary>
		/// The game hides the system cursor and draws its own, but on macOS a hidden cursor stays
		/// hidden outside the window too, so in windowed mode the mouse seemed unable to leave.
		/// Outside, the system cursor shows and this one, stuck at the edge, does not.
		/// </summary>
		public override void _Notification(int what)
		{
			if (what == NotificationWMMouseExit && Input.MouseMode == Input.MouseModeEnum.Hidden)
			{
				Input.MouseMode = Input.MouseModeEnum.Visible;
				shownOutside = true;
				Visible = false;
				cross.Visible = false;
			}
			else if (what == NotificationWMMouseEnter && shownOutside)
			{
				Input.MouseMode = Input.MouseModeEnum.Hidden;
				shownOutside = false;
				Visible = true;
				cross.Visible = true;
			}
		}

		public void InitCursor()
		{
			Texture = uimanager.grCursors.LoadImageAt(0);
			Input.MouseMode = Input.MouseModeEnum.Hidden;
			this.Size = Texture.GetSize() * 4;
		}

		public void SetCursorToObject(int index)
		{
			Texture = uimanager.grObjects.LoadImageAt(index);
			Material = uimanager.grObjects.GetMaterial(index);
			this.Size = Texture.GetSize() * 4;
		}

		public void SetCursorToCursor(int index = 0)
		{
			if (((index == 14) || (index == 12) || (index==13)) && (UWClass._RES == UWClass.GAME_UW2))
			{
				//Special case for the quill and other map cursors
				uimanager.grCursors.PaletteNo = 1;
			}
			else
			{
				uimanager.grCursors.PaletteNo = 0;
			}
			Texture = uimanager.grCursors.LoadImageAt(index);
			//Material= uimanager.grCursors.GetMaterial(index);//no shader is applied here
			Material = null;
			this.Size = Texture.GetSize() * 4;
		}
	}

}//end namespace