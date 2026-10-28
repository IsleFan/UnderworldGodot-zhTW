using Godot;
using System;
using System.Diagnostics;
using Underworld;
using System.IO;
using System.Linq;
using System.ComponentModel;

/// <summary>
/// Node to initialise the game
/// </summary>
public partial class main : Node3D
{

	static bool EnablePositionDebug = false;
	/// <summary>
	/// Blocks input for certain modes
	/// </summary>

	public static main instance;

	// Called when the node enters the scene tree for the first time.
	[Export] public Camera3D cam_world;
	[Export] public Node3D GimbalYaw_world;
	[Export] public Node3D GimbalRoll_world;
	public static Camera3D cameraPitchGimbal_world; //pitch
	public static Node3D cameraRollGimbal_world; // up/down
	public static Node3D cameraYawGimbal_world; // yaw


	[Export] public Camera3D cam_sprites;
	[Export] public Node3D GimbalYaw_sprites;
	[Export] public Node3D GimbalRoll_sprites;
	public static Camera3D cameraPitchGimbal_sprites; //pitch
	public static Node3D cameraRollGimbal_sprites; // up/down
	public static Node3D cameraYawGimbal_sprites; // yaw


	[Export] public Camera3D cam_objectinfo;
	[Export] public Node3D GimbalYaw_objectinfo;
	[Export] public Node3D GimbalRoll_objectinfo;
	public static Camera3D cameraPitchGimbal_objectinfo; //pitch
	public static Node3D cameraRollGimbal_objectinfo; // up/down
	public static Node3D cameraYawGimbal_objectinfo; // yaw



	[Export] public AudioStreamPlayer DigitalAudioPlayer;
	[Export] public RichTextLabel lblPositionDebug;

	[Export] public SubViewport secondarycameras; //remove?

	double gameRefreshTimer = 0f;
	static double testclock = 0;

	double cycletime = 0;

	public static double playingnotetimer = 0;

	public static bool DoRedraw = false;


	public static double PitTimer = 0f;
	public static uint GlobalPITTimer = 0;
	//static uint PitTimer = 0;
	static uint LastGlobalPitTimer = 0;
	static byte EasyMoveFrameIncrement = 0;

	public static byte ThisFrameDelta = 0;
	static byte PreviousFrameDelta = 0;

	public const uint LayerGeo = 2;
	public const uint LayerXFER = 4;
	public const uint LayerObjectInfo = 8;
	public override void _Ready()
	{
		instance = this;
		cameraPitchGimbal_world = cam_world;
		cameraRollGimbal_world = GimbalRoll_world;
		cameraYawGimbal_world = GimbalYaw_world;

		cameraPitchGimbal_sprites = cam_sprites;
		cameraRollGimbal_sprites = GimbalRoll_sprites;
		cameraYawGimbal_sprites = GimbalYaw_sprites;


		cameraPitchGimbal_objectinfo = cam_objectinfo;
		cameraRollGimbal_objectinfo = GimbalRoll_objectinfo;
		cameraYawGimbal_objectinfo = GimbalYaw_objectinfo;

		//uimanager.instance = uwUI;	
		if (uwsettings.instance != null)
		{
			GetTree().DebugCollisionsHint = uwsettings.instance.showcolliders;
		}


		//_ = Peaky.Coroutines.Coroutine.Run(PITTIMER(), main.instance);

	}

	/// <summary>
	/// Experiment Emulation of an old skol PIT Timer
	/// </summary>
	/// <returns></returns>
	// static IEnumerator PITTIMER()
	// {
	// 	while (true)
	// 	{
	// 		yield return new WaitForSeconds(0.00391f);  //0.054945f
	// 		GlobalPITTimer++;
	// 	}
	// 	yield return null;
	// }


	/// <summary>
	/// Test seam. Invoked as the first statement of StartGame so a headless test can prove
	/// the game did not start. Nothing in the game assigns it, and it is internal because
	/// the Godot tests compile into this same assembly.
	///
	/// It exists because suppression is otherwise unobservable: every other signal belongs
	/// to the scene root and stays correct whether or not StartGame ran. Checking a
	/// downstream side effect such as ObjectCreator.grObjects is not sound either, because
	/// StartGame can throw partway through and leave it null while still having been called.
	/// </summary>
#if DEBUG
	internal static System.Action StartGameObserver;
#endif

