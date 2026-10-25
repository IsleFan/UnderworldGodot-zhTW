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
		{//Testing aid: play this cutscene straight away, to see its subtitles.
			uimanager.EnableDisable(uimanager.instance.uw1UI, false);
			uimanager.EnableDisable(uimanager.instance.uw2UI, false);
			uimanager.EnableDisable(uimanager.instance.PanelInventory, false);
			uimanager.EnableDisable(uimanager.instance.ManaFlaskPanel, false);
			uimanager.EnableDisable(uimanager.instance.HealthFlaskPanel, false);
			cutsplayer.PlayCutscene(testcuts, uimanager.ReturnToMainMenu);
			return;
		}
		if (OS.GetEnvironment("UW_TESTMENU") == "1")
		{//Testing aid: straight to the main menu, to see its buttons.
			uimanager.ReturnToMainMenu();
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
						if (i > 0) { look.LookAt(i, objs, true); }
					}
					uimanager.AddToMessageScroll(GameStrings.TextureDescription(1));
				};
			}
			if (int.TryParse(OS.GetEnvironment("UW_TESTOPTIONS"), out int testoptions))
			{//Testing aid: open the options once the level is loaded, or with another number only that picture (OptionButtonIndices), to see their lettering.
				instance.GetTree().CreateTimer(1.0).Timeout += () =>
				{
					uimanager.InteractionModeToggle(uimanager.InteractionModes.ModeOptions);
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
					("small blackrock gem", () => smallblackrockgem.LookAt(objs[weapon], objs)),
					("orb rock", () => OrbRock.Use(objs[weapon], false, null)),
					("food taste", () => Call(typeof(Food), "TasteDescriptionString", objs[weapon])),
					("trap disarmed", () => Call(typeof(trapdisarming), "DoTrapDisarm", objs, 100, objs[weapon], null, objs[weapon])),
					("mending spell", () => repair.MendingSpell(weapon, objs)),
					("vending sign", () => Call(typeof(a_hack_trap_vending), "VendingForSaleSign", plain)),
					("study monster", () => Call(typeof(SpellCasting), "StudyMonster", critter, objs)),
					("void creature", () => Call(typeof(npc), "EtheralVoidNPCDescription", objs[critter])),
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
			cutsplayer.PlayCutscene(9, uimanager.ReturnToMainMenu);
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
				GlobalPITTimer += (uint)(PitTimer / 0.00391);
				PitTimer = 0;
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

				ClockIncrement = 0x19;//ignore original calc since it's too slow. This value is choosen since dosbox debugger shows this value when breaking on motion code often (with some variation)
									  //Setting value too low prevents some motion in certain directions from working.
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
				{
					EasyMoveFrameIncrement = (byte)((AnimationFrameDeltaIncrement >> 1) & 0x1);
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
			if ((EasyMove == false) && (!playerdat.RoamingSightEnchantment))
			{
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

		playerdat.FootSteps();

		//Position player object now after all possible calcs have been completed
		playerdat.PositionPlayerCamera();
	}

	static void ProcessMotionInputs()
	{
		motion.PlayerMotionWalk_77C = 0;
		motion.PlayerMotionHeading_77E = 0;
		if (Input.IsKeyPressed(Key.W))
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

		if (Input.IsKeyPressed(Key.Q))//turn left
		{
			motion.PlayerMotionHeading_77E = -90;//should this be scaled?
			motion.MotionInputPressed = 1;
		}
		if (Input.IsKeyPressed(Key.E))//turn right
		{
			motion.PlayerMotionHeading_77E = +90;
			motion.MotionInputPressed = 1;
		}
		if (Input.IsKeyPressed(Key.S))//walk backwards
		{
			motion.PlayerMotionWalk_77C = 0;
			motion.PlayerMotionHeading_77E = 0;
			motion.MotionInputPressed = 8;
		}
		if (Input.IsKeyPressed(Key.A))//slide left
		{
			motion.PlayerMotionWalk_77C = 0;
			motion.PlayerMotionHeading_77E = 0;
			motion.MotionInputPressed = 9;
		}
		if (Input.IsKeyPressed(Key.D))//slide right
		{
			motion.PlayerMotionWalk_77C = 0;
			motion.PlayerMotionHeading_77E = 0;
			motion.MotionInputPressed = 0xA;
		}

		//Addition. put roaming sight input here and override other inputs.
		if (playerdat.RoamingSightEnchantment)
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
			return;
		}


		if (Input.IsKeyPressed(Key.J))//jump.
		{
			if (playerdat.TileState != 1)//ensure we are not swimming
			{
				if ((playerdat.MagicalMotionAbilities & 0x14) == 0)//check that player is not subject to levitate or fly spells
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
				else
				{
					motion.MotionInputPressed = 0xC; //translate the jump into a fly up
				}
			}

		}
		if (Input.IsKeyPressed(Key.R))//fly up
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
		if (Input.IsKeyPressed(Key.F))//fly down
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



		//Addition. capture look up and down
		if (Input.IsKeyPressed(Key.Key1))
		{
			//lookdown
			if (playerdat.PlayerCameraPitch_dseg_67d6_33D6 >= -4096)
			{
				playerdat.PlayerCameraPitch_dseg_67d6_33D6 = (short)Math.Max(-4096, playerdat.PlayerCameraPitch_dseg_67d6_33D6 - 0x400);
				//Debug.Print($"{playerdat.PlayerCameraPitch_dseg_67d6_33D6}");
			}
		}
		else if (Input.IsKeyPressed(Key.Key3))
		{
			//lookup
			if (playerdat.PlayerCameraPitch_dseg_67d6_33D6 <= 4096)
			{
				playerdat.PlayerCameraPitch_dseg_67d6_33D6 = (short)Math.Min(4096, playerdat.PlayerCameraPitch_dseg_67d6_33D6 + 0x400);
				//Debug.Print($"{playerdat.PlayerCameraPitch_dseg_67d6_33D6}");
			}
		}
		else if (Input.IsKeyPressed(Key.Key2))
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

	public override void _Input(InputEvent @event)
	{
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
						uimanager.ClickOnViewPort(eventMouseButton);
					}

				}
			}
		}


		if ((!uimanager.blockinput) && (uimanager.InGame))
		{
			if (@event is InputEventKey keyinput)
			{
				if (keyinput.Pressed)
				{
					switch (keyinput.Keycode)
					{
						case Key.F1: //open options menu
							uimanager.InteractionModeToggle(0); break;
						case Key.F2: //talk
							uimanager.InteractionModeButtonPressed((uimanager.InteractionModes)1); break;
						case Key.F3://pickup
							uimanager.InteractionModeButtonPressed((uimanager.InteractionModes)2); break;
						case Key.F4://look
							uimanager.InteractionModeButtonPressed((uimanager.InteractionModes)3); break;
						case Key.F5://attack
							uimanager.InteractionModeButtonPressed((uimanager.InteractionModes)4); break;
						case Key.F6://use
							uimanager.InteractionModeButtonPressed((uimanager.InteractionModes)5); break;
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
							{
								//give full mage abilities
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
								break;
							}
					}
				}
			}
		}

		if (ConversationVM.WaitingForInput
			&& !uimanager.MessageScrollIsTemporary
			&& !MessageDisplay.WaitingForTypedInput)
		{
			switch (@event)
			{
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
		{
			if (@event is InputEventKey keyinput)
			{
				Debug.Print("End wait due to key inputclick");
				MessageDisplay.WaitingForMore = false;
			}
		}

		if ((MessageDisplay.WaitingForTypedInput) && (!chargen.ChargenWaitForInput))
		{
			if (@event is InputEventKey keyinput)
			{
				if (keyinput.Pressed)
				{
					bool stop = false;
					switch (keyinput.Keycode)
					{
						case Key.Enter:
							stop = true;
							break;
						case Key.Escape:
							stop = true;
							uimanager.instance.TypedInput.Text = "";
							break;
					}
					if (stop)
					{//end typed input
						uimanager.instance.scroll.Clear();
						MessageDisplay.WaitingForTypedInput = false;
					}
				}
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
								if (text.Length < automapnote.MaxNoteLength)
								{
									text += c;
									uimanager.currentmapnote.notetext = text;
									uimanager.currentmapnote.textlabel.Text = $"[color=#331C13]{text}[/color]";
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
		{
			if (@event is InputEventKey keyinput)
			{
				if (keyinput.Pressed)
				{
					bool stop = false;
					switch (keyinput.Keycode)
					{
						case Key.Enter:
							stop = true;
							break;
						case Key.Escape:
							stop = true;
							uimanager.instance.TypedInput.Text = "No";
							MessageDisplay.YesNoOption = "No";
							break;
						case Key.Y:
							uimanager.instance.TypedInput.Text = "Yes";
							MessageDisplay.YesNoOption = "Yes";
							break;
						default:
							uimanager.instance.TypedInput.Text = "No";
							MessageDisplay.YesNoOption = "No";
							break;
					}
					if (stop)
					{//end typed input
						uimanager.instance.scroll.Clear();
						MessageDisplay.WaitingForYesOrNo = false;
						MessageDisplay.YesNoOption = "";
						cameraPitchGimbal_world.Set("MOVE", true);//re-enable movement
					}
				}
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
