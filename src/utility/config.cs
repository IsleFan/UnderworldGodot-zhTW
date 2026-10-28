using System.IO;
using System.Text.Json;
using System;
using Godot;
using System.Diagnostics;

namespace Underworld;

public class uwsettings
{

	private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        IgnoreReadOnlyProperties = true,
        PropertyNameCaseInsensitive = true,
    };

	private static readonly string FilePath
		= ProjectSettings.GlobalizePath("user://settings.json");

    public static uwsettings instance;

    // This initialises our instance as soon as the class is loaded.
    static uwsettings() => LoadSettings();

    public static void LoadSettings()
    {

        switch (JsonFile.TryRead<uwsettings>(FilePath, JsonOpts, out var loaded, out string error))
        {
            case JsonReadOutcome.Ok:
                Debug.Print($"Loading settings from {FilePath}");
                instance = loaded;
                break;

            case JsonReadOutcome.NotFound:
                Debug.Print($"No existing settings at {FilePath}. Loading defaults.");
                instance = new();
                break;

            default:
                // This runs from the static constructor, so letting the failure out faults the
                // type for the whole process: every later read of uwsettings.instance throws,
                // BasePath is never set, and the game cannot start with nothing on screen to
                // say why. Keep the file so it can be looked at, and carry on with defaults.
                //
                // GD.PushWarning rather than Debug.Print: Debug.Print is [Conditional("DEBUG")]
                // and compiles out of a release build, and this is the one message that explains
                // why the settings just reverted.
                string kept = JsonFile.PreserveUnreadable(FilePath);
                GD.PushWarning($"Could not read {FilePath} ({error}). Loading defaults."
                    + (kept != null
                        ? $" The file was kept as {kept}."
                        : " The file could not be moved aside, so the next save will replace it."));
                instance = new();
                break;
        }

        // A file can parse and still be unusable. gametoload set to null, or to something that
        // names no game, used to throw from here, and this runs from the static constructor, so
        // the effect was the same as an unreadable file: the type faults and the game cannot
        // start. Treat it the same way, and keep the file so it can be looked at.
        if (!GameSelection.TryResolve(instance.gametoload, out byte res))
        {
            string keptBadGame = JsonFile.PreserveUnreadable(FilePath);
            GD.PushWarning(
                $"{FilePath} names no game I recognise (gametoload was "
                + (instance.gametoload == null ? "not set" : $"'{instance.gametoload}'")
                + "). Loading defaults."
                + (keptBadGame != null
                    ? $" The file was kept as {keptBadGame}."
                    : " The file could not be moved aside, so the next save will replace it."));
            instance = new();
            if (!GameSelection.TryResolve(instance.gametoload, out res))
            {
                // Only reachable if the default in this class stops naming a game, which would
                // be a mistake in this file rather than in anyone's settings. Say so instead of
                // running on with a selection nothing chose.
                GD.PushError(
                    $"uwsettings default gametoload '{instance.gametoload}' names no game. "
                    + "Falling back to UW1.");
                res = UWClass.GAME_UW1;
            }
        }

        UWClass._RES = res;
        UWClass.BasePath = (res == UWClass.GAME_UW2) ? instance.pathuw2 : instance.pathuw1;

        // After the game selection, so a file that is about to be discarded does not get to
        // move the camera on its way out.
        if (main.cameraPitchGimbal_world != null)
        {
            main.cameraPitchGimbal_world.Fov = Math.Max(50, instance.FOV);
            main.cameraPitchGimbal_sprites.Fov = main.cameraPitchGimbal_world.Fov;
        }

    }

    public string pathuw1 { get; set; } = @"C:\Games\UW";
    public string pathuw2 { get; set; } = @"C:\Games\UW2";
    public string gametoload { get; set; } = "UW1";
    public int level { get; set; } = 0;
    public float FOV { get; set; } = 75;
    public bool showcolliders { get; set; }
    public int shaderbandsize { get; set; } = 8;
    public string synth { get; set; } = "soundfont";
    // The SoundFont (.sf2) for synth soundfont. Empty uses the defaults (see MeltySynthEngine).
    public string synthpath { get; set; } = "";
    // Folder of the MT-32 or CM-32L ROM images for synth mt32 and cm32l. Empty uses DefaultRomDir.
    public string rompath { get; set; } = "";
    // Folder of higher resolution indexed art made from the player's own data (see HdArt).
    // Empty uses the original art. A relative path is taken from the program's folder (HdArtPath).
    public string hdartpath { get; set; } = "";
    // Folder holding the art sets the launcher offers (each a subfolder with uw1/ and uw2/).
    // Empty uses the folder that hdartpath is in, else DefaultHdArtRoot. Relative as hdartpath.
    public string hdartroot { get; set; } = "";
    // Language of the game text: "en", or one with a translation (see Localization).
    public string language { get; set; } = "en";
    // Folder of translations and the font they need. Empty uses the project's i18n folder.
    public string i18npath { get; set; } = "";
    // Window size as "WIDTHxHEIGHT", or "fullscreen". The game draws at 1280x800 and is scaled to it.
    public string resolution { get; set; } = "1280x800";
    // "classic", the original's controls, or "modern": context clicks, mouse look and the keys below (see Controls).
    public string controls { get; set; } = "classic";
    // Mouse look speed, 1 to 10.
    public int mousesensitivity { get; set; } = 5;
    public bool invertmouse { get; set; }
    // The button that drags objects (in the inventory, and out of the 3D view after a look): the right one, or the left.
    public bool dragwithleft { get; set; }
    // Modern controls' key for each Controls.Action that is not its default, as Godot names keys ("W", "Space").
    public System.Collections.Generic.Dictionary<string, string> keys { get; set; } = new();

    /// <summary>The MT-32 ROM folder: rompath, else DefaultRomDir.</summary>
    public string RomDir() => string.IsNullOrWhiteSpace(rompath) ? DefaultRomDir() : rompath;

    /// <summary>ROMS beside the program (AppPaths.ProgramDir).</summary>
    public static string DefaultRomDir() => Path.Combine(AppPaths.ProgramDir(), "ROMS");

    /// <summary>HD beside the program, as ROMS: where the launcher makes the Pixel HD art sets.</summary>
    public static string DefaultHdArtRoot() => Path.Combine(AppPaths.ProgramDir(), "HD");

    /// <summary>hdartpath, a relative one ("HD/rotsprite4x") taken from the program's folder so the set moves with the program.</summary>
    public string HdArtPath() => FromProgramDir(hdartpath);

    /// <summary>hdartroot, relative as hdartpath.</summary>
    public string HdArtRoot() => FromProgramDir(hdartroot);

    static string FromProgramDir(string path) =>
        string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ? path ?? "" : Path.Combine(AppPaths.ProgramDir(), path);

    /// <summary><paramref name="path"/> relative to the program's folder when it is inside it, else as it is: what to store in hdartpath.</summary>
    public static string ToProgramRelative(string path)
    {
        var rel = Path.GetRelativePath(AppPaths.ProgramDir(), path);
        return rel.StartsWith("..") || Path.IsPathRooted(rel) ? path : rel;
    }

    public void Save()
    {
        Debug.Print($"Saving settings to {FilePath}");
        using var stream = File.Create(FilePath);
        JsonSerializer.Serialize(stream, this, JsonOpts);
    }

}
