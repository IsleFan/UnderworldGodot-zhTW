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
					Controls.UpdateMouseCapture();
					//under mouse look the cursor stays in the middle of the 3D view, where clicks aim
					var mouse = Controls.MouseLookActive
						? uimanager.instance.uwviewport.GetGlobalRect().GetCenter()
						: GetViewport().GetMousePosition();
					cross.Position = mouse - cross.Size / 2;
					this.Position = mouse - hotspot * 4;//the cursor is drawn 4 times its original size
					ShowContextHint(mouse, delta);
				}
			}
			//}
		}

		/// <summary>The icon by the cursor saying what a left click in the 3D view will do (modern controls' context clicks).</summary>
		TextureRect hint;
		double hintTimer;
		uimanager.InteractionModes hintMode = uimanager.InteractionModes.ModeNone;

		/// <summary>
		/// Under modern controls with no icon selected, looks (a few times a second, as reading the
		/// object picture is not free) at what is under the cursor and shows the icon of what a left
		/// click would do to it, half the size of the icons on the panel, beside the cursor.
		/// </summary>
		void ShowContextHint(Vector2 mouse, double delta)
		{
			if (hint == null)
			{
				hint = new TextureRect { MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Visible = false };
				GetParent().AddChild(hint);
			}
			bool wanted = Controls.ContextClicks && uimanager.InGame && !uimanager.blockinput
				&& playerdat.ObjectInHand == -1 && uimanager.UsageMode == 0 && uimanager.IsMouseInViewPort();
			hintTimer -= delta;
			if (wanted && hintTimer <= 0)
			{
				hintTimer = 0.12;
				var mode = uimanager.InteractionModes.ModeNone;
				var text = (Texture2D)uimanager.instance.uwsubviewport_objectinfo.GetTexture();
				var at = Controls.ViewportPoint();
				var image = text.GetImage();
				if (at.X >= 0 && at.Y >= 0 && at.X < image.GetWidth() && at.Y < image.GetHeight())
				{
					var pixel = image.GetPixel((int)at.X, (int)at.Y);
					var index = (pixel.B8 << 8) | pixel.G8;
					if (pixel.R8 == 0 && index > 0 && index <= 1024)
					{
						mode = Controls.ContextMode(UWTileMap.current_tilemap.LevelObjects[index]);
					}
				}
				if (mode != hintMode)
				{
					hintMode = mode;
					if (mode != uimanager.InteractionModes.ModeNone)
					{
						int i = (int)mode;
						if (UWClass._RES == UWClass.GAME_UW2)
						{
							hint.Texture = uimanager.instance.UW2InteractionButtonsOn[i];
							hint.Material = uimanager.instance.InteractionButtonsUW2[i].Material;
						}
						else
						{
							hint.Texture = uimanager.grLfti.LoadImageAt(i * 2 + 1, false);
							hint.Material = uimanager.instance.InteractionButtonsUW1[i].Material;
						}
						hint.Size = hint.Texture.GetSize() * 2;
					}
				}
			}
			hint.Visible = wanted && hintMode != uimanager.InteractionModes.ModeNone;
			if (hint.Visible)
			{
				hint.Position = mouse + new Vector2(12, 12);
			}
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

		/// <summary>
		/// The point of the cursor picture, in its original pixels, that sits on the mouse: the
		/// centre pixel of the crosshair and target, the tip of the map's quills, the middle of the
		/// map's eraser and of an object in hand, and for the rest a quarter of the way in, where
		/// every cursor used to sit.
		/// </summary>
		Vector2 hotspot;

		public void InitCursor()
		{
			SetCursorToCursor(0);
			Input.MouseMode = Input.MouseModeEnum.Hidden;
		}

		public void SetCursorToObject(int index)
		{
			Texture = uimanager.grObjects.LoadImageAt(index);
			Material = uimanager.grObjects.GetMaterial(index);
			this.Size = Texture.GetSize() * 4;
			hotspot = Texture.GetSize() / 2;
		}

		/// <summary>
		/// Shows cursor index of cursors.gr; without one, the pointer of the selected icon: use, look and get show
		/// cursor 11, anything else the usual pointer (deal_with_icons' force_mouse_cursor).
		/// </summary>
		public void SetCursorToCursor(int index = -1)
		{
			if (index < 0)
			{
				index = uimanager.InteractionMode switch
				{
					uimanager.InteractionModes.ModeUse or uimanager.InteractionModes.ModeLook or uimanager.InteractionModes.ModePickup => 11,
					_ => 0,
				};
			}
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
			hotspot = Texture.GetSize() / 4;
			if (HdArt.Active)
			{//with the HD art, the crosshair and the target are drawn again smooth
				hdCursors[index] ??= HdCursors.Make(index, Texture, out hdHotspots[index]);
				if (hdCursors[index] != null)
				{
					Texture = hdCursors[index];
				}
			}
			if (index == 0 || index == 9)
			{
				hotspot = HdCursorHotspot(index);
			}
			else if (index == 12 || index == 14)
			{//the map's quills write with their tip
				hotspot = HdCursors.Tip(Texture);
			}
			else if (index == 13)
			{//the map's eraser rubs with its middle
				hotspot = Texture.GetSize() / 2;
			}
			this.Size = Texture.GetSize() * 4;
			//place it now: while a map note is written the cursor stops following the mouse, and the
			//writing quill has to point where the click was, not where the other quill's corner was
			this.Position = cross.Position + cross.Size / 2 - hotspot * 4;
		}

		readonly ImageTexture[] hdCursors = new ImageTexture[32];
		readonly Vector2[] hdHotspots = new Vector2[32];

		/// <summary>The crosshair's and the target's centre pixel, found as the redraw finds it (also without the HD art).</summary>
		Vector2 HdCursorHotspot(int index)
		{
			if (hdHotspots[index] == Vector2.Zero)
			{
				HdCursors.Make(index, uimanager.grCursors.LoadImageAt(index), out hdHotspots[index]);
			}
			return hdHotspots[index] == Vector2.Zero ? Texture.GetSize() / 2 : hdHotspots[index];
		}
	}

}//end namespace