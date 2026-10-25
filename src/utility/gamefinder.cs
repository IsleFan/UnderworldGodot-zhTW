using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Underworld
{
    /// <summary>
    /// Finds the game folders for the launcher, so a player who has the games need not browse for
    /// them: a usable UW1 or UW2 folder (one InstallationValidator accepts) in the program's own
    /// folder, where the one-click install puts them, or a GOG installation. GOG's Ultima
    /// Underworld I and II (macOS and Windows) keep both games in one CD image, game.gog, a plain
    /// ISO 9660 file; when only the image is found, its UW and UW2 folders are copied out (Extract)
    /// beside the program, which is also where the next launch then finds them. No Godot types, so
    /// the headless tests run it.
    /// </summary>
    public static class GameFinder
    {
        public sealed record Game(byte Res, string Folder, string Exe, string DiscFolder, string[] Subfolders);

        public static readonly Game Uw1 = new(UWClass.GAME_UW1, "UW1", "UW.EXE", "UW", new[] { "UW1", "UW" });
        public static readonly Game Uw2 = new(UWClass.GAME_UW2, "UW2", "UW2.EXE", "UW2", new[] { "UW2" });

        /// <summary>Whether <paramref name="dir"/> holds <paramref name="game"/> as the launcher can start it.</summary>
        public static bool Usable(Game game, string dir) =>
            !string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) && InstallationValidator.TryValidate(game.Res, dir, out _);

        /// <summary>
        /// The first usable folder of <paramref name="game"/>: <paramref name="programDir"/> itself and its
        /// UW1/UW/UW2 subfolders, the one-click install's Games/UWGOG, then the GOG installations' own
        /// folders. Null when none holds it ready to play (a GOG image may still, see FindGogImage).
        /// </summary>
        public static string FindFolder(Game game, string programDir)
        {
            foreach (var dir in Candidates(game, programDir))
            {
                if (Usable(game, dir)) { return dir; }
            }
            return null;
        }

        static IEnumerable<string> Candidates(Game game, string programDir)
        {
            IEnumerable<string> Within(string root)
            {
                if (string.IsNullOrWhiteSpace(root)) { yield break; }
                yield return root;
                foreach (var sub in game.Subfolders) { yield return Path.Combine(root, sub); }
            }
            foreach (var dir in Within(programDir)) { yield return dir; }
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var dir in Within(Path.Combine(home, "Games", "UWGOG"))) { yield return dir; }
            foreach (var gog in GogFolders())
            {
                foreach (var dir in Within(gog)) { yield return dir; }
            }
        }

        /// <summary>
        /// Where GOG installed Ultima Underworld I or II: on macOS the game folder inside the apps in
        /// Applications; on Windows the folders GOG records in the registry and the usual install folders.
        /// </summary>
        public static IEnumerable<string> GogFolders()
        {
            var found = new List<string>();
            if (OperatingSystem.IsMacOS())
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                foreach (var apps in new[] { "/Applications", Path.Combine(home, "Applications") })
                {
                    if (!Directory.Exists(apps)) { continue; }
                    foreach (var app in Directory.GetDirectories(apps, "*Underworld*.app"))
                    {
                        found.Add(Path.Combine(app, "Contents", "Resources", "game"));
                    }
                }
            }
            else if (OperatingSystem.IsWindows())
            {
                foreach (var hive in new[] { @"SOFTWARE\WOW6432Node\GOG.com\Games", @"SOFTWARE\GOG.com\Games" })
                {
                    using var games = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(hive);
                    if (games == null) { continue; }
                    foreach (var id in games.GetSubKeyNames())
                    {
                        using var key = games.OpenSubKey(id);
                        var name = key?.GetValue("gameName") as string;
                        var path = key?.GetValue("path") as string;
                        if (name != null && path != null && name.Contains("Underworld", StringComparison.OrdinalIgnoreCase))
                        {
                            found.Add(path);
                        }
                    }
                }
                var roots = new[]
                {
                    @"C:\GOG Games",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy", "Games"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GOG Galaxy", "Games"),
                };
                foreach (var root in roots)
                {
                    if (!Directory.Exists(root)) { continue; }
                    found.AddRange(Directory.GetDirectories(root, "*Underworld*"));
                }
            }
            return found.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists).ToList();
        }

        /// <summary>The first game.gog of a GOG installation that is an ISO 9660 image holding <paramref name="game"/>'s folder, or null.</summary>
        public static string FindGogImage(Game game)
        {
            foreach (var gog in GogFolders())
            {
                var image = Directory.EnumerateFiles(gog, "game.gog", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 2, MatchCasing = MatchCasing.CaseInsensitive })
                    .FirstOrDefault();
                if (image == null) { continue; }
                try
                {
                    using var iso = new IsoImage(image);
                    if (iso.FindDirectory(game.DiscFolder) != null) { return image; }
                }
                catch (InvalidDataException) { }
            }
            return null;
        }

        /// <summary>
        /// Copies <paramref name="game"/>'s folder out of the GOG image <paramref name="image"/> to
        /// <paramref name="dest"/> (made, never overwritten: an existing folder may hold saves). Returns the files copied.
        /// </summary>
        public static int Extract(string image, Game game, string dest)
        {
            if (Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any())
            {
                throw new IOException($"{dest} already exists");
            }
            using var iso = new IsoImage(image);
            var folder = iso.FindDirectory(game.DiscFolder) ?? throw new InvalidDataException($"{game.DiscFolder} is not on the disc");
            return iso.CopyTree(folder, dest);
        }

        /// <summary>A plain ISO 9660 image read through its primary volume descriptor: directories and file extents only.</summary>
        sealed class IsoImage : IDisposable
        {
            const int Sector = 2048;
            readonly FileStream stream;
            readonly Entry root;

            public sealed record Entry(string Name, long Extent, long Size, bool IsDirectory);

            public IsoImage(string path)
            {
                stream = File.OpenRead(path);
                var pvd = Read(16 * Sector, Sector);
                if (pvd[0] != 1 || pvd[1] != 'C' || pvd[2] != 'D' || pvd[3] != '0' || pvd[4] != '0' || pvd[5] != '1')
                {
                    stream.Dispose();
                    throw new InvalidDataException("not an ISO 9660 image");
                }
                root = Record(pvd, 156);
            }

            byte[] Read(long offset, long count)
            {
                var buf = new byte[count];
                stream.Position = offset;
                stream.ReadExactly(buf);
                return buf;
            }

            static Entry Record(byte[] d, int p)
            {
                long extent = BitConverter.ToUInt32(d, p + 2), size = BitConverter.ToUInt32(d, p + 10);
                bool dir = (d[p + 25] & 2) != 0;
                int nameLen = d[p + 32];
                var name = System.Text.Encoding.ASCII.GetString(d, p + 33, nameLen);
                int semicolon = name.IndexOf(';');
                if (semicolon >= 0) { name = name[..semicolon]; }
                return new Entry(name.TrimEnd('.'), extent, size, dir);
            }

            public IEnumerable<Entry> List(Entry dir)
            {
                var d = Read(dir.Extent * Sector, dir.Size);
                for (int p = 0; p < d.Length;)
                {
                    int len = d[p];
                    if (len == 0) { p = (p / Sector + 1) * Sector; continue; } //records never span sectors
                    if (!(d[p + 32] == 1 && (d[p + 33] == 0 || d[p + 33] == 1))) { yield return Record(d, p); } //not . or ..
                    p += len;
                }
            }

            public Entry FindDirectory(string name) =>
                List(root).FirstOrDefault(e => e.IsDirectory && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

            public int CopyTree(Entry dir, string dest)
            {
                Directory.CreateDirectory(dest);
                int files = 0;
                foreach (var e in List(dir))
                {
                    var target = Path.Combine(dest, e.Name);
                    if (e.IsDirectory)
                    {
                        files += CopyTree(e, target);
                    }
                    else
                    {
                        File.WriteAllBytes(target, Read(e.Extent * Sector, e.Size));
                        files++;
                    }
                }
                return files;
            }

            public void Dispose() => stream.Dispose();
        }
    }
}