	public static void StartGame()
	{
#if DEBUG
		StartGameObserver?.Invoke();
#endif
		if (cameraPitchGimbal_world == null)
		{
			if (instance.cam_world == null)
			{
				Debug.Print("Main Cam instance is null. trying to find it's node");
				instance.cam_world = (Camera3D)instance.GetNode("/root/Underworld/WorldViewContainer/SubViewport/Camera3D");
			}
			cameraPitchGimbal_world = instance.cam_world;
			if (cameraPitchGimbal_world == null)
			{
				Debug.Print("Gamecam is still null!");
			}
		}
		if (uimanager.instance == null)
		{
			Debug.Print("UI Manager is null");
			//UI/uiManager
			uimanager.instance = (uimanager)instance.GetNode("/root/Underworld/UI/uiManager");
			if (uimanager.instance == null)
			{
				Debug.Print("UIManager is still null!!");
			}
		}
		cameraPitchGimbal_world.Fov = Math.Max(50, uwsettings.instance.FOV);
		cameraPitchGimbal_sprites.Fov = cameraPitchGimbal_world.Fov;
		uimanager.EnableDisable(instance.lblPositionDebug, EnablePositionDebug);

		ObjectCreator.grObjects = new GRLoader(GRLoader.OBJECTS_GR, GRLoader.GRShaderMode.BillboardSpriteShader);
		ObjectCreator.grObjects.UseRedChannel = true;
		ObjectCreator.grObjects.UseCropping = false;
		ObjectCreator.grObjects.XFER = ArtLoader.XferChannnelMode.AllColours;

		// ObjectCreator.grObjectsXfer = new GRLoader(GRLoader.OBJECTS_GR, GRLoader.GRShaderMode.BillboardSpriteShader);
		// ObjectCreator.grObjectsXfer.UseRedChannel = true;
		// ObjectCreator.grObjectsXfer.UseCropping = false;
		// ObjectCreator.grObjectsXfer.XFER = ArtLoader.XferChannnelMode.XFEROnly;

		// ObjectCreator.grObjectsInfo = new GRLoader(GRLoader.OBJECTS_GR, GRLoader.GRShaderMode.BillboardSpriteShader);
		// ObjectCreator.grObjectsInfo.UseRedChannel = true;
		// ObjectCreator.grObjectsInfo.UseCropping = false;
		// ObjectCreator.grObjectsInfo.XFER = ArtLoader.XferChannnelMode.AllColours;

		Palette.CurrentPalette = 0;
		uimanager.instance.InitUI();
#if DEBUG
		if (int.TryParse(OS.GetEnvironment("UW_TESTCUTS"), out int testcuts))
		{//Testing aid: play this cutscene straight away, to see its subtitles. A windowed one (100h and up) plays in its
		 //game's layer, which stays on.
			uimanager.EnableDisable(uimanager.instance.uw1UI, testcuts >= 0x100 && UWClass._RES != UWClass.GAME_UW2);
			uimanager.EnableDisable(uimanager.instance.uw2UI, testcuts >= 0x100 && UWClass._RES == UWClass.GAME_UW2);
			uimanager.EnableDisable(uimanager.instance.uwviewport, false);//as a death's cutscene does, so the 3D view does not cover it
			uimanager.EnableDisable(uimanager.instance.PanelInventory, false);
			uimanager.EnableDisable(uimanager.instance.ManaFlaskPanel, false);
			uimanager.EnableDisable(uimanager.instance.HealthFlaskPanel, false);
			cutsplayer.PlayCutscene(testcuts, uimanager.ReturnToMainMenu);
			return;
		}
		if (OS.GetEnvironment("UW_TESTMENU") == "1")
		{//Testing aid: straight to the main menu, to see its buttons.
			uimanager.ReturnToMainMenu();
			if (OS.GetEnvironment("UW_TESTSELFCHECK") == "1")
			{//and the menu's theme starts again once it has ended (load_new_music)
				instance.GetTree().CreateTimer(2.0).Timeout += () =>
				{
					var theme = XMIMusic.CurrentlyPlayingThemeNo;
					MusicStreamPlayer.Instance?.Stop();
					XMIMusic.RefreshMusic();
					GD.Print($"SELFCHECK menu music: theme {theme} stopped, after the refresh playing {MusicStreamPlayer.Instance?.IsPlaying} theme {XMIMusic.CurrentlyPlayingThemeNo}");
				};
			}
			return;
		}
		if (OS.GetEnvironment("UW_TESTCHARGEN") == "1")
		{//Testing aid: straight into character creation, past gender, handedness and class, to see its text.
			uimanager.InitChargenUI();
			for (int stage = 0; stage < 3; stage++)
			{
				chargen.SubmitChargenOption(chargen.CurrentStage, 0);
			}
			return;
		}
		if (OS.GetEnvironment("UW_AUTOSTART") == "1")
		{//Testing aid: straight into the first level with no intro, menu or character creation, the way the demo starts.
			playerdat.InitEmptyPlayer();
			playerdat.CharName = "TEST";//an empty player has 0 hp and dies at once, so give it the demo's defaults
			playerdat.max_hp = 30;
			playerdat.play_hp = 30;
			playerdat.STR = 20;
			playerdat.INT = 20;
			playerdat.DEX = 20;
			var startfolder = OS.GetEnvironment("UW_AUTOSTART_FOLDER");//Testing aid: start from this save folder (SAVE1..4) instead of a new game
			uimanager.instance.JourneyOnwards(string.IsNullOrEmpty(startfolder) ? "DATA" : startfolder);
			var teststring = OS.GetEnvironment("UW_TESTSTRING").Split(',');
			if (teststring.Length == 2 && int.TryParse(teststring[0], out int testblock) && int.TryParse(teststring[1], out int testindex))
			{//Testing aid: show this string (block,index) in the message scroll, to see how it lays out.
				uimanager.AddToMessageScroll(GameStrings.GetDisplayString(testblock, testindex));
			}
			if (int.TryParse(OS.GetEnvironment("UW_TESTPANEL"), out int testpanel))
			{//Testing aid: turn to this side panel (1 runes, 2 stats) once the level is loaded.
				instance.GetTree().CreateTimer(1.0).Timeout += () => uimanager.SetPanelMode(testpanel);
			}
			if (OS.GetEnvironment("UW_TESTCONTROLS") == "1")
			{//Testing aid (modern controls): point at the nearest thing that can be picked up, then shift + click it, then mouse look, the weapon key and the wheel, a second apart, printing what each did.
				var tree = instance.GetTree();
				Vector2 target = Vector2.Zero;
				tree.CreateTimer(1.5).Timeout += () =>
				{//the object picture is what clicks read: find the nearest pixel of something that can be picked up
					var view = uimanager.instance.uwviewport;
					var image = ((Texture2D)uimanager.instance.uwsubviewport_objectinfo.GetTexture()).GetImage();
					float best = float.MaxValue;
					for (int y = 0; y < image.GetHeight(); y += 2)
					{
						for (int x = 0; x < image.GetWidth(); x += 2)
						{
							var pixel = image.GetPixel(x, y);
							var index = (pixel.B8 << 8) | pixel.G8;
							if (pixel.R8 != 0 || index <= 0 || index > 1024) { continue; }
							var o = UWTileMap.current_tilemap.LevelObjects[index];
							if (o.majorclass == 1 || !commonObjDat.CanBePickedUp(o.item_id)) { continue; }
							float d = (image.GetHeight() - y);//lowest on screen is nearest
							if (d < best)
							{
								best = d;
								target = view.GetGlobalRect().Position + new Vector2(x, y);
								GD.Print($"TESTCONTROLS target {o.index} {GameStrings.GetSimpleObjectNameUW(o.item_id)} at pixel {x},{y}");
							}
						}
					}
					target = instance.GetTree().Root.GetFinalTransform() * target;//window pixels, which input events and the warp use
					Input.WarpMouse(target);
					Input.ParseInputEvent(new InputEventMouseMotion { Position = target, GlobalPosition = target });
				};
				tree.CreateTimer(2.0).Timeout += () =>
				{//right click looks
					Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = target, GlobalPosition = target });
					Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false, Position = target, GlobalPosition = target });
				};
				tree.CreateTimer(2.5).Timeout += () =>
				{
					GD.Print($"TESTCONTROLS mode {uimanager.InteractionMode}, in viewport {uimanager.IsMouseInViewPort()}");
					Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, Pressed = true });
					Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = target, GlobalPosition = target, ShiftPressed = true });
					Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = target, GlobalPosition = target, ShiftPressed = true });
					Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, Pressed = false });
				};
				tree.CreateTimer(3.5).Timeout += () =>
				{
					GD.Print($"TESTCONTROLS after click: in hand {playerdat.ObjectInHand}, backpack {string.Join(",", uimanager.BackPackIndices)}");
					Input.ParseInputEvent(new InputEventKey { Keycode = Controls.KeyFor(Controls.Action.MouseLook), Pressed = true });
					Input.ParseInputEvent(new InputEventKey { Keycode = Controls.KeyFor(Controls.Action.MouseLook), Pressed = false });
				};
				short yawBefore = 0;
				tree.CreateTimer(4.0).Timeout += () =>
				{
					yawBefore = playerdat.PlayerCameraYaw_dseg_8294;
					Input.ParseInputEvent(new InputEventMouseMotion { Relative = new Vector2(240, -60), ScreenRelative = new Vector2(240, -60) });
				};
				tree.CreateTimer(4.5).Timeout += () =>
					GD.Print($"TESTCONTROLS mouse look {Controls.MouseLookActive}, mouse mode {Input.MouseMode}, yaw {yawBefore} -> {playerdat.PlayerCameraYaw_dseg_8294}, pitch {playerdat.PlayerCameraPitch_dseg_67d6_33D6}");
				tree.CreateTimer(5.0).Timeout += () =>
				{
					Input.ParseInputEvent(new InputEventKey { Keycode = Controls.KeyFor(Controls.Action.Weapon), Pressed = true });
					Input.ParseInputEvent(new InputEventKey { Keycode = Controls.KeyFor(Controls.Action.Weapon), Pressed = false });
				};
				tree.CreateTimer(6.0).Timeout += () =>
				{
					GD.Print($"TESTCONTROLS weapon drawn {playerdat.play_drawn}, mode {uimanager.InteractionMode}");
					Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true });
				};
				tree.CreateTimer(6.5).Timeout += () => GD.Print($"TESTCONTROLS after wheel: mode {uimanager.InteractionMode}, weapon drawn {playerdat.play_drawn}");
			}
			if (int.TryParse(OS.GetEnvironment("UW_TESTCUTS_INGAME"), out int testcutsgame))
			{//Testing aid: once the level is loaded, play this cutscene the way a death plays its own (the 3D view off), to
			 //see a windowed cutscene where it belongs
				instance.GetTree().CreateTimer(1.0).Timeout += () =>
				{
					uimanager.EnableDisable(uimanager.instance.uwviewport, false);
					cutsplayer.PlayCutscene(CutsceneNo: testcutsgame, callBackMethod: null, useSingleRedChannel: true);
				};
			}
			if (OS.GetEnvironment("UW_TESTSELFCHECK") == "1")
			{//Testing aid: check by measurement what cannot be seen in a picture, printing SELFCHECK lines
				var tree = instance.GetTree();
				int lastClock = 0;
				tree.CreateTimer(1.0).Timeout += () => lastClock = playerdat.ClockValue;
				tree.CreateTimer(4.0).Timeout += () =>
				{//the physics clock: what it advanced by in 3 seconds of rounds (about 0x19 a round at 256 ticks a second)
					var delta = playerdat.ClockValue - lastClock;
					GD.Print($"SELFCHECK clock advanced {delta} ticks in 3 s (256 a second expected: {3 * 256})");
					//digital sound: a sound from the west, the player facing north then south
					var px = (playerdat.playerObject.tileX << 3) + 3; var py = (playerdat.playerObject.tileY << 3) + 3;
					var before = UWsoundeffects.CalculateSoundFallOff(px - 16, py, 3, 0);
					var heading = playerdat.playerObject.heading;
					playerdat.playerObject.heading = (short)((heading + 4) & 7);
					var after = UWsoundeffects.CalculateSoundFallOff(px - 16, py, 3, 0);
					playerdat.playerObject.heading = heading;
					GD.Print($"SELFCHECK sound from the west: pan {before.Pan:X2} vol {before.Vol:X2}; turned round: pan {after.Pan:X2} vol {after.Vol:X2} (the pan should cross 0x40)");
					//the dream: an object held on waking stays where the player was
					var index = ObjectCreator.PrepareNewObject(0x80);
					if (index != 0)
					{
						playerdat.ObjectInHand = index;
						playerdat.DreamingInVoid = true;
						playerdat.DreamDungeon = playerdat.dungeon_level;
						playerdat.DreamTileX = playerdat.playerObject.tileX;
						playerdat.DreamTileY = playerdat.playerObject.tileY;
						var tx = playerdat.playerObject.tileX; var ty = playerdat.playerObject.tileY;
						sleep.AwakenFromTheVoid();
						var o = UWTileMap.current_tilemap.LevelObjects[index];
						GD.Print($"SELFCHECK dream: held after waking {playerdat.ObjectInHand} (-1 expected); the object at tile {o.tileX},{o.tileY}, player was at {tx},{ty} (within 6 fine units expected)");
					}
					//music: a theme that has ended starts again
					var theme = XMIMusic.CurrentlyPlayingThemeNo;
					MusicStreamPlayer.Instance?.Stop();
					XMIMusic.RefreshMusic();
					GD.Print($"SELFCHECK music: theme {theme} stopped, after the refresh playing {MusicStreamPlayer.Instance?.IsPlaying} theme {XMIMusic.CurrentlyPlayingThemeNo}");
				};
			}
			if (OS.GetEnvironment("UW_TESTTELEPORT") == "1")
			{//Testing aid: teleport within the level once it is loaded, to see the view fade out and back in
				instance.GetTree().CreateTimer(1.0).Timeout += () =>
					Teleportation.Teleport(character: 0, tileX: playerdat.playerObject.tileX, tileY: playerdat.playerObject.tileY + 1, newLevel: 0, HeadingHeightFlag: 0);
			}
			if (OS.GetEnvironment("UW_TESTMAP") == "1")
			{//Testing aid: open the map and put the mouse at (600, 400), to see where the quill points.
				instance.GetTree().CreateTimer(1.0).Timeout += () => uimanager.DrawAutoMap(playerdat.dungeon_level - 1, 0);
				instance.GetTree().CreateTimer(1.5).Timeout += () =>
				{
					var at = instance.GetTree().Root.GetFinalTransform() * new Vector2(600, 400);
					Input.WarpMouse(at);
					Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
				};
				instance.GetTree().CreateTimer(2.0).Timeout += () =>
				{//click to start a note: the writing quill
					var at = instance.GetTree().Root.GetFinalTransform() * new Vector2(600, 400);
					Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = at, GlobalPosition = at });
					Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at });
				};
				instance.GetTree().CreateTimer(2.3).Timeout += () =>
				{//type TEST and end the note: it should start at the click, its top 4 rows above it (x 600, y 384 here)
					foreach (var (key, ch) in new[] { (Key.T, 'T'), (Key.E, 'E'), (Key.S, 'S'), (Key.T, 'T') })
					{
						Input.ParseInputEvent(new InputEventKey { Keycode = key, Unicode = ch, Pressed = true });
						Input.ParseInputEvent(new InputEventKey { Keycode = key, Unicode = ch, Pressed = false });
					}
					Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, Pressed = true });
					Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, Pressed = false });
				};
			}
			if (OS.GetEnvironment("UW_TESTLOOK") == "1")
			{//Testing aid: look at a creature, a named NPC, a weapon and a wall once the level is loaded, to see the descriptions.
				instance.GetTree().CreateTimer(1.0).Timeout += () =>
				{
					var objs = UWTileMap.current_tilemap.LevelObjects;
					var picks = new[]
					{
						System.Array.FindIndex(objs, o => o != null && o.majorclass == 1 && o.npc_whoami == 0),
						System.Array.FindIndex(objs, o => o != null && o.majorclass == 1 && o.npc_whoami != 0 && o.npc_whoami < 240),
						System.Array.FindIndex(objs, o => o != null && o.majorclass == 0 && o.index > 0),
					};
					foreach (var i in picks)
					{
						if (i > 0) { look.LookAtWorld(i, lore: 1, lookMode: false); }
					}
					uimanager.AddToMessageScroll(GameStrings.TextureDescription(1));
				};
			}
			if (int.TryParse(OS.GetEnvironment("UW_TESTOPTIONS"), out int testoptions))
			{//Testing aid: open the options once the level is loaded, or with another number only that picture (OptionButtonIndices), to see their lettering.
				instance.GetTree().CreateTimer(1.0).Timeout += () =>
				{
					var watch = System.Diagnostics.Stopwatch.StartNew();
					uimanager.InteractionModeToggle(uimanager.InteractionModes.ModeOptions);
					GD.Print($"TESTOPTIONS opened in {watch.ElapsedMilliseconds} ms");
					if (testoptions != 1)
					{//another menu's picture: hide the top menu's buttons so all of it shows
						uimanager.SetGameOptionButtons(new[] { -1, -1, -1, -1, -1, -1, -1 });
						uimanager.SetGameOptionsBackground(testoptions);
					}
				};
			}
			var testsfx = OS.GetEnvironment("UW_TESTSFX").Split(',', StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < testsfx.Length; i++)
			{//Testing aid: play these sound effects at the avatar 1.5 seconds apart, inside the 15 quiet seconds before the music, to measure them in a recording; "door" works the nearest door, and anything else just takes up a slot.
				if (byte.TryParse(testsfx[i], out byte effect))
				{
					instance.GetTree().CreateTimer(1.0 + 1.5 * i).Timeout += () => UWsoundeffects.PlaySoundEffectAtAvatar(effect, 0x40, 0);
				}
				else if (testsfx[i] == "door")
				{//the nearest unlocked door, opened or shut the way the player does it, with its positional sound
					instance.GetTree().CreateTimer(1.0 + 1.5 * i).Timeout += () =>
					{
						var player = playerdat.playerObject;
						var nearest = UWTileMap.current_tilemap.LevelObjects
							.Where(o => o != null && o.index > 0 && o.majorclass == 5 && o.minorclass == 0 && !a_lock.GetIsLocked(o))
							.OrderBy(o => Math.Abs(o.tileX - player.tileX) + Math.Abs(o.tileY - player.tileY))
							.FirstOrDefault();
						GD.Print($"UW_TESTSFX door {(nearest == null ? "none" : $"{nearest.index} at {nearest.tileX},{nearest.tileY}, player at {player.tileX},{player.tileY}")}");
						if (nearest != null) { door.Use(nearest, true); }
					};
				}
			}
			if (OS.GetEnvironment("UW_TESTMSGS") == "1")
			{//Testing aid: once the level is loaded, set off UW2's composed messages one at a time on the level's own objects, to see them in the message scroll.
				var objs = UWTileMap.current_tilemap.LevelObjects;
				var weapon = System.Array.FindIndex(objs, o => o != null && o.index > 0 && o.majorclass == 0);
				var critter = System.Array.FindIndex(objs, o => o != null && o.index > 0 && o.majorclass == 1 && o.npc_whoami != 0);
				var plain = System.Array.Find(objs, o => o != null && o.index > 0 && o.owner == 0 && o.majorclass != 1);
				void Call(System.Type type, string method, params object[] args) =>
					type.GetMethod(method, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Invoke(null, args);
				var steps = new (string, System.Action)[]
				{
					("pocketwatch", () => pocketwatch.Use(objs[weapon], false)),
					("storage crystal", () => storage_crystal.Use(objs[weapon], false)),
					("look", () => look.LookAt(objs[weapon], objs, 2)),
					("orb rock", () => OrbRock.Use(objs[weapon], false, null)),
					("food taste", () => Call(typeof(Food), "TasteDescriptionString", objs[weapon])),
					("trap disarmed", () => Call(typeof(trapdisarming), "DoTrapDisarm", objs, 100, objs[weapon], null, objs[weapon])),
					("mending spell", () => repair.MendingSpell(weapon, objs)),
					("vending sign", () => Call(typeof(a_hack_trap_vending), "VendingForSaleSign", plain)),
					("study monster", () => Call(typeof(SpellCasting), "StudyMonster", critter, objs)),
					("critter look", () => npc.LookAt(objs[critter])),
					("detect monsters", () => tracking.DetectMonsters(8, 40)),
					("repair estimate", () => repair.RepairLogic(objs[weapon], 30, false).MoveNext()),
					//last: the blast can kill the test character, and the death cutscene hides the scroll
					("enchant destroyed", () => Call(typeof(enchanting), "EnchantFailureExplosion", objs[weapon], playerdat.playerObject.tileX, playerdat.playerObject.tileY)),
				};
				for (int i = 0; i < steps.Length; i++)
				{
					var (name, step) = steps[i];
					instance.GetTree().CreateTimer(1.0 + 0.8 * i).Timeout += () =>
					{
						GD.Print($"UW_TESTMSGS {name}");
						step();
					};
				}
			}
			if (int.TryParse(OS.GetEnvironment("UW_TESTSAVEUI"), out int testsaveui))
			{//Testing aid: save to this slot through the Options menu the way a click does, then submit the description prompt as Enter does.
				const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
				bool PromptOpen() => ((SaveDescriptionPrompt)typeof(uimanager).GetField("SaveDescription_Prompt", any).GetValue(null)).Active;
				instance.GetTree().CreateTimer(1.0).Timeout += () =>
				{
					uimanager.InteractionModeToggle(uimanager.InteractionModes.ModeOptions);
					uimanager.CurrentGameOptionMenu = uimanager.OptionMenus.SaveMenu;
					var click = new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left };
					typeof(uimanager).GetMethod("_on_game_options_input", any).Invoke(uimanager.instance, new object[] { click, (long)testsaveui });
					GD.Print($"UW_TESTSAVEUI prompt open {PromptOpen()}");
				};
				instance.GetTree().CreateTimer(2.0).Timeout += () =>
				{
					typeof(uimanager).GetMethod("OnSaveDescriptionSubmitted", any).Invoke(null, new object[] { "" });
					GD.Print($"UW_TESTSAVEUI submitted, prompt open {PromptOpen()}");
				};
			}
			if (int.TryParse(OS.GetEnvironment("UW_TESTSAVE"), out int testsave))
			{//Testing aid: change some state, save to this slot, load it back and print both states, to check a save round trip.
				string State()
				{
					var p = playerdat.playerObject;
					var objs = UWTileMap.current_tilemap.LevelObjects;
					var doors = objs.Where(o => o != null && o.index > 0 && o.majorclass == 5 && o.minorclass == 0).Select(o => door.isOpen(o) ? 1 : 0).Sum();
					var objects = objs.Count(o => o != null && o.index > 0 && o.tileX < 64);
					var slots = string.Join(",", Enumerable.Range(0, 19).Select(s => playerdat.GetInventorySlotObject(s)).Select(o => o == null ? "-" : $"{o.item_id:X}"));
					return $"level {playerdat.dungeon_level} tile {p.tileX},{p.tileY} pos {p.xpos},{p.ypos},{p.zpos} heading {p.heading} hp {playerdat.play_hp} mana {playerdat.play_mana} open doors {doors} objects {objects} slots {slots}";
				}
				instance.GetTree().CreateTimer(1.0).Timeout += () =>
				{
					playerdat.play_hp = 17;
					playerdat.play_mana = 3;
					var player = playerdat.playerObject;
					var shut = UWTileMap.current_tilemap.LevelObjects
						.Where(o => o != null && o.index > 0 && o.majorclass == 5 && o.minorclass == 0 && !a_lock.GetIsLocked(o) && !door.isOpen(o))
						.OrderBy(o => Math.Abs(o.tileX - player.tileX) + Math.Abs(o.tileY - player.tileY))
						.FirstOrDefault();
					if (shut != null) { door.Use(shut, true); }
					if (OS.GetEnvironment("UW_TESTSAVE_PICKUP") != "")
					{//take the nearest container with something in it off the floor into the first free backpack slot, as the panel does
						var box = UWTileMap.current_tilemap.LevelObjects
							.Where(o => o != null && o.index > 1 && o.tileX < 64 && o.majorclass == 2 && o.minorclass == 0 && o.link != 0)
							.OrderBy(o => Math.Abs(o.tileX - player.tileX) + Math.Abs(o.tileY - player.tileY))
							.FirstOrDefault();
						int free = Enumerable.Range(11, 8).FirstOrDefault(s => playerdat.GetInventorySlotListHead(s) == 0);
						if (box != null && free != 0 && pickup.PickUp(box.index, UWTileMap.current_tilemap.LevelObjects))
						{
							var index = playerdat.AddObjectToPlayerInventory(playerdat.ObjectInHand, false);
							playerdat.SetInventorySlotListHead(free, index);
							playerdat.ObjectInHand = -1;
							GD.Print($"UW_TESTSAVE picked up {box.item_id:X} into slot {free} as {index}");
						}
					}
					GD.Print($"UW_TESTSAVE before {State()}");
				};
				//UW_TESTSAVE_VIA=level,x,y saves on another level, then goes back to see that the first one kept its changes
				var via = OS.GetEnvironment("UW_TESTSAVE_VIA").Split(',').Select(v => int.TryParse(v, out int n) ? n : -1).ToArray();
				bool travel = via.Length == 3 && via.All(v => v >= 0);
				int homeX = 0, homeY = 0, homeLevel = 0;
				if (travel)
				{
					instance.GetTree().CreateTimer(1.5).Timeout += () =>
					{
						homeX = playerdat.playerObject.tileX; homeY = playerdat.playerObject.tileY; homeLevel = playerdat.dungeon_level;
						Teleportation.Teleport(0, via[1], via[2], via[0], 0);
					};
				}
				instance.GetTree().CreateTimer(2.5).Timeout += () =>
				{
					GD.Print($"UW_TESTSAVE saving to slot {testsave}");
					SaveGame.Save(testsave, "roundtrip");
					GD.Print($"UW_TESTSAVE saved {State()}");
				};
				instance.GetTree().CreateTimer(3.5).Timeout += () => uimanager.instance.JourneyOnwards($"SAVE{testsave}");
				instance.GetTree().CreateTimer(5.0).Timeout += () => GD.Print($"UW_TESTSAVE loaded {State()}");
				if (travel)
				{
					instance.GetTree().CreateTimer(5.5).Timeout += () => Teleportation.Teleport(0, homeX, homeY, homeLevel, 0);
					instance.GetTree().CreateTimer(7.0).Timeout += () => GD.Print($"UW_TESTSAVE back home {State()}");
				}
			}
			if (OS.GetEnvironment("UW_TESTANSWER") == "1")
			{//Testing aid (with UW_TESTTALK): past the [MORE]s to the first answer menu, print where each answer is, point at the second one, then click it.
				var tree = instance.GetTree();
				bool done = false;
				void Step()
				{
					if (done) { return; }
					if (MessageDisplay.WaitingForMore) { MessageDisplay.WaitingForMore = false; }
					if (ConversationVM.WaitingForInput && ConversationVM.MaxAnswer >= 2)
					{
						done = true;
						var label = uimanager.MessageScroll;
						float first = -1, last = -1;
						for (float y = 0; y <= label.Size.Y; y += 1)
						{
							int a = uimanager.instance.AnswerAt(y);
							if (a == 2) { if (first < 0) { first = y; } last = y; }
						}
						for (int l = 0; l < label.GetLineCount(); l++) { GD.Print($"TESTANSWER line {l} at {label.GetLineOffset(l)}"); }
						var font = label.GetThemeFont("normal_font"); int fsize = label.GetThemeFontSize("normal_font_size");
						GD.Print($"TESTANSWER font {font?.GetType().Name} size {fsize} ascent {font?.GetAscent(fsize)} descent {font?.GetDescent(fsize)} height {font?.GetHeight(fsize)} sep {label.GetThemeConstant("line_separation")}");
						GD.Print($"TESTANSWER answers {ConversationVM.MaxAnswer}, answer 2 from {first} to {last}, scroll at {label.GetGlobalRect()}");
						var at = instance.GetTree().Root.GetFinalTransform() * (label.GetGlobalRect().Position + new Vector2(200, (first + last) / 2));
						Input.WarpMouse(at);
						uimanager.CursorOverMessageScroll = true;//the panel's mouse_entered does not follow a warped mouse in a recording
						Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
						tree.CreateTimer(1.0).Timeout += () =>
						{
							GD.Print($"TESTANSWER highlighted {uimanager.instance.scroll.HighlightOption}, mouse at {label.GetLocalMousePosition()}");
							Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = at, GlobalPosition = at });
							Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at });
						};
						tree.CreateTimer(1.5).Timeout += () => GD.Print($"TESTANSWER picked {ConversationVM.PlayerNumericAnswer}");
						return;
					}
					tree.CreateTimer(0.5).Timeout += Step;
				}
				tree.CreateTimer(1.5).Timeout += Step;
			}
			if (int.TryParse(OS.GetEnvironment("UW_TESTTALK"), out int testtalk))
			{//Testing aid: talk to the first NPC of this whoami on the level once it is loaded, to see conversation text.
				instance.GetTree().CreateTimer(1.0).Timeout += () =>
				{
					foreach (var obj in UWTileMap.current_tilemap.LevelObjects)
					{
						if (obj != null && obj.majorclass == 1 && obj.npc_whoami == testtalk)
						{
							ConversationVM.StartConversation(obj);
							break;
						}
					}
				};
			}
			return;
		}
