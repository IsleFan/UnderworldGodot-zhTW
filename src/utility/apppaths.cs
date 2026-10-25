using System.IO;
using Godot;

namespace Underworld;

/// <summary>
/// Where the program's own folders are, run from source or exported.
///
/// The translations, the CJK font and the bundled SoundFont are read with System.IO, which
/// cannot reach into an export's .pck, and ProjectSettings.GlobalizePath("res://...") does
/// not work in an export at all. So an export carries them as plain folders (the release
/// packaging copies i18n/ and soundfonts/ in), found here.
/// </summary>
public static class AppPaths
{
    /// <summary>True when running from source (the editor or the Godot binary), not an export.</summary>
    static bool FromSource => !OS.HasFeature("template");

    /// <summary>
    /// The folder holding the program: the .exe's on Windows and Linux, the one holding the .app
    /// on macOS; the project folder from source. The player's own files go here (ROMS).
    /// </summary>
    public static string ProgramDir()
    {
        if (FromSource)
        {
            return ProjectSettings.GlobalizePath("res://");
        }
        var dir = Path.GetDirectoryName(OS.GetExecutablePath());
        if (OS.GetName() == "macOS")
        {
            dir = Path.GetFullPath(Path.Combine(dir, "..", "..", "..")); //X.app/Contents/MacOS
        }
        return dir;
    }

    /// <summary>
    /// The folder of the files shipped with the program (i18n/, soundfonts/): the project folder
    /// from source, beside the .exe on Windows and Linux, the .app's Contents/Resources on macOS
    /// so they travel with it.
    /// </summary>
    public static string DataDir()
    {
        if (FromSource)
        {
            return ProjectSettings.GlobalizePath("res://");
        }
        var dir = Path.GetDirectoryName(OS.GetExecutablePath());
        if (OS.GetName() == "macOS")
        {
            dir = Path.GetFullPath(Path.Combine(dir, "..", "Resources")); //X.app/Contents/MacOS
        }
        return dir;
    }
}
