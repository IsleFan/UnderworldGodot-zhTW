using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
		ApplyTexts();
		ShowSynthRows();
#if DEBUG
		//Testing aid: UW_TESTHDGEN=folder makes the Pixel HD set there as the button does, prints the outcome and quits.
		var testHdGen = OS.GetEnvironment("UW_TESTHDGEN");
		if (!string.IsNullOrEmpty(testHdGen))
		{
			_uwSettings.hdartroot = testHdGen;
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
			HdStatus.Text = T("Made on this computer from your own game files, in about a minute.",
				"用你自己的遊戲檔案在這台電腦產生，大約一分鐘。");
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
		ShowGameStatus(PathUW1Status, UWClass.GAME_UW1, _uwSettings.pathuw1);
		ShowGameStatus(PathUW2Status, UWClass.GAME_UW2, _uwSettings.pathuw2);
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

	/// <summary>Shows whether game <paramref name="res"/> is at <paramref name="path"/>, and lets its Start button start it only if so.</summary>
	private void ShowGameStatus(Label status, byte res, string path)
	{
		bool found = false;
		if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
		{
			ShowStatus(status, false, T("Folder not found: choose it with Browse", "找不到遊戲目錄，請按「瀏覽…」選擇"));
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
		(res == UWClass.GAME_UW2 ? StartUW2 : StartUW1).Disabled = !found || _generating;
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
		string root = ArtRoot() ?? Path.Combine(OS.GetUserDataDir(), "hd");
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
			if (string.IsNullOrWhiteSpace(_uwSettings.hdartroot)) { _uwSettings.hdartroot = root; }
			_uwSettings.hdartpath = set;
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

	/// <summary>
	/// The folder the art sets are in: hdartroot, else the folder holding the current set.
	/// </summary>
	private string ArtRoot()
	{
		if (!string.IsNullOrWhiteSpace(_uwSettings.hdartroot))
		{
			return _uwSettings.hdartroot;
		}
		if (!string.IsNullOrWhiteSpace(_uwSettings.hdartpath))
		{
			return Path.GetDirectoryName(_uwSettings.hdartpath.TrimEnd('/', '\\'));
		}
		return null;
	}

	/// <summary>
	/// Lists Original and every subfolder of the art root holding uw1/ or uw2/, and selects the
	/// one hdartpath names. A set named in hdartpath but not found is kept as an entry, so
	/// opening the launcher never silently changes the setting.
	/// </summary>
	private void PopulateArtOptions()
	{
		if (ArtOptionSelector == null) { return; }
		ArtOptionSelector.Clear();
		ArtOptionSelector.AddItem(T("Original (low-res pixels)", "原版(低像素風格)"));
		string root = ArtRoot();
		var sets = new List<string>();
		if (root != null && Directory.Exists(root))
		{
			foreach (var dir in Directory.GetDirectories(root))
			{
				if (Directory.Exists(Path.Combine(dir, "uw1")) || Directory.Exists(Path.Combine(dir, "uw2")))
				{
					sets.Add(Path.GetFileName(dir));
				}
			}
			sets.Sort(StringComparer.OrdinalIgnoreCase);
		}
		string current = string.IsNullOrWhiteSpace(_uwSettings.hdartpath)
			? null : Path.GetFileName(_uwSettings.hdartpath.TrimEnd('/', '\\'));
		if (current != null && !sets.Contains(current))
		{
			sets.Add(current);
		}
		foreach (var set in sets)
		{
			ArtOptionSelector.AddItem(ArtNames.TryGetValue(set, out var n) ? T($"{n.English} ({n.Label})", $"{n.Chinese}({n.Label})") : set);
			ArtOptionSelector.SetItemMetadata(ArtOptionSelector.ItemCount - 1, set);
		}
		ArtOptionSelector.Selected = current == null ? 0 : sets.IndexOf(current) + 1;
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
			if (string.IsNullOrWhiteSpace(_uwSettings.hdartroot))
			{//the sets were found beside hdartpath's: keep that folder, or the next launch lists none
				_uwSettings.hdartroot = ArtRoot() ?? "";
			}
			_uwSettings.hdartpath = "";
		}
		else
		{
			string root = ArtRoot();
			if (root == null) { return; }
			_uwSettings.hdartpath = Path.Combine(root, ArtOptionSelector.GetItemMetadata(index).AsString());
		}
		_uwSettings.Save();
	}

}