#endif
		if (UWClass._RES != UWClass.GAME_UWDEMO)
		{
			uimanager.EnableDisable(uimanager.instance.uw1UI, false);
			uimanager.EnableDisable(uimanager.instance.uw2UI, false);
			uimanager.EnableDisable(uimanager.instance.PanelInventory, false);
			uimanager.EnableDisable(uimanager.instance.ManaFlaskPanel, false);
			uimanager.EnableDisable(uimanager.instance.HealthFlaskPanel, false);
			cutsplayer.PlayCutscene(9, uimanager.ReturnToMainMenuOrIntroduction);
			//Play intro theme after cutscene coroutine is queued, so music and graphics start together
			XMIMusic.LoadXMI(XMIMusic.IntroTheme);
			uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 13));//welcome message
		}
		else
		{
			cutsplayer.PlayCutscene(9, LaunchUWDemo);
		}
	}

	private static void LaunchUWDemo()
	{
		playerdat.InitEmptyPlayer();
		//get demo player files
		var filelist = Directory.GetFiles(Path.Combine(UWClass.BasePath, "DATA"), "DPLAYER.*");
		if (filelist.Length > 0)
		{
			var demopdat = filelist[Rng.r.Next(filelist.Length)];
			var pdata = File.ReadAllBytes(demopdat);
			playerdat.pdat = playerdat.EncryptDecryptUW1(pdata, pdata[0]);
			Array.Resize(ref playerdat.pdat, playerdat.InventoryPtr + 512 * 8);
		}
		else
		{
			playerdat.CharName = "DEMO";
			playerdat.max_hp = 30;
			playerdat.play_hp = 30;
			playerdat.STR = 20;
			playerdat.INT = 20;
			playerdat.DEX = 20;
		}

		uimanager.instance.JourneyOnwards("DATA");
		uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, 13));//welcome message
	}

	/// <summary>
	/// Draws a debug marker sprite on game load to show where the character is positioned
	/// </summary>
	/// <param name="gr"></param>
	public static void DrawPlayerPositionSprite(GRLoader gr)
	{
		int spriteNo = 127;
		var a_sprite = new MeshInstance3D(); //new Sprite3D();
		a_sprite.Name = "player";
		a_sprite.Mesh = new QuadMesh();
		Vector2 NewSize;
		var img = gr.LoadImageAt(spriteNo);
		if (img != null)
		{
			a_sprite.Mesh.SurfaceSetMaterial(0, gr.GetMaterial(spriteNo));
			NewSize = new Vector2(
					ArtLoader.SpriteScale * img.GetWidth(),
					ArtLoader.SpriteScale * img.GetHeight()
					);
			a_sprite.Mesh.Set("size", NewSize);
			Node3D worldobjects = instance.GetNode<Node3D>("/root/Underworld/worldobjects");
			worldobjects.AddChild(a_sprite);
			a_sprite.Position = cameraPitchGimbal_world.Position;
		}
	}

	public override void _Process(double delta)
	{
		WarpMouseStep();
		DigitalEffectPlayback.Update(DigitalAudioPlayer);
		base._Process(delta);
		if (uimanager.InGame)
		{
			var mat = (ShaderMaterial)uimanager.instance.uwviewport.Material;
			mat.SetShaderParameter("viewport_1", (Texture)uimanager.instance.uwsubviewport_world.GetTexture());
			mat.SetShaderParameter("viewport_2", (Texture)uimanager.instance.uwsubviewport_sprites.GetTexture());
			if (UWClass._RES == UWClass.GAME_UW2)
			{
				mat.SetShaderParameter("uwgame", (int)2);
			}
			else
			{
				mat.SetShaderParameter("uwgame", (int)1);
			}
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _PhysicsProcess(double delta)
	{
		if ((uimanager.InGame) || (uimanager.AtMainMenu) || (uimanager.CurrentGameMode == uimanager.GameModes.CUTSCENE))
		{
			PitTimer += delta;
			cycletime += delta;
			if (cycletime > 0.2)
			{
				cycletime = 0;
				PaletteLoader.UpdatePaletteCycles();
			}
			if (PitTimer >= 0.00391)
			{
				//simulation of the dos pit timer.
				//(the part of a tick left over is kept, so the clock runs at the 256 ticks a second of the original)
				var ticks = (uint)(PitTimer / 0.00391);
				GlobalPITTimer += ticks;
				PitTimer -= ticks * 0.00391;
				// if ((GlobalPITTimer % 32) == 0)
				// {
				if (uimanager.InGame)
				{
					uimanager.AnimateFlasks();
				}
				//}
			}
		}


		if (musicalinstrument.PlayingNote > 0)
		{
			playingnotetimer += delta;
			if (playingnotetimer >= 0.2f)
			{
				musicalinstrument.StopMusicalNote();
			}
		}
		// //DOS interupt 8
		// Pit += (delta*5);  //seem smoother		
		//GlobalPITTimer += delta;
		// if (Pit >= 0.054945) // DOS PIT Timer interupt 8 is 18.2 times a second
		// {
		// 	PitTimer++;   // (uint)(Pit / 0.054945);//This is probably all wrong. needs revisiting.
		// 	Pit = 0;
		// }

		//DOSBox seesm to indicate there is a 255hz timer that is incrementing GlobalPit. This would tally with the updating i've seen with that global. 
		// This would indicate that for a typical clock increment of ~ 0x19 (based on breakpoints in dosbox) the game is updating motion about every 0.097659 seconds.

		//Dosbox  PIT:PIT 0 Timer at 255.9927 Hz mode 3 


		//UGH

		testclock += delta;

		if ((uimanager.InGame) && (testclock >= 0.097659) && (!uimanager.blockinput))
		{
			testclock = 0;
			byte AnimationFrameDeltaIncrement = 0;

			var ClockIncrement = GlobalPITTimer - LastGlobalPitTimer;
			if ((ClockIncrement < 0) || (ClockIncrement > 0x40))
			{
				ClockIncrement = 0x40;
				AnimationFrameDeltaIncrement = 1;
				EasyMoveFrameIncrement += 4;
			}
			else
			{
				//Debug.Print($"{PitTimer - LastPitTimer}");
				EasyMoveFrameIncrement += (byte)((GlobalPITTimer >> 4) - (LastGlobalPitTimer >> 4));  //every 16 pits?
				AnimationFrameDeltaIncrement = (byte)((GlobalPITTimer >> 6) - (LastGlobalPitTimer >> 6));//every 63 pits? This controls how often NPCs and mobile objects move. It could stand to be faster.

				//check_physics: the time since the last round, at most 0x40 (the loop runs every 0.0977 s, about 0x19 ticks)
			}

			if ((ClockIncrement != 0) && (!uimanager.blockinput))
			{
				ProcessMotionInputs();
				if (AnimationFrameDeltaIncrement != 0)
				{
					AnimationOverlay.UpdateAnimationOverlays();
					timers.RunTimerTriggers(AnimationFrameDeltaIncrement);
				}
				playerdat.ClockValue += (int)ClockIncrement;
				LastGlobalPitTimer = GlobalPITTimer;
				AnimationFrameDeltaIncrement = EasyMoveFrameIncrement;
				if (playerdat.SpeedEnchantment)
				{//haste: the mobiles get half the frames and the odd one carries over (check_physics)
					AnimationFrameDeltaIncrement = (byte)(AnimationFrameDeltaIncrement >> 1);
					EasyMoveFrameIncrement = (byte)(EasyMoveFrameIncrement & 0x1);
				}
				else
				{
					EasyMoveFrameIncrement = 0;
				}

				GameObjectLoop(
					ClockIncrement: (byte)ClockIncrement,
					AnimationFrameDelta: AnimationFrameDeltaIncrement,
					EasyMove: false);

			}
		}

		if ((uimanager.InGame) && (!uimanager.blockinput))
		{
			combat.CombatInputHandler(delta);//may need to be moved outside this block
			playerdat.PlayerTimedLoop(delta);
			RefreshWorldState();//handles teleports, tile redraws	
		}

		//Other updates
		if (uimanager.InGame)
		{
			if (EnablePositionDebug)
			{
				var fps = Engine.GetFramesPerSecond();
				lblPositionDebug.Text = $"FPS:{fps} Time:{playerdat.game_time} PIT:{GlobalPITTimer}\nL:{playerdat.dungeon_level} X:{playerdat.playerObject.npc_xhome} Y:{playerdat.playerObject.npc_yhome}\nMouseposition:{uimanager.instance.uwviewport.GetLocalMousePosition()}\nPlayer Coordinates {motion.playerMotionParams.x_0} {motion.playerMotionParams.y_2} {motion.playerMotionParams.z_4}";
			}

			if ((MessageDisplay.WaitingForTypedInput) || (MessageDisplay.WaitingForYesOrNo))
			{
				if (!uimanager.instance.TypedInput.HasFocus())
				{
					uimanager.instance.TypedInput.GrabFocus();
				}
				uimanager.instance.scroll.UpdateMessageDisplay();
			}
		}
	}

	static void GameObjectLoop(byte ClockIncrement, byte AnimationFrameDelta, bool EasyMove)
	{
		//playerdat.play_hp = playerdat.max_hp;
		playerdat.CameraBobZAdjust_dseg_67d6_33CE = 0;
		motion.RelatedToClockIncrement_67d6_742 += ClockIncrement;
		playerdat.CameraIsBobbing_dseg_67d6_33c6 = false;

		if (
			(motion.MotionInputPressed != 0)
			||
			(motion.playerMotionParams.momentum_14 != 0)
			||
			(motion.playerMotionParams.unk_a_pitch != 0)
			||
			(motion.playerMotionParams.gravity_10_Z != 0)
			||
			(motion.playerMotionParams.unk_e_Y != 0)
			||
			(motion.playerMotionParams.unk_c_X != 0)
			||
			(motion.PlayerMotionUpdateRequired_dseg_D3 != false)
		)
		{
			if (EasyMove == false)
			{//(with the roaming sight the input is already zeroed, so only falling, currents and the like move the body)
				//when any forced movement or player input is not 0
				motion.PlayerMotion(ClockIncrement); //todo confirm increments
			}

		}
		if (playerdat.FreezeTimeEnchantment == false)
		{
			if (AnimationFrameDelta != 0)
			{
				ProcessMobileObjects(AnimationFrameDelta);
			}
		}
		if (playerdat.TileState != 0)
		{
			motion.WalkOnSurfaceType();
		}

		playerdat.ApplyPlayerSneakScore(EasyMove);

		playerdat.FootSteps(EasyMove);

		//Position player object now after all possible calcs have been completed
		playerdat.PositionPlayerCamera();
	}

	/// <summary>Whether the interaction icon may change now, as clicking one checks (not dead, nothing in hand, nothing being used or cast), and the options menu is not open.</summary>
	static bool CanChangeInteractionMode() =>
		playerdat.play_hp != 0 && playerdat.ObjectInHand == -1 && uimanager.UsageMode == 0
		&& uimanager.InteractionMode != uimanager.InteractionModes.ModeOptions;

	/// <summary>
	/// Classic controls' movement keys held down (do_player_keyboard), in the original's order: w forward fast, s
	/// forward slowly, a and d turn, z and c slide, x back, e and q up and down when levitating or flying (else stop).
	/// None with exactly one of Shift and Caps Lock on (the capitals are the step keys), or with Alt or Ctrl held.
	/// </summary>
	static void ClassicKeyboardMotion()
	{
		if ((Input.IsKeyPressed(Key.Shift) != CapsLockOn) || Input.IsKeyPressed(Key.Alt) || Input.IsKeyPressed(Key.Ctrl))
		{
			return;
		}
		var flying = (playerdat.MagicalMotionAbilities & 0x14) != 0;
		if (Controls.Held(Controls.Action.Forward)) { motion.PlayerMotionWalk_77C = 0x70; motion.MotionInputPressed = 1; }
		if (Controls.Held(Controls.Action.Walk)) { motion.PlayerMotionWalk_77C = 0x32; motion.MotionInputPressed = 1; }
		if (Controls.Held(Controls.Action.TurnLeft)) { motion.PlayerMotionHeading_77E = -90; motion.MotionInputPressed = 1; }
		if (Controls.Held(Controls.Action.TurnRight)) { motion.PlayerMotionHeading_77E = 90; motion.MotionInputPressed = 1; }
		if (Controls.Held(Controls.Action.StrafeLeft)) { motion.PlayerMotionWalk_77C = 0; motion.PlayerMotionHeading_77E = 0; motion.MotionInputPressed = 9; }
		if (Controls.Held(Controls.Action.StrafeRight)) { motion.PlayerMotionWalk_77C = 0; motion.PlayerMotionHeading_77E = 0; motion.MotionInputPressed = 0xA; }
		if (Controls.Held(Controls.Action.Back)) { motion.PlayerMotionWalk_77C = 0; motion.PlayerMotionHeading_77E = 0; motion.MotionInputPressed = 8; }
		if (Controls.Held(Controls.Action.FlyUp)) { motion.MotionInputPressed = (short)(flying ? 0xC : 0); }
		if (Controls.Held(Controls.Action.FlyDown)) { motion.MotionInputPressed = (short)(flying ? 0xD : 0); }
	}

	/// <summary>Whether the left button went down in the 3D view and is still held: classic controls move with it.</summary>
	public static bool MouseMoveHeld;

	/// <summary>
	/// Classic controls move with the left button held in the 3D view (parse_playin(-1)): the bottom fifth slides
	/// left, steps back or slides right by third; elsewhere the pointer's distance from the middle third turns
	/// (up to 0x80) and its height above two fifths of the view sets the forward speed (up to about 0x73). Both
	/// buttons jump (forward in the air or swimming), unless the weapon is drawn, when the right button attacks.
	/// </summary>
	static void ClassicMouseMotion()
	{
		if (!MouseMoveHeld || !Input.IsMouseButtonPressed(MouseButton.Left))
		{
			MouseMoveHeld &= Input.IsMouseButtonPressed(MouseButton.Left);
			return;
		}
		if (playerdat.RoamingSightEnchantment)
		{//player_mous_move: with the Wizard Eye the mouse moves the eye (move_cam(0)) instead of the body
			RoamingSightMouseMove();
			return;
		}
		if (Input.IsMouseButtonPressed(MouseButton.Right) && playerdat.play_drawn != 1)
		{
			motion.MotionInputPressed = 1;
			if ((playerdat.TileState != 1) && ((motion.playerMotionParams.tilestate25 & 0x10) == 0))
			{
				motion.MotionInputPressed = 7;
			}
			return;
		}
		var view = uimanager.instance.uwviewport;
		var p = view.GetLocalMousePosition();
		var inside = new Vector2(Math.Clamp(p.X, 0f, view.Size.X - 1), Math.Clamp(p.Y, 0f, view.Size.Y - 1));
		if (inside != p)
		{//mouse_constrain: while the button is held the pointer stays in the view
			instance.GetViewport().WarpMouse(view.GetGlobalTransformWithCanvas() * inside);
			p = inside;
		}
		var x = Math.Clamp(p.X / view.Size.X, 0f, 0.999f);
		var up = Math.Clamp(1f - p.Y / view.Size.Y, 0f, 1f);//the original counts y up from the bottom
		motion.PlayerMotionWalk_77C = 0;
		motion.PlayerMotionHeading_77E = 0;
		if (up < 0.2f)
		{
			motion.MotionInputPressed = new short[] { 9, 8, 10 }[(int)(x * 3)];
			return;
		}
		if (x < 1f / 3f)
		{
			motion.PlayerMotionHeading_77E = (short)-(int)((1f / 3f - x) * 0x180);
		}
		if (x > 2f / 3f)
		{
			motion.PlayerMotionHeading_77E = (short)(int)((x - 2f / 3f) * 0x180);
		}
		if (up > 0.4f)
		{
			motion.PlayerMotionWalk_77C = (short)(int)((up - 0.4f) * 0xC0);
		}
		motion.MotionInputPressed = 1;
	}

	/// <summary>
	/// move_cam(0): the pointer's third of the view across turns the eye left, not at all or right (0x400 a time), and its
	/// third up from the bottom steps it back, not at all or forward, within 0x180..0x3D80.
	/// </summary>
	static void RoamingSightMouseMove()
	{
		var view = uimanager.instance.uwviewport;
		var p = view.GetLocalMousePosition();
		var turn = (int)(Math.Clamp(p.X / view.Size.X, 0f, 0.999f) * 3);
		var step = (int)(Math.Clamp(1f - p.Y / view.Size.Y, 0f, 0.999f) * 3);//the original counts y up from the bottom
		playerdat.DoCameraH += (short)((turn - 1) << 0xA);
		if (step != 1)
		{
			short dx = 0; short dy = 0;
			motion.SomethingProjectileHeading_seg021_22FD_EAE((ushort)playerdat.DoCameraH, ref dx, ref dy);
			playerdat.DoCameraX = (short)Math.Clamp(playerdat.DoCameraX + (dx >> 8) * (step - 1), 0x180, 0x3D80);
			playerdat.DoCameraY = (short)Math.Clamp(playerdat.DoCameraY + (dy >> 8) * (step - 1), 0x180, 0x3D80);
		}
	}

	/// <summary>When the last step key was taken: one step at most every 0x18 ticks.</summary>
	static ulong lastStepMsec;
	/// <summary>
	/// player_simple_move, classic's capitals (PLAYER.C): A and D turn 45 degrees, S steps half a tile (not into another
	/// terrain or off a ledge), W takes that step unchecked, X steps a quarter tile back (simple_fizix). One step at most
	/// every 0x18 ticks.
	/// </summary>
	static void ClassicStepKey(Key key)
	{
		int turn;
		switch (key)
		{
			case Key.A: turn = -1; break;
			case Key.D: turn = 1; break;
			case Key.S: turn = 0; break;
			case Key.W: turn = 2; break;
			case Key.X: turn = -2; break;
			default: return;
		}
		var now = Time.GetTicksMsec();
		if (now - lastStepMsec < 0x18 * 1000 / 256)
		{
			return;
		}
		lastStepMsec = now;
		PlayerSimpleMove(turn);
	}

	/// <summary>
	/// player_simple_move: a step or a turn (simple_fizix), then one 0x40 round of the world with it (move_physics
	/// with easy set: the player's own physics is skipped, the step sounds and is heard).
	/// </summary>
	public static void PlayerSimpleMove(int turn)
	{
		if (!motion.SimpleFizix(turn))
		{
			return;
		}
		EasyMoveFrameIncrement += 4;
		motion.playerMotionParams.momentum_14 = 0;
		AnimationOverlay.UpdateAnimationOverlays();
		playerdat.ClockValue += 0x40;
		LastGlobalPitTimer = GlobalPITTimer;
		byte frames = EasyMoveFrameIncrement;
		if (playerdat.SpeedEnchantment)
		{
			frames >>= 1;
			EasyMoveFrameIncrement &= 1;
		}
		else
		{
			EasyMoveFrameIncrement = 0;
		}
		GameObjectLoop(ClockIncrement: 0x40, AnimationFrameDelta: frames, EasyMove: true);
	}

	/// <summary>
	/// Caps Lock, as far as the keys show it: Godot does not give the lock's state, but a letter comes as a capital
	/// exactly when one of Shift and Caps Lock is on, so each letter sets it again; the Caps Lock key itself flips it.
	/// </summary>
	static bool CapsLockOn;

	static void TrackCapsLock(InputEventKey key)
	{
		if (!key.Pressed || key.Echo)
		{
			return;
		}
		if (key.Keycode == Key.Capslock)
		{
			CapsLockOn = !CapsLockOn;
		}
		else if (char.IsLetter((char)key.Unicode) && (key.Unicode < 0x80))
		{
			CapsLockOn = char.IsUpper((char)key.Unicode) != key.ShiftPressed;
		}
	}

	/// <summary>Classic controls' pitch keys (chg_plyp): 1 and 3 step the pitch by 0x400 within 0x1000 either way, 2 levels it.</summary>
	static void ClassicPitchKey(Key key)
	{
		if (Controls.Is(key, Controls.Action.LookDown))
		{
			playerdat.PlayerCameraPitch_dseg_67d6_33D6 = (short)Math.Max(-4096, playerdat.PlayerCameraPitch_dseg_67d6_33D6 - 0x400);
		}
		else if (Controls.Is(key, Controls.Action.LookUp))
		{
			playerdat.PlayerCameraPitch_dseg_67d6_33D6 = (short)Math.Min(4096, playerdat.PlayerCameraPitch_dseg_67d6_33D6 + 0x400);
		}
		else if (Controls.Is(key, Controls.Action.LookCentre))
		{
			playerdat.PlayerCameraPitch_dseg_67d6_33D6 = 0;
		}
	}

	static void ProcessMotionInputs()
	{
		motion.PlayerMotionWalk_77C = 0;
		motion.PlayerMotionHeading_77E = 0;
		if (!Controls.Modern)
		{
			ClassicKeyboardMotion();
			ClassicMouseMotion();
		}
		else if (Controls.Held(Controls.Action.Forward))
		{
			if (Input.IsKeyPressed(Key.Shift))//forwards
			{
				//walk forwards
				motion.PlayerMotionWalk_77C = 0x32;
			}
			else
			{
				//run forwards
				motion.PlayerMotionWalk_77C = 0x70;
			}
			motion.MotionInputPressed = 1;
		}

		if (Controls.Modern)
		{
			if (Controls.Held(Controls.Action.TurnLeft))//turn left
			{
				motion.PlayerMotionHeading_77E = -90;//should this be scaled?
				motion.MotionInputPressed = 1;
			}
			if (Controls.Held(Controls.Action.TurnRight))//turn right
			{
				motion.PlayerMotionHeading_77E = +90;
				motion.MotionInputPressed = 1;
			}
			if (Controls.Held(Controls.Action.Back))//walk backwards
			{
				motion.PlayerMotionWalk_77C = 0;
				motion.PlayerMotionHeading_77E = 0;
				motion.MotionInputPressed = 8;
			}
			if (Controls.Held(Controls.Action.StrafeLeft))//slide left
			{
				motion.PlayerMotionWalk_77C = 0;
				motion.PlayerMotionHeading_77E = 0;
				motion.MotionInputPressed = 9;
			}
			if (Controls.Held(Controls.Action.StrafeRight))//slide right
			{
				motion.PlayerMotionWalk_77C = 0;
				motion.PlayerMotionHeading_77E = 0;
				motion.MotionInputPressed = 0xA;
			}
		}

		//Roaming Sight with the keys: UW2 moves the eye (parse_playin's move_cam(PlayerInput + 1)); UW1's keys still move the body
		if (playerdat.RoamingSightEnchantment && (UWClass._RES == UWClass.GAME_UW2))
		{
			var si = 0;
			var di = 0;
			bool movecamera = false;
			switch (motion.MotionInputPressed)
			{
				case 1://turning/walking
					{

						switch (motion.PlayerMotionHeading_77E)
						{
							case > 0:
								di = 2;
								break;
							case 0:
								di = 1;
								break;
							case < 0:
								di = 0;
								break;
						}

						if (motion.PlayerMotionWalk_77C > 0)
						{
							si = 2;
						}
						else
						{
							si = 1;
						}
						movecamera = true;
					}
					break;
				case 8://walk backwards
					{
						di = 1; si = 0;
						movecamera = true;
						break;
					}
				default://cancel input
					{
						motion.PlayerMotionWalk_77C = 0;
						motion.PlayerMotionHeading_77E = 0;
						motion.MotionInputPressed = 0;
						break;
					}
			}

			//apply changes to camera
			if (movecamera)
			{
				di--;
				playerdat.DoCameraH += (short)(di << 0xA);
				if (si != 1)
				{
					short x = 0; short y = 0;
					motion.SomethingProjectileHeading_seg021_22FD_EAE((ushort)playerdat.DoCameraH, ref x, ref y);
					playerdat.DoCameraX += (short)((x >> 8) * (si - 1));
					playerdat.DoCameraY += (short)((y >> 8) * (si - 1));
					playerdat.DoCameraX = (short)Math.Max((short)Math.Min((short)playerdat.DoCameraX, (short)0x3D80), (short)0x180);
					playerdat.DoCameraY = (short)Math.Max((short)Math.Min((short)playerdat.DoCameraY, (short)0x3D80), (short)0x180);
				}
			}
			//the keys went to the eye (move_cam, command = 0): the body gets no input but physics still acts on it
			motion.PlayerMotionWalk_77C = 0;
			motion.PlayerMotionHeading_77E = 0;
			motion.MotionInputPressed = 0;
			return;
		}


		if (Controls.Held(Controls.Action.Jump))//jump.
		{
			//a jump only from the ground and not swimming; in the air or in water the original turns it into
			//forward movement, so here the movement keys stay as they are (flying up is the fly up key)
			if ((playerdat.TileState != 1) && ((motion.playerMotionParams.tilestate25 & 0x10) == 0))
			{
				if (Input.IsKeyPressed(Key.Shift))
				{
					//long jump
					motion.MotionInputPressed = 6;
				}
				else
				{
					//jump
					motion.MotionInputPressed = 7;
				}
			}

		}
		if (Controls.Modern && Controls.Held(Controls.Action.FlyUp))//fly up (classic: ClassicKeyboardMotion)
		{
			if ((playerdat.MagicalMotionAbilities & 0x14) != 0)
			{
				motion.MotionInputPressed = 0xC;
			}
			else
			{
				motion.MotionInputPressed = 0;
			}
		}
		if (Controls.Modern && Controls.Held(Controls.Action.FlyDown))//fly down
		{
			if ((playerdat.MagicalMotionAbilities & 0x14) != 0)
			{
				motion.MotionInputPressed = 0xD;
			}
			else
			{
				motion.MotionInputPressed = 0;
			}
		}



		//Addition. capture look up and down (modern; classic steps once a key press, ClassicPitchKey)
		if (!Controls.Modern)
		{
			return;
		}
		if (Controls.Held(Controls.Action.LookDown))
		{
			//lookdown
			if (playerdat.PlayerCameraPitch_dseg_67d6_33D6 >= -4096)
			{
				playerdat.PlayerCameraPitch_dseg_67d6_33D6 = (short)Math.Max(-4096, playerdat.PlayerCameraPitch_dseg_67d6_33D6 - 0x400);
				//Debug.Print($"{playerdat.PlayerCameraPitch_dseg_67d6_33D6}");
			}
		}
		else if (Controls.Held(Controls.Action.LookUp))
		{
			//lookup
			if (playerdat.PlayerCameraPitch_dseg_67d6_33D6 <= 4096)
			{
				playerdat.PlayerCameraPitch_dseg_67d6_33D6 = (short)Math.Min(4096, playerdat.PlayerCameraPitch_dseg_67d6_33D6 + 0x400);
				//Debug.Print($"{playerdat.PlayerCameraPitch_dseg_67d6_33D6}");
			}
		}
		else if (Controls.Held(Controls.Action.LookCentre))
		{
			playerdat.PlayerCameraPitch_dseg_67d6_33D6 = 0;
			//Debug.Print($"{playerdat.PlayerCameraPitch_dseg_67d6_33D6}");
		}
	}

	static void ProcessMobileObjects(byte AnimationFrameDelta)
	{
		ThisFrameDelta = (byte)((PreviousFrameDelta + AnimationFrameDelta) & 0xF);
		for (int i = 0; i < UWTileMap.current_tilemap.NoOfActiveMobiles; i++)
		{
			var index = UWTileMap.current_tilemap.GetActiveMobileAtIndex(i);
			if ((index > 1) && (index < 256))
			{
				var obj = UWTileMap.current_tilemap.LevelObjects[index];
				//Loop update for a many times as the frame deltas require the object to be updated in this loop.
				var initialnextframe = obj.NextFrame_0XA_Bit0123;
				while (CheckIfUpdateNeeded(nextFrame: obj.NextFrame_0XA_Bit0123))
				{
					if (obj.majorclass == 1)
					{
						if (UWTileMap.ValidTile(obj.tileX, obj.tileY))
						{
							//This is an NPC on the map	
							var n = (npc)obj.instance;
							bool result;

							result = npc.NPCInitialProcess(critter: obj);


							if (n != null)
							{
								if (obj.instance != null)
								{
									var CalcedFacing = npc.CalculateFacingAngleToNPC(obj);
									n.SetAnimSprite(animationNo: obj.npc_animation, frameNo: obj.AnimationFrame, relativeHeading: CalcedFacing);
								}
							}
							if (result == false)
							{
								break;
							}
						}
						else
						{
							Debug.Print($"{obj.a_name} {obj.index} is off map");
						}
					}
					else
					{
						//This is a projectile
						if (motion.MotionProcessing(projectile: obj, SpecialMotionHandler: MotionHandler.ObjectMotionHandler) == false)
						{
							break;
						}
					}
					if (initialnextframe == obj.NextFrame_0XA_Bit0123)
					{
						obj.NextFrame_0XA_Bit0123 += 4; //hack. this will prevent an infinite loop occurring.
														//Debug.Print($"{obj.a_name} {obj.index} has bugged out in ProcessMobileObjects(), probably needs to be made static.");
					}

				}
			}
		}
		PreviousFrameDelta = ThisFrameDelta;
	}

	/// <summary>
	/// Checks if object/npc needs to move based on their nextFrame value and the current Animation Frame Delta
	/// </summary>
	/// <param name="nextFrame"></param>
	/// <param name="AnimationFrameDelta"></param>
	/// <returns></returns>
	static bool CheckIfUpdateNeeded(int nextFrame)
	{
		//if (AnimationFrameDelta)
		if (ThisFrameDelta > nextFrame)
		{
			if (nextFrame + 4 >= ThisFrameDelta)
			{
				return true;
			}
		}
		//seg007_17A2_357A
		if (ThisFrameDelta + 0x10 <= nextFrame)
		{
			return false;
		}
		if (PreviousFrameDelta > nextFrame)
		{
			return false;
		}
		if (PreviousFrameDelta <= ThisFrameDelta)
		{
			return false;
		}
		else
		{
			return true;
		}
	}

	/// <summary>
	/// wdialog's editing, for the line typed into the scroll: Ctrl+A/E start and end, Ctrl+B/F left and right, Ctrl+D
	/// deletes at the cursor, Ctrl+K cuts to the end; the first character typed replaces the offered text; a quantity
	/// takes only digits; at most 50 characters. Returns whether it dealt with the key (the line edit does the rest).
	/// </summary>
	static bool WdialogEditKey(InputEventKey key)
	{
		var edit = uimanager.instance.TypedInput;
		if (key.CtrlPressed)
		{
			var caret = edit.CaretColumn;
			switch (key.Keycode)
			{
				case Key.A: edit.CaretColumn = 0; break;
				case Key.E: edit.CaretColumn = edit.Text.Length; break;
				case Key.B: edit.CaretColumn = Math.Max(0, caret - 1); break;
				case Key.F: edit.CaretColumn = Math.Min(edit.Text.Length, caret + 1); break;
				case Key.D:
					if (caret < edit.Text.Length) { edit.Text = edit.Text.Remove(caret, 1); edit.CaretColumn = caret; }
					break;
				case Key.K: edit.Text = edit.Text.Substring(0, caret); edit.CaretColumn = caret; break;
				default: return false;
			}
			MessageDisplay.TypedInitialPending = false;
			return true;
		}
		var c = (char)key.Unicode;
		if (c < 0x20 || c > 0x7E)
		{
			if (key.Keycode != Key.Enter && key.Keycode != Key.Escape)
			{//moving about keeps the offered text
				MessageDisplay.TypedInitialPending = false;
			}
			return false;
		}
		if (MessageDisplay.TypedInitialPending)
		{
			edit.Text = "";
			edit.CaretColumn = 0;
			MessageDisplay.TypedInitialPending = false;
		}
		if (((MessageDisplay.QuantityPrompt > 0) && !char.IsAsciiDigit(c)) || (edit.Text.Length >= 50))
		{
			return true;//not taken
		}
		return false;
	}

	static SysFont noteFont;
	/// <summary>The width of a map note in the original's note font, FONT4X5P (string_width).</summary>
	static int NoteFontWidth(string text)
	{
		if (noteFont == null)
		{
			var path = System.IO.Path.Combine(UWClass.BasePath, "DATA", "FONT4X5P.SYS");
			noteFont = System.IO.File.Exists(path) ? SysFont.Parse(System.IO.File.ReadAllBytes(path)) : null;
			if (noteFont == null)
			{
				return 0;
			}
		}
		var width = 0;
		foreach (var ch in text)
		{
			width += noteFont.Covers(ch) ? noteFont.AdvanceOf(ch) : 0;
		}
		return width;
	}

	/// <summary>Alt+H (flip_bool on mouse_hand): the left and right buttons swapped.</summary>
	static bool MouseHandSwapped = false;
	/// <summary>Marks the button events this file sends itself (INS, DEL, the swap), so they are not taken again.</summary>
	const int SyntheticMouseDevice = 0x5EED;
	/// <summary>keyboard_mouse's warp: where the keypad sends the pointer (original 320x200, y up), the key, and the rate.</summary>
	static Vector2 warpTarget;
	static Key warpKey = Key.None;
	static int warpRate = 0;

	static Vector2 OriginalToViewport(float x, float yUp) => new Vector2(x * 4, (200 - yUp) * 4);

	/// <summary>
	/// The keyboard's mouse (MOUSE.C, PLAYER.C, UWEDIT.C): Tab and Shift+Tab move the pointer between the 3D view, the
	/// inventory and the bottom right; the keypad warps it toward an edge or corner, faster for each repeat; INS and
	/// DEL are the left and right buttons; Alt+H swaps the buttons; Alt+X leaves the game. Returns whether it took the event.
	/// </summary>
	bool KeyboardMouse(InputEvent @event)
	{
		if (Controls.Modern)
		{//classic only: modern keeps its own layout
			return false;
		}
		if (@event is InputEventMouseButton mb && MouseHandSwapped && mb.Device != SyntheticMouseDevice
			&& (mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Right))
		{
			var swapped = (InputEventMouseButton)mb.Duplicate();
			swapped.ButtonIndex = mb.ButtonIndex == MouseButton.Left ? MouseButton.Right : MouseButton.Left;
			swapped.Device = SyntheticMouseDevice;
			Input.ParseInputEvent(swapped);
			return true;
		}
		if (@event is not InputEventKey key || MessageDisplay.WaitingForTypedInput || uimanager.SaveDescriptionPromptActive)
		{
			return false;
		}
		if (!key.Pressed)
		{
			if (key.Keycode == warpKey)
			{
				warpKey = Key.None;
			}
			return false;
		}
		if (key.AltPressed && key.Keycode == Key.H && !key.Echo)
		{
			MouseHandSwapped = !MouseHandSwapped;
			return true;
		}
		if (key.AltPressed && key.Keycode == Key.X && !key.Echo)
		{//graceful_exit
			GetTree().Quit();
			return true;
		}
		if ((key.Keycode == Key.Insert || key.Keycode == Key.Delete) && !key.Echo)
		{//mouse_btns: INS the left button, DEL the right, pressed and let go where the pointer is
			var at = GetViewport().GetMousePosition();
			foreach (var pressed in new[] { true, false })
			{
				Input.ParseInputEvent(new InputEventMouseButton
				{
					ButtonIndex = key.Keycode == Key.Insert ? MouseButton.Left : MouseButton.Right,
					Pressed = pressed, Position = at, GlobalPosition = at, Device = SyntheticMouseDevice
				});
			}
			return true;
		}
		var mouse = GetViewport().GetMousePosition();
		var mx = mouse.X / 4; var my = 200 - mouse.Y / 4;
		if (key.Keycode == Key.Tab && !key.CtrlPressed && !key.AltPressed)
		{
			Vector2 to;
			if (!key.ShiftPressed)
			{
				to = mx < 0xE2 ? new Vector2(0x10E, 0x78) : my > 0x49 ? new Vector2(0x131, 7) : new Vector2(0x64, 0x82);
			}
			else
			{
				to = mx < 0xE2 ? new Vector2(0x131, 7) : my > 0x49 ? new Vector2(0x64, 0x82) : new Vector2(0x10E, 0x78);
			}
			Input.WarpMouse(GetTree().Root.GetFinalTransform() * OriginalToViewport(to.X, to.Y));
			return true;
		}
		Vector2? target = key.Keycode switch
		{
			Key.Kp4 => new Vector2(0, my),
			Key.Kp6 => new Vector2(0x13F, my),
			Key.Kp8 => new Vector2(mx, 0xC7),
			Key.Kp2 => new Vector2(mx, 0),
			Key.Kp7 => new Vector2(0, 0xC7),
			Key.Kp9 => new Vector2(0x13F, 0xC7),
			Key.Kp1 => new Vector2(0, 0),
			Key.Kp3 => new Vector2(0x13F, 0),
			_ => null,
		};
		if (target.HasValue)
		{//pressing the same key again speeds the warp up (to 40)
			warpRate = key.Keycode == warpKey ? Math.Min(40, warpRate + 1) : 1;
			warpKey = key.Keycode;
			warpTarget = target.Value;
			return true;
		}
		return false;
	}

	/// <summary>Moves the pointer on toward the keypad's target while its key is held (moveMouse).</summary>
	void WarpMouseStep()
	{
		if (warpKey == Key.None)
		{
			return;
		}
		var mouse = GetViewport().GetMousePosition();
		var now = new Vector2(mouse.X / 4, 200 - mouse.Y / 4);
		var to = warpTarget - now;
		if (to.Length() < 1)
		{
			warpKey = Key.None;
			return;
		}
		var step = to.Normalized() * Math.Min(to.Length(), warpRate);
		Input.WarpMouse(GetTree().Root.GetFinalTransform() * OriginalToViewport(now.X + step.X, now.Y + step.Y));
	}

	public override void _Input(InputEvent @event)
	{
		if (KeyboardMouse(@event))
		{
			GetViewport().SetInputAsHandled();
			return;
		}
		if (@event is InputEventKey menuKey && uimanager.instance.MainMenuKey(menuKey))
		{
			GetViewport().SetInputAsHandled();
			return;
		}
		if (@event is InputEventKey optionsKey && !optionsKey.Echo && uimanager.OptionsPanelKey(optionsKey))
		{//busywaiting_new_options
			GetViewport().SetInputAsHandled();
			return;
		}
		// The save description prompt takes Escape before anything else. SetInputAsHandled
		// keeps it away from the LineEdit, and the return keeps it away from the rest of
		// this method, which would otherwise treat it as closing a conversation or a scroll.
		if (uimanager.SaveDescriptionPromptActive
			&& @event is InputEventKey escKey
			&& escKey.Pressed
			&& !escKey.Echo
			&& escKey.Keycode == Key.Escape)
		{
			uimanager.CancelSaveDescription();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventMouseMotion look && Controls.MouseLookActive)
		{
			Controls.Look(look.ScreenRelative);//screen pixels, so the speed is the same at any window size
		}

		if ((@event is InputEventMouseButton wheel) && wheel.Pressed
			&& ((wheel.ButtonIndex == MouseButton.WheelUp) || (wheel.ButtonIndex == MouseButton.WheelDown))
			&& Controls.Modern && uimanager.InGame && !uimanager.blockinput && CanChangeInteractionMode()
			&& Controls.WheelStep(wheel.Factor))
		{//modern controls: the wheel steps through the interaction icons
			var next = Controls.NextMode(wheel.ButtonIndex == MouseButton.WheelDown ? 1 : -1);
			if (next == uimanager.InteractionModes.ModeNone)
			{
				uimanager.EnterInteractionModeNone();
			}
			else
			{
				uimanager.InteractionModeToggle(next);
			}
		}

		uimanager.ContinueLookDrag(@event);

		if ((@event is InputEventMouseButton eventMouseButton)
			&&
			((eventMouseButton.ButtonIndex == MouseButton.Left) || (eventMouseButton.ButtonIndex == MouseButton.Right)))
		{
			if (eventMouseButton.Pressed)
			{
				if (MessageDisplay.WaitingForMore)
				{
					//Debug.Print("End wait due to click");
					MessageDisplay.WaitingForMore = false;
					return; //don't process any more clicks here.
				}
				if (!uimanager.blockinput)
				{
					if (uimanager.IsMouseInViewPort())
					{
						if (!Controls.Modern && eventMouseButton.ButtonIndex == MouseButton.Left)
						{//classic: the left button moves (mous_in_3d)
							MouseMoveHeld = uimanager.InGame;
						}
						else if (!Controls.Modern && Input.IsMouseButtonPressed(MouseButton.Left)
							&& uimanager.InteractionMode != uimanager.InteractionModes.ModeAttack)
						{//both buttons are a jump, not a click
						}
						else
						{
							uimanager.ClickOnViewPort(eventMouseButton);
						}
					}

				}
			}
		}


		if ((!uimanager.blockinput) && (uimanager.InGame))
		{
			if (@event is InputEventKey keyinput)
			{
				if (keyinput.Pressed && !keyinput.Echo && Controls.Modern)
				{
					if (Controls.Is(keyinput.Keycode, Controls.Action.MouseLook))
					{
						Controls.ToggleMouseLook();
					}
					else if (Controls.Is(keyinput.Keycode, Controls.Action.Weapon) && CanChangeInteractionMode())
					{//draw the weapon, or put it away and go back to context clicks
						if (playerdat.play_drawn == 1)
						{
							uimanager.ToggleWeaponAnimationState(false);
							uimanager.EnterInteractionModeNone();
						}
						else
						{
							uimanager.InteractionModeToggle(uimanager.InteractionModes.ModeAttack);
						}
					}
				}
				TrackCapsLock(keyinput);
				if (keyinput.Pressed && !Controls.Modern && !keyinput.CtrlPressed && !keyinput.AltPressed)
				{//classic: the capitals step (Shift or Caps Lock, not both), and 1, 2, 3 tilt a step a press (repeats included)
					if (keyinput.ShiftPressed != CapsLockOn)
					{
						ClassicStepKey(keyinput.Keycode);
					}
					ClassicPitchKey(keyinput.Keycode);
				}
				if (keyinput.Pressed)
				{
					switch (keyinput.Keycode)
					{
						case Key.F1:
						case Key.F2:
						case Key.F3:
						case Key.F4:
						case Key.F5:
						case Key.F6:
							//the icons, in the order they stand on screen (deal_with_icons: not partway through an action)
							if (CanChangeInteractionMode())
							{
								var icon = Controls.FunctionKeyIcon((int)(keyinput.Keycode - Key.F1));
								if (icon == uimanager.InteractionModes.ModeOptions)
								{
									uimanager.InteractionModeToggle(icon);
								}
								else
								{
									uimanager.InteractionModeButtonPressed(icon);
								}
							}
							break;
						case Key.S:
						case Key.R:
						case Key.M:
						case Key.F:
						case Key.D:
						case Key.Q:
							if (keyinput.CtrlPressed && !keyinput.Echo)
							{//do_option_shortcut: save, restore, music, sound, detail, quit
								uimanager.OptionShortcut(keyinput.Keycode switch
								{
									Key.S => 0,
									Key.R => 1,
									Key.M => 2,
									Key.F => 3,
									Key.D => 4,
									_ => 6,
								});
							}
							break;
						case Key.F7://toggle panel
							if (keyinput.AltPressed)
							{
								printVersion();
							}
							else
							{
								uimanager.ChangePanels();
							}
							break;
						case Key.F8: //cast spell
							if (keyinput.AltPressed)
							{
								printPlayerLocation();
							}
							else
							{
								RunicMagic.CastRunicSpell();
							}
							break;
						case Key.F9://track skill
							tracking.DetectMonsters(8, playerdat.Track); break;
						case Key.F10: // make camp
							{
								//Try and find a bedroll in player inventory.
								var bedroll = objectsearch.FindMatchInFullObjectList(
									majorclass: 4, minorclass: 2, classindex: 1,
									objList: playerdat.InventoryObjects);
								if (bedroll != null)
								{
									sleep.Sleep(1);
								}
								else
								{
									sleep.Sleep(0);
								}
								break;
							}

						case Key.F11://toggle position label
							{
								EnablePositionDebug = !EnablePositionDebug;
								uimanager.EnableDisable(lblPositionDebug, EnablePositionDebug);
								break;
							}
						case Key.F12://screenshot
							{
								if (keyinput.AltPressed)
								{
									SaveScreenshot();
								}
								break;
							}
						case Key.Pagedown:
							{
								motion.MotionSingleStepEnabled = true;//for stepping through motion processing.
								break;
							}
						case Key.Apostrophe:
							if (OS.IsDebugBuild())
							{
								//debug builds only: give full mage abilities
								playerdat.max_mana = 60;
								playerdat.play_mana = 60;
								playerdat.Casting = 30;
								playerdat.ManaSkill = 30;
								playerdat.play_level = 16;
								for (int r = 0; r < 24; r++)
								{
									playerdat.SetRune(r, true);
								}
								playerdat.PlayerStatusUpdate();
							}
							break;
					}
				}
			}
		}

		if (ConversationVM.WaitingForInput
			&& ConversationVM.MenusActive//while a message has cleared the answers, the wait for them takes the input
			&& !uimanager.MessageScrollIsTemporary
			&& !MessageDisplay.WaitingForTypedInput)
		{
			switch (@event)
			{
				case InputEventMouseMotion:
					uimanager.instance.HighlightAnswerUnderMouse(uimanager.CursorOverMessageScroll);
					break;
				//Click to select options in conversation. Ensure we only allow left &right click to adhere to the original UW implementation. 
				case InputEventMouseButton mouseEvent:
					if (uimanager.CursorOverMessageScroll)
					{
						if (mouseEvent.Pressed && (mouseEvent.ButtonIndex == MouseButton.Left || mouseEvent.ButtonIndex == MouseButton.Right))
						{
							int result = uimanager.instance.HandleMessageScrollClick(mouseEvent);
							if ((result > 0) && (result <= ConversationVM.MaxAnswer))
							{
								ConversationVM.PlayerNumericAnswer = result;
								ConversationVM.WaitingForInput = false;
							}
							//GD.Print("Mouse Clicked in conversation");
						}
					}

					break;

				//Using keyboard numbers to select options in conversation
				case InputEventKey keyinput:
					{

						if (keyinput.Pressed)
						{
							if (int.TryParse(keyinput.AsText(), out int result))
							{
								if ((result > 0) && (result <= ConversationVM.MaxAnswer))
								{
									ConversationVM.PlayerNumericAnswer = result;
									ConversationVM.WaitingForInput = false;
								}
							}
						}

						break;
					}
			}
		}

		if (MessageDisplay.WaitingForMore)
		{//scroll_wait: a new key, not a release, a repeat or a modifier on its own (clicks are taken above)
			if (@event is InputEventKey keyinput && keyinput.Pressed && !keyinput.Echo
				&& keyinput.Keycode is not (Key.Shift or Key.Ctrl or Key.Alt or Key.Meta or Key.Capslock))
			{
				MessageDisplay.WaitingForMore = false;
			}
		}

		if ((MessageDisplay.WaitingForTypedInput) && (!chargen.ChargenWaitForInput))
		{//wdialog: Enter, Escape or a click ends it; Escape gives no answer and shows "-"
			string shown = null;
			if (@event is InputEventKey editKey && editKey.Pressed && WdialogEditKey(editKey))
			{
				GetViewport().SetInputAsHandled();
				return;
			}
			if (@event is InputEventKey keyinput && keyinput.Pressed)
			{
				switch (keyinput.Keycode)
				{
					case Key.Enter:
						shown = uimanager.instance.TypedInput.Text;
						break;
					case Key.Escape:
						uimanager.instance.TypedInput.Text = "";
						shown = "-";
						break;
				}
			}
			else if (@event is InputEventMouseButton typedClick && typedClick.Pressed
				&& (typedClick.ButtonIndex == MouseButton.Left || typedClick.ButtonIndex == MouseButton.Right))
			{
				if (MessageDisplay.QuantityPrompt > 0)
				{//AskHowMany: left one, right the whole stack, both none
					var both = Input.IsMouseButtonPressed(MouseButton.Left) && Input.IsMouseButtonPressed(MouseButton.Right);
					uimanager.instance.TypedInput.Text = both ? "" : typedClick.ButtonIndex == MouseButton.Left ? "1" : MessageDisplay.QuantityPrompt.ToString();
				}
				shown = uimanager.instance.TypedInput.Text;
			}
			if (shown != null)
			{//end typed input; the prompt and the answer stay on the scroll
				uimanager.instance.scroll.FreezeTypedInput(shown);
				MessageDisplay.QuantityPrompt = 0;
				MessageDisplay.WaitingForTypedInput = false;
			}
		}

		if ((MessageDisplay.WaitingForTypedInput) && (chargen.ChargenWaitForInput))
		{//handles character name input
			if (@event is InputEventKey keyinput)
			{
				if (keyinput.Pressed)
				{
					bool stop = false;
					//var keyin = keyinput.GetKeycodeWithModifiers();
					switch (keyinput.Keycode)
					{
						case Key.Enter:
							stop = true;
							break;
						case Key.Backspace:
							{
								var text = uimanager.instance.ChargenNameInput.Text;
								if (text.Length > 0)
								{
									text = text.Remove(text.Length - 1);
									uimanager.instance.ChargenNameInput.Text = text;
								}
								break;
							}
						case >= Key.Space and <= Key.Z:
							{
								string inputed;// = (char)keyinput.Unicode;
								if (Input.IsPhysicalKeyPressed(Key.Shift))
								{
									inputed = ((char)keyinput.Unicode).ToString().ToUpper();
								}
								else
								{
									inputed = ((char)keyinput.Unicode).ToString().ToLower();
								}
								var text = uimanager.instance.ChargenNameInput.Text;
								if (text.Length < 16)
								{
									text += inputed;
									uimanager.instance.ChargenNameInput.Text = text;
								}
								break;
							}
					}
					if (stop)
					{//end typed input						
						MessageDisplay.WaitingForTypedInput = false;
						chargen.ChargenWaitForInput = false;
					}
				}
			}
		}

		if (uimanager.CurrentAutomapAction == uimanager.automapactions.WRITING)
		{
			if (@event is InputEventKey keyinput)
			{
				if (keyinput.Pressed)
				{
					switch (keyinput.Keycode)
					{
						case Key.Enter:
							uimanager.StopWritingAutomapNote(false);
							break;
						case Key.Escape:
							// DOS commits on Escape exactly as on Enter: both jump to the
							// same routine, which copies the typed string into the note
							// (UW1_asm.asm:310176-310185, target ovr092_BA1 at :310359).
							// Nothing in the original cancels a note part way through.
							uimanager.StopWritingAutomapNote(false);
							break;
						case Key.Backspace:
							{
								var text = uimanager.currentmapnote.notetext;
								if (text.Length > 0)
								{
									text = text.Remove(text.Length - 1);
									uimanager.currentmapnote.notetext = text;
									uimanager.currentmapnote.textlabel.Text = $"[color=#331C13]{text}[/color]";
								}
								else if (UWClass._RES != UWClass.GAME_UW2)
								{//UW1 beeps at a character it will not take (do_beep(0x12C, 0xA))
									PcSpeaker.Beep(0x12C, 0xA);
								}
								break;
							}

						default:
							{
								// DOS accepts 0x20 to 0x7A only and upper-cases as it goes.
								// Appending the raw key let a note keep lower-case text, which
								// hangs UW1 when the automap is opened, and let a non-text key
								// append a NUL that reads back as an empty note.
								var c = automapnote.NormaliseNoteText(((char)keyinput.Unicode).ToString());
								if (c.Length == 0) break;

								var text = uimanager.currentmapnote.notetext;
								if ((text.Length < automapnote.MaxNoteLength) && (uimanager.currentmapnote.posX + NoteFontWidth(text + c) <= 0x13B))
								{//ManageDungeonMap: not past the map's right edge (x + width at most 0x13B in FONT4X5P)
									text += c;
									uimanager.currentmapnote.notetext = text;
									uimanager.currentmapnote.textlabel.Text = $"[color=#331C13]{text}[/color]";
								}
								else if (UWClass._RES != UWClass.GAME_UW2)
								{//UW1 beeps at a character it will not take (do_beep(0x12C, 0xA))
									PcSpeaker.Beep(0x12C, 0xA);
								}
								break;
							}
					}
				}
			}
		}

		if (musicalinstrument.PlayingInstrument)
		{
			if (@event is InputEventKey keyinput)
			{
				if (keyinput.Pressed)
				{
					switch (keyinput.Keycode)
					{
						case >= Key.Key0 and <= Key.Key9:
						case >= Key.Kp0 and <= Key.Kp9:
							musicalinstrument.PlayMusicalNote(keyinput);
							break;
						case Key.Escape:
							musicalinstrument.StopPlaying();
							break;
					}
				}
			}
		}

		if (MessageDisplay.WaitingForYesOrNo)
		{//wyorn: only Y and N change the answer; Enter, Escape (No) or a click ends it, the right button alone
		 //answering yes and the left or both no (the callers' wd_bool(r == 2))
			bool stop = false;
			if (@event is InputEventKey keyinput && keyinput.Pressed)
			{
				switch (keyinput.Keycode)
				{
					case Key.Enter:
						stop = true;
						break;
					case Key.Escape:
						stop = true;
						MessageDisplay.YesNoOption = "No";
						break;
					case Key.Y:
						MessageDisplay.YesNoOption = "Yes";
						break;
					case Key.N:
						MessageDisplay.YesNoOption = "No";
						break;
				}
			}
			else if (@event is InputEventMouseButton yesNoClick && yesNoClick.Pressed
				&& (yesNoClick.ButtonIndex == MouseButton.Left || yesNoClick.ButtonIndex == MouseButton.Right))
			{
				stop = true;
				MessageDisplay.YesNoOption = (yesNoClick.ButtonIndex == MouseButton.Right && !Input.IsMouseButtonPressed(MouseButton.Left)) ? "Yes" : "No";
			}
			uimanager.instance.TypedInput.Text = MessageDisplay.YesNoOption;
			if (stop)
			{//end the question; it and its answer stay on the scroll
				uimanager.instance.scroll.FreezeTypedInput(MessageDisplay.YesNoText(MessageDisplay.YesNoOption));
				MessageDisplay.WaitingForYesOrNo = false;
				MessageDisplay.YesNoOption = "";
				cameraPitchGimbal_world.Set("MOVE", true);//re-enable movement
			}
		}
	}

	/// <summary>
	/// Handles the end of chain events.
	/// </summary>
	public static void RefreshWorldState()
	{
		if (DoRedraw)
		{
			//update tile faces
			UWTileMap.SetTileMapWallFacesUW();
			//UWTileMap.current_tilemap.CleanUp();
			//Handle tile changes after all else is done
			foreach (var t in UWTileMap.current_tilemap.Tiles)
			{
				if (t.Redraw)
				{
					UWTileMap.RemoveTile(
						tileX: t.tileX,
						tileY: t.tileY,
						removeWall: (t.tileType >= 2 && t.tileType <= 5));
					tileMapRender.RenderTile(tileMapRender.worldnode, t.tileX, t.tileY, t);
					t.Redraw = false;
				}
			}
		}
		if (UWClass._RES == UWClass.GAME_UW2)
		{
			if (playerdat.dungeon_level == 5)
			{
				largeblackrockgem.CycleGemColours();
			}
		}

		//Handle level transitions now since it's possible for further traps to be called after the teleport trap
		Teleportation.HandleTeleportation();
	}

	/// <summary>
	/// game version.
	/// </summary>
	static void printVersion()
	{
		uimanager.AddToMessageScroll($"{GameStrings.GetString(1, GameStrings.GameName)}x.xx");
	}

	/// <summary>
	///  In vanilla UW pressing Alt+F8 will print the players dungeon,tileX and tileY as a string.
	/// </summary>
	static void printPlayerLocation()
	{
		uimanager.AddToMessageScroll($"{playerdat.dungeon_level.ToString("D2")}{playerdat.playerObject.npc_xhome.ToString("D2")}{playerdat.playerObject.npc_yhome.ToString("D2")}");
	}

	/// <summary>
	/// In vanilla uw pressing Alt+Q will save a screenshot. To avoid conflicts with movemnt this is changed to Alt+F12
	/// </summary>
	static void SaveScreenshot()
	{
		var basefolder = ProjectSettings.GlobalizePath("user://");
		var screenshotfolder = Path.Combine(basefolder, "screenshots");
		if (!Path.Exists(screenshotfolder))
		{
			System.IO.Directory.CreateDirectory(screenshotfolder);
		}
		var filename = Path.Combine(screenshotfolder, $"{DateTime.UtcNow.ToString("yyyyMMdd_hhmmss")}.png");
		uimanager.instance.GetViewport().GetTexture().GetImage().SavePng(filename);
	}

}//end class
