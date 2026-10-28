using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Underworld;

// <summary>
// This script node backs-up the initial launch scene for the game, allowing
// the user to configure paths and pick between UW 1 & 2.
// </summary>
public partial class LaunchMenu : Control
{

	private const string NextScene = "res://scenes/Underworld.tscn";

	/// <summary>The cover of each game; clicking one starts it, as its Start button does.</summary>
	[Export]
	public TextureRect SelectUW1, SelectUW2;

	/// <summary>Each game's folder on one line, cut short with an ellipsis; the whole path is its tooltip.</summary>
	[Export]
	public Label PathUW1, PathUW2;

	[Export]
	public Button StartUW1, StartUW2, BrowseUW1, BrowseUW2, BrowseSynth, BrowseRom;

	/// <summary>Chooses a game folder. Native on Windows and macOS (use_native_dialog), like the other two.</summary>
	[Export]
	public FileDialog GameDirSelector;

	/// <summary>Chooses the SoundFont file.</summary>
	[Export]
	public FileDialog GameFilesSelector;

	[Export]
	public OptionButton SoundOptionSelector;

	[Export]
	public Label SynthPath;

	/// <summary>The SoundFont row, shown for the soundfont synth, and the ROM rows, shown for mt32 and cm32l.</summary>
	[Export]
	public Control SoundFontBox, RomBox;

	[Export]
	public Label TitleLabel, SubtitleLabel, SettingsTitle, SoundHint, SynthLabel;

	/// <summary>Makes the Pixel HD art set from the player's own game files (HdArtGenerator), and what it is doing.</summary>
	[Export]
	public Button HdGenerate;

	/// <summary>The scale to make: each item's metadata is one of HdArtGenerator.Scales.</summary>
	[Export]
	public OptionButton HdScale;

	[Export]
	public Label HdStatus;

	private bool _generating;

	/// <summary>Where GenerateHdArt makes the set instead of beside the program; only the UW_TESTHDGEN testing aid sets it.</summary>
	private string _hdRootOverride;

	/// <summary>
	/// "Original" followed by each HD art set found under hdartroot (see HdArt). Each set item's
	/// metadata is its folder name; the text is its name in ArtNames, or the folder name.
	/// </summary>
	[Export]
	public OptionButton ArtOptionSelector;

	/// <summary>How the launcher names the art sets it knows, by folder: English, Chinese, and the set's own name.</summary>
	private static readonly Dictionary<string, (string English, string Chinese, string Label)> ArtNames = new(StringComparer.OrdinalIgnoreCase)
	{
		{ HdArtGenerator.SetName(2), ("Pixel HD", "像素高清", "RotSprite2x") },
		{ HdArtGenerator.SetName(4), ("Pixel HD", "像素高清", "RotSprite4x") },
		{ HdArtGenerator.SetName(8), ("Pixel HD", "像素高清", "RotSprite8x") },
	};

	/// <summary>
	/// English followed by each translation found under Localization.Root. Each
	/// item's metadata is its language code.
	/// </summary>
	[Export]
	public OptionButton LanguageOptionSelector;

	/// <summary>
	/// Window sizes, all the 16:10 of the original 320x200, and full screen. Each item's metadata
	/// is the setting it writes.
	/// </summary>
	[Export]
	public OptionButton ResolutionOptionSelector;

	public const string Fullscreen = "fullscreen";
	private static readonly string[] Resolutions = { "1280x800", "1600x1000", "1920x1200", "2560x1600", Fullscreen };

	/// <summary>How the launcher names the translations it knows; others show their folder name.</summary>
	private static readonly Dictionary<string, string> LanguageNames = new()
	{
		{ "zh-TW", "繁體中文" },
	};

	/// <summary>
	/// Shows why the game could not be started, whether the installation was rejected or
	/// the scene failed to load. Carries no font override on purpose: the
	/// game's fonts are built from the player's own data, so the one moment this label is
	/// needed is the moment those fonts could not be loaded.
	/// </summary>
	[Export]
	public Label LaunchError;

	/// <summary>The title and the check of each game's folder, shown under its path.</summary>
	[Export]
	public Label PathUW1Label, PathUW1Status, PathUW2Label, PathUW2Status;

	/// <summary>The MT-32 ROM folder (uwsettings.RomDir), chosen with RomDirSelector, and whether it holds ROMs.</summary>
	[Export]
	public Label RomPath;

	[Export]
	public Label RomLabel, RomStatus;

	[Export]
	public FileDialog RomDirSelector;

	/// <summary>The options' titles, which follow the language setting with the rest (ApplyTexts).</summary>
	[Export]
	public Label SoundLabel, ArtLabel, LanguageLabel, ResolutionLabel;

	/// <summary>
	/// The control scheme (Controls): item metadata is uwsettings.controls. ControlsBox, shown for
	/// the modern one, holds mouse look's speed and direction, the keys and a summary of the scheme.
	/// </summary>
	[Export]
	public Label ControlsLabel, ControlsHint;

	[Export]
	public OptionButton ControlsOptionSelector;

	[Export]
	public Control ControlsBox;

	/// <summary>The button that drags objects, in both control schemes: the right (0) or the left (1).</summary>
	[Export]
	public Label DragLabel;

	[Export]
	public OptionButton DragOptionSelector;

	/// <summary>Mouse look speed 1 to 10 (item index + 1), and up and down normal (0) or inverted (1).</summary>
	[Export]
	public OptionButton Sensitivity, InvertMouse;

	[Export]
	public Button KeysButton;

	static readonly Color StatusGood = new(0.55f, 0.85f, 0.55f), StatusBad = new(1f, 0.45f, 0.4f);

	/// <summary>The game whose folder GameDirSelector is choosing.</summary>
	private byte _browsingGame;

	// Allows easy storage and retrieval of mouse mode overrides.
	private readonly Stack<Input.MouseModeEnum> _mouseModeHistory = new();

	// Only need one reference to the current settings.
	private readonly uwsettings _uwSettings = uwsettings.instance;

	public override void _Ready()
	{
		//Debug.Print($"_Ready fired");

		for (int i = 0; i < SoundOptionSelector.ItemCount; i++)
		{
			if (SoundOptionSelector.GetItemText(i) == _uwSettings.synth)
			{
				SoundOptionSelector.Selected = i;
				break;
			}
		}
		PopulateArtOptions();
		PopulateLanguageOptions();
		PopulateResolutionOptions();
		ApplyResolution(_uwSettings.resolution);
		FindGames();
		ApplyTexts();
		ShowSynthRows();
#if DEBUG
		//Testing aid: UW_TESTHDGEN=folder makes the Pixel HD set there as the button does (=default: where the
		//button would put it), prints the outcome and quits.
		var testHdGen = OS.GetEnvironment("UW_TESTHDGEN");
		if (!string.IsNullOrEmpty(testHdGen))
		{
			_hdRootOverride = testHdGen == "default" ? null : testHdGen;
			_uwSettings.hdartpath = "";
			if (int.TryParse(OS.GetEnvironment("UW_TESTHDGEN_SCALE"), out int testScale))
			{
				for (int i = 0; i < HdScale.ItemCount; i++)
				{
					if (HdScale.GetItemMetadata(i).AsInt32() == testScale) { HdScale.Selected = i; }
				}
			}
			Callable.From(GenerateHdArt).CallDeferred();
		}
		if (OS.GetEnvironment("UW_TESTKEYS") == "1")
		{//Testing aid: open the key list, to see it.
			Callable.From(ShowKeyBindings).CallDeferred();
		}
#endif

		// Start loading the main scene as soon as practicable. This happens before the focus
		// switch below, which returns early on an unrecognised selection: leaving the preload
		// after it meant that branch skipped the request, and a later valid launch would then
		// call LoadThreadedGet for a request that was never made.
		var error = ResourceLoader.LoadThreadedRequest(NextScene, "PackedScene", true);
		if (error != Error.Ok)
		{
			// Only on a genuine failure. Reporting unconditionally logged
			// "Ok while preloading main scene." on every normal startup.
			GD.PushError($"{Enum.GetName(error)} while preloading main scene.");
		}

		// Set the initial focus selection.
		switch (UWClass._RES)
		{
			case UWClass.GAME_UWDEMO:
			case UWClass.GAME_UW1:
				(StartUW1.Disabled ? (Control)SelectUW1 : StartUW1).GrabFocus();
				break;
			case UWClass.GAME_UW2:
				(StartUW2.Disabled ? (Control)SelectUW2 : StartUW2).GrabFocus();
				break;
			default:
				// Non-blocking at this point. Just notify and do no more.
				GD.PushError("Invalid game path selection: ", UWClass._RES);
				return;
		}

	}

	/// <summary>Shows <paramref name="dialog"/> with the ordinary cursor, which the game may have hidden.</summary>
	private void ShowDialog(FileDialog dialog)
	{
		_mouseModeHistory.Push(Input.MouseMode);
		Input.MouseMode = Input.MouseModeEnum.Visible;
		dialog.Show();
	}

	private void RestoreMouse()
	{
		if (_mouseModeHistory.Count > 0)
		{
			Input.MouseMode = _mouseModeHistory.Pop();
		}
	}

	public void OnDialogCanceled() => RestoreMouse();

	/// <summary>Chooses the folder of game <paramref name="selection"/> (1 UW1, 2 UW2).</summary>
	public void BrowseGameFolder(int selection)
	{
		_browsingGame = (byte)selection;
		var current = selection == UWClass.GAME_UW2 ? _uwSettings.pathuw2 : _uwSettings.pathuw1;
		GameDirSelector.Title = selection == UWClass.GAME_UW2
			? T("Choose the Ultima Underworld II folder (with UW2.EXE)", "選擇《Ultima Underworld II》的遊戲目錄（有 UW2.EXE 的資料夾）")
			: T("Choose the Ultima Underworld folder (with UW.EXE)", "選擇《Ultima Underworld》的遊戲目錄（有 UW.EXE 的資料夾）");
		if (Directory.Exists(current))
		{
			GameDirSelector.CurrentDir = current;
		}
		ShowDialog(GameDirSelector);
	}

	public void OnGameDirSelected(string dir)
	{
		if (_browsingGame == UWClass.GAME_UW2)
		{
			_uwSettings.pathuw2 = dir;
		}
		else
		{
			_uwSettings.pathuw1 = dir;
		}
		_uwSettings.Save();
		ShowPaths();
		ShowPathStatus();
		RestoreMouse();
	}

	public void BrowseSoundFont()
	{
		var current = _uwSettings.synthpath;
		if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(Path.GetDirectoryName(current)))
		{
			GameFilesSelector.CurrentDir = Path.GetDirectoryName(current);
		}
		ShowDialog(GameFilesSelector);
	}

	public void OnGameFilesSelectorSubmitted(string path)
	{
		// The SoundFont file itself, which MeltySynthEngine looks for, not its folder.
		if (File.Exists(path))
		{
			_uwSettings.synthpath = path;
			_uwSettings.Save();
			ShowPaths();
		}
		RestoreMouse();
	}

	public void BrowseRomFolder()
	{
		if (Directory.Exists(_uwSettings.RomDir()))
		{
			RomDirSelector.CurrentDir = _uwSettings.RomDir();
		}
		ShowDialog(RomDirSelector);
	}

	public void OnRomDirSelected(string dir)
	{
		_uwSettings.rompath = dir;
		_uwSettings.Save();
		ShowPaths();
		ShowPathStatus();
		RestoreMouse();
	}

	/// <summary>The folders and files the settings name, each on one line with the whole path as its tooltip.</summary>
	private void ShowPaths()
	{
		static void Show(Label label, string path, string empty)
		{
			label.Text = string.IsNullOrWhiteSpace(path) ? empty : path;
			label.TooltipText = path ?? "";
		}
		Show(PathUW1, _uwSettings.pathuw1, T("(not set)", "（尚未設定）"));
		Show(PathUW2, _uwSettings.pathuw2, T("(not set)", "（尚未設定）"));
		Show(SynthPath, _uwSettings.synthpath, T("The bundled SoundFont", "內建的 SoundFont"));
		Show(RomPath, _uwSettings.RomDir(), T("(not set)", "（尚未設定）"));
	}

	/// <summary>Shows the SoundFont row for the soundfont synth and the ROM rows for mt32 and cm32l.</summary>
	private void ShowSynthRows()
	{
		var synth = _uwSettings.synth ?? "";
		SoundFontBox.Visible = synth == "soundfont";
		RomBox.Visible = synth == "mt32" || synth == "cm32l";
	}

	/// <summary>The launcher's own words in the language the settings name (Chinese for zh-TW), else English.</summary>
	private string T(string english, string chinese) => _uwSettings.language == ZhText.Language ? chinese : english;

	/// <summary>Writes every caption in the chosen language, and the folder checks with them.</summary>
	private void ApplyTexts()
	{
		SubtitleLabel.Text = T("Choose a game, check the settings on the right, then start.", "創世紀地下世界．選擇遊戲、確認右邊的設定，再按「開始遊戲」。");
		SettingsTitle.Text = T("Settings", "設定");
		LanguageLabel.Text = T("Language", "語言");
		ResolutionLabel.Text = T("Window", "視窗大小");
		ControlsLabel.Text = T("Controls", "操作");
		PopulateControlsOptions();
		DragLabel.Text = T("Drag objects", "拖曳物品");
		DragOptionSelector.Clear();
		DragOptionSelector.AddItem(T("Right button", "右鍵拖曳"));
		DragOptionSelector.AddItem(T("Left button", "左鍵拖曳"));
		DragOptionSelector.Selected = _uwSettings.dragwithleft ? 1 : 0;
		Sensitivity.Clear();
		for (int speed = 1; speed <= 10; speed++)
		{
			Sensitivity.AddItem(T($"Mouse speed {speed}", $"滑鼠速度 {speed}"));
		}
		Sensitivity.Selected = Math.Clamp(_uwSettings.mousesensitivity, 1, 10) - 1;
		InvertMouse.Clear();
		InvertMouse.AddItem(T("Mouse up = look up", "上下正常"));
		InvertMouse.AddItem(T("Mouse up = look down", "上下反轉"));
		InvertMouse.Selected = _uwSettings.invertmouse ? 1 : 0;
		KeysButton.Text = T("Keys…", "按鍵設定…");
		ArtLabel.Text = T("Art", "美術");
		SoundLabel.Text = T("Music", "配樂");
		SoundHint.Text = T("Sound effects always use the Sound Blaster sounds.", "音效一律使用 Sound Blaster 的音效。");
		SynthLabel.Text = T("SoundFont (.sf2)", "SoundFont（.sf2）");
		RomLabel.Text = T("MT-32 / CM-32L ROM folder", "MT-32／CM-32L ROM 目錄");
		PathUW1Label.Text = T("Game folder", "遊戲目錄");
		PathUW2Label.Text = PathUW1Label.Text;
		foreach (var browse in new[] { BrowseUW1, BrowseUW2, BrowseSynth, BrowseRom })
		{
			browse.Text = T("Browse…", "瀏覽…");
		}
		HdGenerate.Text = T("Make Pixel HD art", "產生像素高清素材");
		int chosen = HdScale.ItemCount > 0 ? HdScale.GetItemMetadata(HdScale.Selected).AsInt32() : 4;
		HdScale.Clear();
		foreach (var scale in HdArtGenerator.Scales)
		{
			HdScale.AddItem(T($"{scale}x", $"{scale} 倍"));
			HdScale.SetItemMetadata(HdScale.ItemCount - 1, scale);
			if (scale == chosen) { HdScale.Selected = HdScale.ItemCount - 1; }
		}
		if (!_generating)
		{
			HdStatus.Text = HdHint();
		}
		StartUW1.Text = T("Start", "開始遊戲");
		StartUW2.Text = StartUW1.Text;
		GameFilesSelector.Title = T("Choose a SoundFont", "選擇 SoundFont");
		RomDirSelector.Title = T("Choose the MT-32 ROM folder", "選擇 MT-32 ROM 目錄");
		ShowPaths();
		ShowPathStatus();
	}

	/// <summary>Checks each game folder the way launching does, and the ROM folder for a ROM pair.</summary>
	private void ShowPathStatus()
	{
		bool uw1 = ShowGameStatus(PathUW1Status, UWClass.GAME_UW1, _uwSettings.pathuw1);
		bool uw2 = ShowGameStatus(PathUW2Status, UWClass.GAME_UW2, _uwSettings.pathuw2);
		//the Pixel HD art is made for the games found, so with neither there is nothing to make it from
		HdGenerate.Disabled = _generating || _extracting > 0 || !(uw1 || uw2);
		if (!_generating)
		{
			if (!uw1 && !uw2) { HdStatus.Text = HdNoGame(); }
			else if (HdStatus.Text == HdNoGame()) { HdStatus.Text = HdHint(); }
		}
		bool roms = HasRoms(_uwSettings.RomDir());
		ShowStatus(RomStatus, roms, roms
			? T("MT-32 ROMs found", "已找到 MT-32 ROM")
			: T("No MT-32 or CM-32L ROMs here", "這裡找不到 MT-32 或 CM-32L ROM"));
	}

	/// <summary>
	/// Whether <paramref name="dir"/> holds a control ROM and a PCM ROM, by the names Munt.NET's
	/// Mt32EmuEngine looks for (MT32_CONTROL.ROM, CM32L_PCM.ROM, MAME's cm32l_ctrl_1_02.rom...).
	/// Its own probe is not public.
	/// </summary>
	private static bool HasRoms(string dir)
	{
		if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
		{
			return false;
		}
		bool control = false, pcm = false;
		foreach (var file in Directory.GetFiles(dir, "*.rom", new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive }))
		{
			var name = Path.GetFileName(file).ToLowerInvariant();
			control |= name.Contains("control") || name.Contains("ctrl");
			pcm |= name.Contains("pcm");
		}
		return control && pcm;
	}

	/// <summary>Shows whether game <paramref name="res"/> is at <paramref name="path"/>, lets its Start button start it only if so, and says which.</summary>
	private bool ShowGameStatus(Label status, byte res, string path)
	{
		bool found = false;
		if (_extracting > 0 && ExtractingGame(res))
		{
			ShowStatus(status, true, T("Copying the game out of the GOG installation...", "正在從 GOG 版取出遊戲檔案…"));
		}
		else if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
		{
			ShowStatus(status, false, T("No game folder found: install it from GOG, or choose it with Browse", "沒有找到遊戲目錄，請安裝 GOG 版或按「瀏覽…」選擇"));
		}
		else if (!InstallationValidator.TryValidate(res, path, out string reason))
		{
			ShowStatus(status, false, T("Not a usable game folder: ", "遊戲目錄無效：") + reason);
		}
		else
		{
			ShowStatus(status, true, T("Game found", "已找到遊戲"));
			found = true;
		}
		(res == UWClass.GAME_UW2 ? StartUW2 : StartUW1).Disabled = !found || _generating || ExtractingGame(res);
		return found;
	}

	private string HdHint() => T("Made on this computer from your own game files, in about a minute (8x takes about 1 GB).",
		"用你自己的遊戲檔案在這台電腦產生，大約一分鐘（8 倍約佔 1 GB）。");
	private string HdNoGame() => T("No game folder found, so there is nothing to make the art from.", "沒有找到遊戲目錄，無法產生高清素材。");

	/// <summary>How many games are being copied out of a GOG image, and which.</summary>
	private int _extracting;
	private readonly HashSet<byte> _extractingGames = new();
	private bool ExtractingGame(byte res) => _extractingGames.Contains(res);

	/// <summary>
	/// For each game whose folder in the settings is not usable, looks for one (GameFinder): beside
	/// the program, where the one-click install puts it, or in a GOG installation. When only GOG's
	/// CD image is there, the game is copied out of it beside the program (into the user's data
	/// folder when that cannot be written), in the background, and the folder is set once done.
	/// A usable folder in the settings is never replaced.
	/// </summary>
	private void FindGames()
	{
		var programDir = AppPaths.ProgramDir();
		foreach (var (game, path) in new[] { (GameFinder.Uw1, _uwSettings.pathuw1), (GameFinder.Uw2, _uwSettings.pathuw2) })
		{
			if (GameFinder.Usable(game, path))
			{
				continue;
			}
			var found = GameFinder.FindFolder(game, programDir);
			if (found != null)
			{
				SetGamePath(game, found);
				continue;
			}
			var image = GameFinder.FindGogImage(game);
			if (image == null)
			{
				continue;
			}
			var dest = Path.Combine(programDir, game.Folder);
			if (!CanWriteGame(dest))
			{
				dest = Path.Combine(OS.GetUserDataDir(), game.Folder);
			}
			_extracting++;
			_extractingGames.Add(game.Res);
			Task.Run(() => GameFinder.Extract(image, game, dest))
				.ContinueWith(task => Callable.From(() =>
				{
					_extracting--;
					_extractingGames.Remove(game.Res);
					if (task.Exception == null && GameFinder.Usable(game, dest))
					{
						SetGamePath(game, dest);
					}
					else
					{
						GD.PushError($"Copying {game.DiscFolder} out of {image} failed: {task.Exception?.InnerException?.Message}");
					}
					ShowPaths();
					ShowPathStatus();
				}).CallDeferred());
		}
	}

	private void SetGamePath(GameFinder.Game game, string dir)
	{
		if (game.Res == UWClass.GAME_UW2) { _uwSettings.pathuw2 = dir; } else { _uwSettings.pathuw1 = dir; }
		_uwSettings.Save();
	}

	/// <summary>Whether a game can be copied to <paramref name="dest"/>: its parent can be written and it is missing or empty (it may hold saves).</summary>
	private static bool CanWriteGame(string dest)
	{
		if (Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any()) { return false; }
		var parent = Path.GetDirectoryName(dest);
		try
		{
			Directory.CreateDirectory(parent);
			var probe = Path.Combine(parent, ".uw-write-test");
			File.WriteAllText(probe, "");
			File.Delete(probe);
			return true;
		}
		catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
		{
			return false;
		}
	}

	private static void ShowStatus(Label status, bool good, string text)
	{
		status.Text = (good ? "✓ " : "✗ ") + text;
		status.AddThemeColorOverride("font_color", good ? StatusGood : StatusBad);
	}

	/// <summary>A click on a cover starts its game.</summary>
	public void OnLaunchInput(InputEvent @event, int selection)
	{
		if (@event is InputEventMouseButton { Pressed: true })
		{
			Launch(selection);
		}
	}

	/// <summary>Starts game <paramref name="selection"/> (1 UW1, 2 UW2), or says why it cannot.</summary>
	public void Launch(int selection)
	{
		// Update settings and the current state.
		UWClass._RES = (byte)selection;
		switch (UWClass._RES)
		{
			case UWClass.GAME_UWDEMO:
			case UWClass.GAME_UW1:
				if (File.Exists(System.IO.Path.Combine(_uwSettings.pathuw1, "UWDEMO.EXE")))
				{
					Debug.Print("UWDemo has been selected.");
					_uwSettings.gametoload = "UWDEMO";
					UWClass._RES = UWClass.GAME_UWDEMO;
				}
				else
				{
					_uwSettings.gametoload = "UW1";
				}
				UWClass.BasePath = _uwSettings.pathuw1;
				break;
			case UWClass.GAME_UW2:
				UWClass.BasePath = _uwSettings.pathuw2;
				_uwSettings.gametoload = "UW2";
				break;
			default:
				// Non-blocking at this point. Just notify and do no more.
				GD.PushError("Invalid game path selection: ", selection);
				return;
		}

		// Check the selected installation here rather than after the transition, because
		// the game scene cannot display a legible message about fonts that failed to load.
		// TryValidate no longer refuses the demo outright: it checks the demo's own fonts.
		if (!InstallationValidator.TryValidate(UWClass._RES, UWClass.BasePath, out string reason))
		{
			GD.PushError("Cannot start: " + reason);
			if (LaunchError != null) LaunchError.Text = T("", "遊戲目錄無效：") + reason;
			return;
		}
		if (LaunchError != null) LaunchError.Text = "";

		// Save only once the installation selection is known to validate. Saving first
		// would persist a configuration that was rejected a line later. A scene that then
		// fails to load does not invalidate the selection, so this stays before the handoff.
		_uwSettings.Save();

		// Switch scenes to start the game.
		//
		// LoadThreadedRequest only starts the load. It returns Ok even for a path that does
		// not exist, so a scene that cannot be loaded says nothing until here, where the get
		// waits if it has to and hands back an empty result. Casting that gives null, and
		// ChangeSceneToPacked(null) returns ErrInvalidParameter, which used to be discarded
		// and left the player looking at the launcher with no explanation.
		//
		// `as` rather than a hard cast on purpose: a resource of the wrong type then takes
		// the same reported path instead of throwing.
		var scene = ResourceLoader.LoadThreadedGet(NextScene) as PackedScene;
		if (scene == null)
		{
			HandleLaunchFailure(T("The game could not be loaded.", "無法載入遊戲。"));
			return;
		}

		var change = GetTree().ChangeSceneToPacked(scene);
		if (change != Error.Ok)
		{
			HandleLaunchFailure(T("The game could not be started", "無法啟動遊戲") + $" ({Enum.GetName(change)}).");
		}
	}

	/// <summary>
	/// Puts a failure where the player will see it, not only in the log, the way a rejected
	/// installation is already reported, and rearms the preload. Named Handle rather than
	/// Report because that second part is a state change, not just a message.
	///
	/// Also reissues the preload. LoadThreadedGet consumes the request whether or not it
	/// succeeded, so without this a second attempt would find nothing outstanding and fail
	/// with InvalidResource rather than retrying properly.
	///
	/// The message deliberately carries no ThreadLoadStatus. That status is a snapshot taken
	/// before the get, and the get can wait for a load that then fails, so quoting it can
	/// tell the player the load is still in progress when it has already failed. Godot logs
	/// the real reason itself.
	/// </summary>
	private void HandleLaunchFailure(string reason)
	{
		GD.PushError(reason);
		if (LaunchError != null) { LaunchError.Text = reason; }

		var rearm = ResourceLoader.LoadThreadedRequest(NextScene, "PackedScene", true);
		if (rearm != Error.Ok)
		{
			GD.PushError($"{Enum.GetName(rearm)} while rearming the preload, so a retry will not work.");
		}
	}

	/// <summary>
	/// Makes the Pixel HD set for every game whose folder checks out, in the art root (made under the
	/// user data folder when there is none). The pictures are made off the main thread into a
	/// hidden folder, the fonts then on the main thread (they need Godot), and only a finished
	/// game replaces the one before it. Art then switches to the new set.
	/// </summary>
	public void GenerateHdArt()
	{
		if (_generating)
		{
			return;
		}
		var games = new List<(string Dir, string Game)>();
		if (InstallationValidator.TryValidate(UWClass.GAME_UW1, _uwSettings.pathuw1, out _)) { games.Add((_uwSettings.pathuw1, "uw1")); }
		if (InstallationValidator.TryValidate(UWClass.GAME_UW2, _uwSettings.pathuw2, out _)) { games.Add((_uwSettings.pathuw2, "uw2")); }
		if (games.Count == 0)
		{
			HdStatus.Text = T("Choose a game folder first.", "請先在遊戲卡片選好遊戲目錄。");
			return;
		}
		string root = _hdRootOverride ?? uwsettings.DefaultHdArtRoot();
		if (!CanWrite(root))
		{//beside the program is read only (an Applications or Program Files folder, a macOS app run from its download): the user's data folder
			root = UserHdArtRoot();
		}
		int scale = HdScale.GetItemMetadata(HdScale.Selected).AsInt32();
		string work = Path.Combine(root, "." + HdArtGenerator.SetName(scale) + "-new");
		SetGenerating(true);
		Task.Run(() =>
		{
			if (Directory.Exists(work)) { Directory.Delete(work, true); }
			foreach (var (dir, game) in games)
			{
				HdArtGenerator.Generate(dir, game, work, scale, (kind, done) =>
				{
					if (done % 100 == 0) { Callable.From(() => ShowHdProgress(game, done)).CallDeferred(); }
				});
			}
		}).ContinueWith(task => Callable.From(() => FinishHdArt(task.Exception, games, root, work, scale)).CallDeferred());
	}

	/// <summary>Whether files can be made in <paramref name="dir"/> (made if missing). Godot is kept out of it with a .gdignore.</summary>
	private static bool CanWrite(string dir)
	{
		try
		{
			Directory.CreateDirectory(dir);
			File.WriteAllText(Path.Combine(dir, ".gdignore"), "");
			return true;
		}
		catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
		{
			return false;
		}
	}

	private void ShowHdProgress(string game, int done)
	{
		HdStatus.Text = T($"Making {game.ToUpperInvariant()}: {done} pictures done...", $"正在產生 {game.ToUpperInvariant()}：已完成 {done} 張…");
	}

	private void FinishHdArt(AggregateException error, List<(string Dir, string Game)> games, string root, string work, int scale)
	{
		try
		{
			if (error != null)
			{
				throw error.InnerException ?? error;
			}
			string set = Path.Combine(root, HdArtGenerator.SetName(scale));
			foreach (var (dir, game) in games)
			{
				HdArtFonts.Generate(dir, game, work, scale);
				var dest = Path.Combine(set, game);
				if (Directory.Exists(dest)) { Directory.Delete(dest, true); }
				Directory.CreateDirectory(set);
				Directory.Move(Path.Combine(work, game), dest);
			}
			_uwSettings.hdartpath = uwsettings.ToProgramRelative(set);
			_uwSettings.Save();
			PopulateArtOptions();
			HdStatus.Text = T($"Pixel HD art ({scale}x) is ready and chosen above.", $"像素高清素材（{scale} 倍）已產生完成，美術已切換過去。");
		}
		catch (Exception e)
		{
			GD.PushError($"Making the Pixel HD art failed: {e}");
			HdStatus.Text = T("Making the art failed: ", "產生素材失敗：") + e.Message;
		}
		finally
		{
			try { if (Directory.Exists(work)) { Directory.Delete(work, true); } } catch (IOException) { }
			SetGenerating(false);
#if DEBUG
			if (!string.IsNullOrEmpty(OS.GetEnvironment("UW_TESTHDGEN")))
			{
				GD.Print($"UW_TESTHDGEN {HdStatus.Text}");
				GetTree().Quit();
			}
#endif
		}
	}

	private void SetGenerating(bool on)
	{
		_generating = on;
		HdGenerate.Disabled = on;
		HdScale.Disabled = on;
		ArtOptionSelector.Disabled = on;
		if (on)
		{
			HdStatus.Text = T("Making the Pixel HD art...", "正在產生像素高清素材…");
		}
		ShowPathStatus(); //the Start buttons wait for it
	}

	public void _on_sound_options_item_selected(int index)
	{
		_uwSettings.synth = SoundOptionSelector.GetItemText(index);
		_uwSettings.Save();
		ShowSynthRows();
	}

	/// <summary>Where the launcher makes the Pixel HD sets when beside the program cannot be written.</summary>
	private static string UserHdArtRoot() => Path.Combine(OS.GetUserDataDir(), "HD");

	/// <summary>
	/// The folders the art sets are looked for in, each once: HD beside the program (where the
	/// launcher makes them), HD in the user's data folder (where it makes them when beside the
	/// program is read only), hdartroot, and the folder holding the chosen set.
	/// </summary>
	private List<string> ArtRoots()
	{
		var roots = new List<string>();
		void Add(string dir)
		{
			if (string.IsNullOrWhiteSpace(dir)) { return; }
			dir = Path.GetFullPath(dir.TrimEnd('/', '\\'));
			if (!roots.Exists(r => string.Equals(r, dir, StringComparison.OrdinalIgnoreCase))) { roots.Add(dir); }
		}
		Add(uwsettings.DefaultHdArtRoot());
		Add(UserHdArtRoot());
		Add(_uwSettings.HdArtRoot());
		if (!string.IsNullOrWhiteSpace(_uwSettings.hdartpath))
		{
			Add(Path.GetDirectoryName(_uwSettings.HdArtPath().TrimEnd('/', '\\')));
		}
		return roots;
	}

	/// <summary>
	/// Lists Original and every art set (a subfolder holding uw1/ or uw2/) in the art roots, and
	/// selects the one hdartpath names. Each item's metadata is the set's full path; a name found in
	/// more than one root also shows the folder it is in. A set named in hdartpath but not found is
	/// kept as an entry, so opening the launcher never silently changes the setting.
	/// </summary>
	private void PopulateArtOptions()
	{
		if (ArtOptionSelector == null) { return; }
		ArtOptionSelector.Clear();
		ArtOptionSelector.AddItem(T("Original (low-res pixels)", "原版(低像素風格)"));
		var sets = new List<string>();
		foreach (var root in ArtRoots())
		{
			if (!Directory.Exists(root)) { continue; }
			foreach (var dir in Directory.GetDirectories(root))
			{
				if (Directory.Exists(Path.Combine(dir, "uw1")) || Directory.Exists(Path.Combine(dir, "uw2")))
				{
					sets.Add(Path.GetFullPath(dir));
				}
			}
		}
		string current = string.IsNullOrWhiteSpace(_uwSettings.hdartpath)
			? null : Path.GetFullPath(_uwSettings.HdArtPath().TrimEnd('/', '\\'));
		if (current != null && !sets.Exists(p => string.Equals(p, current, StringComparison.OrdinalIgnoreCase)))
		{
			sets.Add(current);
		}
		sets.Sort((x, y) => string.Compare(Path.GetFileName(x), Path.GetFileName(y), StringComparison.OrdinalIgnoreCase));
		int selected = 0;
		foreach (var set in sets)
		{
			var name = Path.GetFileName(set);
			var label = ArtNames.TryGetValue(name, out var n) ? T($"{n.English} ({n.Label})", $"{n.Chinese}({n.Label})") : name;
			if (sets.Count(other => string.Equals(Path.GetFileName(other), name, StringComparison.OrdinalIgnoreCase)) > 1)
			{
				label += $" [{Path.GetFileName(Path.GetDirectoryName(set))}]";
			}
			ArtOptionSelector.AddItem(label);
			ArtOptionSelector.SetItemMetadata(ArtOptionSelector.ItemCount - 1, set);
			if (current != null && string.Equals(set, current, StringComparison.OrdinalIgnoreCase)) { selected = ArtOptionSelector.ItemCount - 1; }
		}
		ArtOptionSelector.Selected = selected;
	}

	/// <summary>
	/// Lists English and every language with a master.tsv under Localization.Root/uw1, and selects the
	/// one the settings name. As with art, a language set but not found is kept as an entry.
	/// </summary>
	private void PopulateLanguageOptions()
	{
		if (LanguageOptionSelector == null) { return; }
		LanguageOptionSelector.Clear();
		var languages = new List<string> { Localization.English };
		var root = Path.Combine(Localization.Root, "uw1");
		if (Directory.Exists(root))
		{
			var found = new List<string>();
			foreach (var dir in Directory.GetDirectories(root))
			{
				if (File.Exists(Path.Combine(dir, "master.tsv")))
				{
					found.Add(Path.GetFileName(dir));
				}
			}
			found.Sort(StringComparer.OrdinalIgnoreCase);
			languages.AddRange(found);
		}
		var current = string.IsNullOrWhiteSpace(_uwSettings.language) ? Localization.English : _uwSettings.language;
		if (!languages.Contains(current))
		{
			languages.Add(current);
		}
		foreach (var code in languages)
		{
			var name = code == Localization.English ? "English" : LanguageNames.GetValueOrDefault(code, code);
			LanguageOptionSelector.AddItem(name);
			LanguageOptionSelector.SetItemMetadata(LanguageOptionSelector.ItemCount - 1, code);
		}
		LanguageOptionSelector.Selected = languages.IndexOf(current);
	}

	/// <summary>Lists the resolutions and selects the one the settings name, keeping one not in the list.</summary>
	private void PopulateResolutionOptions()
	{
		if (ResolutionOptionSelector == null) { return; }
		ResolutionOptionSelector.Clear();
		var current = string.IsNullOrWhiteSpace(_uwSettings.resolution) ? Resolutions[0] : _uwSettings.resolution;
		var items = new List<string>(Resolutions);
		if (!items.Contains(current))
		{
			items.Insert(items.Count - 1, current);
		}
		foreach (var r in items)
		{
			ResolutionOptionSelector.AddItem(r == Fullscreen ? T("Fullscreen", "全螢幕") : r.Replace("x", " x "));
			ResolutionOptionSelector.SetItemMetadata(ResolutionOptionSelector.ItemCount - 1, r);
		}
		ResolutionOptionSelector.Selected = items.IndexOf(current);
	}

	public void _on_resolution_options_item_selected(int index)
	{
		_uwSettings.resolution = ResolutionOptionSelector.GetItemMetadata(index).AsString();
		_uwSettings.Save();
		ApplyResolution(_uwSettings.resolution);
	}

	/// <summary>
	/// Sizes the window to <paramref name="resolution"/> ("WIDTHxHEIGHT" or "fullscreen"), shrunk
	/// to the screen's usable area if it is bigger, keeping its shape, and centred. Anything
	/// unreadable leaves the window as it is.
	/// </summary>
	public static void ApplyResolution(string resolution)
	{
		if (resolution == Fullscreen)
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
			return;
		}
		var parts = (resolution ?? "").Split('x');
		if (parts.Length != 2 || !int.TryParse(parts[0], out int w) || !int.TryParse(parts[1], out int h) || w <= 0 || h <= 0)
		{
			return;
		}
		if (DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Windowed)
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		}
		var usable = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen());
		float fit = Mathf.Min(1f, Mathf.Min((float)usable.Size.X / w, (float)usable.Size.Y / h));
		var size = new Vector2I(Mathf.FloorToInt(w * fit), Mathf.FloorToInt(h * fit));
		DisplayServer.WindowSetSize(size);
		DisplayServer.WindowSetPosition(usable.Position + (usable.Size - size) / 2);
	}

	/// <summary>Lists the two control schemes, selects the one the settings name and shows the modern one's options with it.</summary>
	private void PopulateControlsOptions()
	{
		ControlsOptionSelector.Clear();
		ControlsOptionSelector.AddItem(T("Original", "原版操作"));
		ControlsOptionSelector.SetItemMetadata(0, "classic");
		ControlsOptionSelector.AddItem(T("Modern", "優化操作"));
		ControlsOptionSelector.SetItemMetadata(1, "modern");
		ControlsOptionSelector.Selected = _uwSettings.controls == "modern" ? 1 : 0;
		ShowControlsBox();
	}

	private void ShowControlsBox()
	{
		ControlsBox.Visible = _uwSettings.controls == "modern";
		ControlsHint.Text = T(
			$"Left click talks, picks up or uses, by what it is on; right click looks; shift + click puts it in the backpack. {KeyName(Controls.Action.MouseLook)}: mouse look. {KeyName(Controls.Action.Weapon)}: draw the weapon (left button swings). Wheel: change icon. Arrows move, {KeyName(Controls.Action.Jump)} jumps.",
			$"左鍵依目標自動說話、撿取或使用，右鍵查看，Shift＋左鍵直接收進背包。{KeyName(Controls.Action.MouseLook)}：滑鼠視角。{KeyName(Controls.Action.Weapon)}：拔出武器（左鍵攻擊）。滾輪：切換動作。方向鍵移動，{KeyName(Controls.Action.Jump)} 跳。");
	}

	public void _on_controls_options_item_selected(int index)
	{
		_uwSettings.controls = ControlsOptionSelector.GetItemMetadata(index).AsString();
		_uwSettings.Save();
		ShowControlsBox();
	}

	public void OnSensitivityChanged(int index)
	{
		_uwSettings.mousesensitivity = index + 1;
		_uwSettings.Save();
	}

	public void OnDragOptionSelected(int index)
	{
		_uwSettings.dragwithleft = index == 1;
		_uwSettings.Save();
	}

	public void OnInvertMouseSelected(int index)
	{
		_uwSettings.invertmouse = index == 1;
		_uwSettings.Save();
	}

	/// <summary>The keys the player may choose under modern controls, with their names.</summary>
	private static readonly (Controls.Action Action, string English, string Chinese)[] KeyActions =
	{
		(Controls.Action.Forward, "Forward", "前進"), (Controls.Action.Back, "Back", "後退"),
		(Controls.Action.StrafeLeft, "Step left", "左移"), (Controls.Action.StrafeRight, "Step right", "右移"),
		(Controls.Action.TurnLeft, "Turn left", "左轉"), (Controls.Action.TurnRight, "Turn right", "右轉"),
		(Controls.Action.Jump, "Jump", "跳躍"), (Controls.Action.FlyUp, "Fly up", "飛高"), (Controls.Action.FlyDown, "Fly down", "飛低"),
		(Controls.Action.LookUp, "Look up", "抬頭"), (Controls.Action.LookDown, "Look down", "低頭"), (Controls.Action.LookCentre, "Look ahead", "平視"),
		(Controls.Action.MouseLook, "Mouse look", "滑鼠視角"), (Controls.Action.Weapon, "Draw weapon", "拔出／收起武器"),
		(Controls.Action.Bash, "Bash", "重擊"), (Controls.Action.Slash, "Slash", "揮砍"), (Controls.Action.Stab, "Stab", "突刺"),
	};

	/// <summary>Punctuation keys by their mark rather than Godot's name for them.</summary>
	private static readonly Dictionary<Key, string> KeyMarks = new()
	{
		{ Key.Semicolon, ";" }, { Key.Period, "." }, { Key.Comma, "," }, { Key.Slash, "/" }, { Key.Apostrophe, "'" },
		{ Key.Bracketleft, "[" }, { Key.Bracketright, "]" }, { Key.Backslash, "\\" }, { Key.Minus, "-" }, { Key.Equal, "=" }, { Key.Quoteleft, "`" },
	};

	private static string KeyName(Controls.Action action)
	{
		var key = Controls.KeyFor(action);
		return key == Key.None ? "" : KeyMarks.TryGetValue(key, out var mark) ? mark : OS.GetKeycodeString(key);
	}

	private AcceptDialog _keysDialog;
	private Button _defaultsButton;
	private readonly Dictionary<Controls.Action, (Label Name, Button Key)> _keyRows = new();
	private Controls.Action? _waitingFor;

	/// <summary>
	/// The key list of modern controls: click an action's key, then press the new one (Escape
	/// keeps the old). An action that had the new key gets the old one in exchange. "Defaults"
	/// puts every key back.
	/// </summary>
	public void ShowKeyBindings()
	{
		if (_keysDialog == null)
		{
			_keysDialog = new AcceptDialog { Exclusive = true, Unresizable = true };
			var grid = new GridContainer { Columns = 4 };
			grid.AddThemeConstantOverride("h_separation", 12);
			grid.AddThemeConstantOverride("v_separation", 6);
			foreach (var (action, _, _) in KeyActions)
			{
				var name = new Label();
				var button = new Button { CustomMinimumSize = new Vector2(120, 0), FocusMode = FocusModeEnum.None };
				var act = action;
				button.Pressed += () => WaitForKey(act);
				_keyRows[action] = (name, button);
				grid.AddChild(name);
				grid.AddChild(button);
			}
			_keysDialog.AddChild(grid);
			_defaultsButton = _keysDialog.AddButton("", false, "defaults");
			_keysDialog.CustomAction += action =>
			{
				if (action == "defaults")
				{
					_uwSettings.keys.Clear();
					_uwSettings.Save();
					_waitingFor = null;
					ShowKeys();
				}
			};
			_keysDialog.WindowInput += OnKeysDialogInput;
			_keysDialog.Confirmed += CloseKeyBindings;
			_keysDialog.Canceled += CloseKeyBindings;
			AddChild(_keysDialog);
		}
		_keysDialog.Title = T("Keys (modern controls)", "按鍵設定（優化操作）");
		_keysDialog.OkButtonText = T("Done", "完成");
		_defaultsButton.Text = T("Defaults", "恢復預設");
		foreach (var (action, english, chinese) in KeyActions)
		{
			_keyRows[action].Name.Text = T(english, chinese);
		}
		_waitingFor = null;
		ShowKeys();
		_mouseModeHistory.Push(Input.MouseMode);
		Input.MouseMode = Input.MouseModeEnum.Visible;
		_keysDialog.PopupCentered();
	}

	private void CloseKeyBindings()
	{
		_waitingFor = null;
		ShowControlsBox();
		RestoreMouse();
	}

	private void ShowKeys()
	{
		foreach (var (action, row) in _keyRows)
		{
			row.Key.Text = _waitingFor == action ? T("Press a key…", "請按鍵…") : KeyName(action);
		}
	}

	private void WaitForKey(Controls.Action action)
	{
		_waitingFor = action;
		ShowKeys();
	}

	private void OnKeysDialogInput(InputEvent @event)
	{
		if (_waitingFor is not Controls.Action action || @event is not InputEventKey key || !key.Pressed || key.Echo)
		{
			return;
		}
		_keysDialog.SetInputAsHandled();
		var code = key.Keycode;
		if (code != Key.Escape && code != Key.None)
		{
			var old = Controls.KeyFor(action);
			foreach (var (other, _, _) in KeyActions)
			{
				if (other != action && Controls.KeyFor(other) == code)
				{
					SetKey(other, old);
				}
			}
			SetKey(action, code);
			_uwSettings.Save();
		}
		_waitingFor = null;
		ShowKeys();
	}

	/// <summary>Stores <paramref name="code"/> for <paramref name="action"/>, as nothing when it is the default.</summary>
	private void SetKey(Controls.Action action, Key code)
	{
		if (code == Controls.DefaultKey(action))
		{
			_uwSettings.keys.Remove(action.ToString());
		}
		else
		{
			_uwSettings.keys[action.ToString()] = OS.GetKeycodeString(code);
		}
	}

	public void _on_language_options_item_selected(int index)
	{
		_uwSettings.language = LanguageOptionSelector.GetItemMetadata(index).AsString();
		_uwSettings.Save();
		PopulateArtOptions();
		PopulateResolutionOptions();
		ApplyTexts();
	}

	public void _on_art_options_item_selected(int index)
	{
		if (index == 0)
		{
			if (string.IsNullOrWhiteSpace(_uwSettings.hdartroot) && !string.IsNullOrWhiteSpace(_uwSettings.hdartpath))
			{//the sets beside hdartpath's are only listed through it: keep that folder, or the next launch lists none
				var folder = Path.GetDirectoryName(_uwSettings.HdArtPath().TrimEnd('/', '\\'));
				if (!string.Equals(Path.GetFullPath(folder), Path.GetFullPath(uwsettings.DefaultHdArtRoot()), StringComparison.OrdinalIgnoreCase)
					&& !string.Equals(Path.GetFullPath(folder), Path.GetFullPath(UserHdArtRoot()), StringComparison.OrdinalIgnoreCase))
				{
					_uwSettings.hdartroot = uwsettings.ToProgramRelative(folder);
				}
			}
			_uwSettings.hdartpath = "";
		}
		else
		{
			_uwSettings.hdartpath = uwsettings.ToProgramRelative(ArtOptionSelector.GetItemMetadata(index).AsString());
		}
		_uwSettings.Save();
	}

}
