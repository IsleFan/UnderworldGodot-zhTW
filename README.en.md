# Ultima Underworld I & II — Traditional Chinese Edition

[繁體中文](README.md) | **English**

> "You awaken in darkness. The chill of the Stygian Abyss seeps into your bones…"

Remember lighting your first torch in the Stygian Abyss back in 1992? Or watching the blackrock gem seal Lord British's castle?

This project runs ***Ultima Underworld: The Stygian Abyss*** and ***Ultima Underworld II: Labyrinth of Worlds*** natively on modern **Windows** and **macOS**, faithful to the originals, with **everything from conversations and books to cutscene subtitles translated into Traditional Chinese**. No DOSBox needed, and you can play in English just as well: the language is a launcher setting.

It is built on Hank Morgan's open-source engine recreation [hankmorgan/UnderworldGodot](https://github.com/hankmorgan/UnderworldGodot) (Godot 4.3 .NET), with a Chinese translation, high-resolution art, sound and music fixes, optional modern controls, and saves that work with the original DOS games.

> [!IMPORTANT]
> This project **contains no game data**. You need your own legal copy of the games (for example [Ultima Underworld 1+2 on GOG](https://www.gog.com/)); the program reads your original game files.

## Features

- **Both games fully translated into Traditional Chinese**
  - UW1: conversations, books, scrolls, messages, item names, cutscene subtitles and the English painted into the interface pictures; composed messages ("You see a…") follow Chinese word order.
  - UW2: the conversations of all 125 NPCs, books, spells, wall writing and cutscene subtitles; terms follow the UW1 translation, and names keep their English original in brackets, e.g. 伊歐洛(Iolo).
  - The lizardmen's language stays as it is, followed by its meaning (taken from Murgo's sign language in the game).
- **Pixel HD art**: besides the original low-resolution art, the launcher's "Make Pixel HD art" button builds a 2x, 4x or 8x RotSprite set from your own game files on your computer (about a minute; 8x takes about 1 GB). Walls, creatures, objects and the interface are enlarged with smoothed diagonals and outlines that still look like pixel art; text and cutscene subtitles use smooth high-resolution fonts. The art keeps the original palette, so lighting and palette animation work as before, and no game art is distributed.
- **Authentic music**: MT-32/CM-32L (bring your own ROMs), Sound Blaster/AdLib OPL music, or the built-in SoundFont; sound effects are always the Sound Blaster ones.
- **Saves compatible with the DOS games**: UW2 games saved here load in GOG's original UW2.EXE (tested with opened doors, health, position, equipment, backpack and container contents), and the other way round.
- **Optional modern controls**: the launcher's "Controls" setting offers "Original" or "Modern". With modern controls you no longer switch interaction icons all the time: a left click talks, picks up or uses depending on what it is on, a right click looks, and an icon beside the cursor shows what a left click will do; shift + left click puts an item straight into the backpack; Tab toggles mouse look (like a modern first-person game), X draws the weapon and holding the left button attacks, the wheel steps through the icons, the arrow keys move and Space jumps. Mouse speed, inverted look and every key can be changed in the launcher.
- **Modern conveniences**: resizable window or full screen, the mouse can leave the window, a launcher for choosing the game, language, controls, art and music; conversation answers light up in gold under the mouse so clicking picks the right one; with the HD art the cursor is a smooth high-resolution one too.
- **Windows and macOS (Apple Silicon and Intel)**, with no .NET or other runtime to install. Tested on a physical Windows PC, an Apple Silicon Mac and an Intel Mac.

## What you need

1. **The games**: *Ultima Underworld 1+2* from GOG (the Windows or the macOS version). GOG's first and second game come with the same disc image, so installing either one gives you both.
2. (Optional) **MT-32 or CM-32L ROMs**, only for Roland MT-32 music. The ROMs are copyrighted and not included.

## Download and setup

Download the zip for your platform from this repository's [Releases](../../releases/latest):

| File | Platform |
| --- | --- |
| `UnderworldGodot-zhTW-<version>-windows-x64.zip` | Windows 10/11 (x64) |
| `UnderworldGodot-zhTW-<version>-macos.zip` | macOS 12 or later (Apple Silicon and Intel) |

There is nothing to install and no installer. Follow these steps in order (the zip's `INSTALL.txt` has the details, in Chinese):

1. **Install the games** from GOG (*Ultima Underworld* or *II*; either one gives you both). You do not need to extract any game files yourself.
2. **(Optional, Windows MT-32 music only) Install the [Microsoft Visual C++ Redistributable (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe).** Without it, choosing MT-32 falls back to OPL music; nothing else needs it.
3. **Unzip the program** into a folder you can write to (for example under Documents), not Program Files or Applications: the game files, saves and HD art are kept next to the program. On Windows, right-click the zip > Properties > tick "Unblock" before unzipping.
4. **Open it for the first time**:
   - Windows: if "Windows protected your PC" appears, click "More info" > "Run anyway".
   - macOS: the app is not notarized by Apple, so the first time macOS says it "can't be opened" or "is damaged". It is not: run `xattr -dr com.apple.quarantine` followed by the path of Underworld.app in Terminal, or go to System Settings > Privacy & Security and click "Open Anyway".
5. **In the launcher, in this order**:
   1. Choose the language ("English" or "繁體中文").
   2. Check that both games show a green "Game found". The launcher **finds the games by itself** (`UW1` and `UW2` next to the program, the old one-click install's `Games/UWGOG`, and GOG's installation); when it finds only GOG's disc image (`game.gog`), it copies both games out next to the program. If not found, click "Browse…" and choose the folder with UW.EXE (UW2.EXE for the second game), DATA and SOUND.
   3. Choose "Controls": Original or Modern.
   4. Choose "Music", "Art" (click "Make Pixel HD art" for the HD look) and "Window".
   5. Click "Start". Everything is remembered: next time just open the program and click "Start".

### Good to know

- **Graphics**: needs Vulkan or DirectX 12 (Windows). Virtual machines such as Parallels do not support it: the window stays black and closes.
- **HD art** is not shipped (it is derived from the original art): after installing the games, click "Make Pixel HD art" in the launcher (about a minute). The art goes into the `HD` folder next to the program (like `ROMS`, it moves with the folder; when the program's folder is not writable it goes into your user folder). Delete `HD` to go back to the original art.

## Tips

- Typed input (keywords in conversations, altar mantras, save descriptions) must be in **English**; the mantras are the original ones.
- UW2 saves are in the game folder's `SAVE1` to `SAVE4`. Copy GOG's original save folders here to continue a game, or the other way round.
- Settings file: `%APPDATA%\Godot\app_userdata\Underworld\settings.json` on Windows, `~/Library/Application Support/Godot/app_userdata/Underworld/settings.json` on macOS. If the settings go wrong, delete it while the game is closed to get the defaults back.

## For developers: running from source

### Requirements

- [Godot 4.3](https://godotengine.org/download/archive/4.3-stable/), the **.NET** edition
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Your own game files; optional MT-32/CM-32L ROMs

With .NET from Homebrew on macOS, `dotnet@9` is not added to `PATH`; add this to your shell profile:

```sh
export DOTNET_ROOT="$(brew --prefix dotnet@9)/libexec"
export PATH="$(brew --prefix dotnet@9)/bin:$PATH"
```

### Getting started

1. Clone this repository (no Git LFS needed), or use "Code > Download ZIP" on GitHub.
2. Open the project in Godot 4.3 .NET and build once, or use one of the launchers below, which build every time.
3. Run the project, choose the game folders, language, art and music in the launcher, and click "Start".

### Launchers (no commands to type)

`launcher/` holds two double-click launchers that build the latest C# and run this source with your Godot:

- macOS: `launcher/Ultima Underworld 開發版.app`. The first time, macOS asks whether it may access the Documents folder: click "Allow". It can go in the Dock and still works when moved (it remembers the project). It looks for Godot in the `GODOT` environment variable, a `godot/` folder next to the project, then the Godot .NET app in Applications; .NET 9 from Homebrew or `/usr/local/share/dotnet`. The build log is `~/Library/Logs/UnderworldGodot-launcher.log`.
- Windows: `launcher/Start-Underworld-Dev.bat`. Godot from the `GODOT` environment variable or `Godot_v4.3-stable_mono_win64.exe` in a `godot` folder next to (or inside) the project; the .NET 9 SDK must be on PATH (Microsoft's installer does that).

### MT-32 ROMs

Put the ROMs in the `ROMS` folder of the project (or next to the packaged program), or choose another folder with "Browse…" under "MT-32 / CM-32L ROM folder" in the launcher (shown when the music is mt32 or cm32l). Accepted names: `MT32_CONTROL.ROM` + `MT32_PCM.ROM`, `CM32L_CONTROL.ROM` + `CM32L_PCM.ROM`, and MAME-style names. `ROMS/` is in `.gitignore`.

### Settings

Settings are kept in `settings.json` in Godot's user data folder. Most of them can be changed in the launcher; all fields are in [uwsettings.schema.json](uwsettings.schema.json). Fields added or changed in this fork:

| Field | Meaning |
| --- | --- |
| `language` | `en` or `zh-TW` |
| `i18npath` | Folder of translations and fonts; empty uses the project's `i18n/` |
| `hdartpath` | The HD art set in use; empty for the original art. A relative path (e.g. `HD/rotsprite4x`) is taken from the program's folder |
| `hdartroot` | Another folder of HD art sets to list (optional). "Art" always lists the sets in `HD` next to the program, in the user folder's `HD` and here; "Make Pixel HD art" always makes them in `HD` next to the program |
| `resolution` | `WIDTHxHEIGHT` or `fullscreen`, default `1280x800` |
| `synthpath` | The `.sf2` file for `soundfont`; empty uses the bundled one |
| `rompath` | MT-32/CM-32L ROM folder; empty uses `ROMS` next to the program |
| `controls` | `classic` (original controls, default) or `modern` |
| `mousesensitivity` | Modern controls' mouse look speed, 1 to 10, default 5 |
| `invertmouse` | Modern controls' inverted mouse look |
| `keys` | Modern controls' changed keys (action name to key name), edited with "Keys…" in the launcher |

### Packaging

```sh
GODOT=/path/to/Godot sh packaging/package.sh 0.3.0
```

Needs the Godot 4.3 .NET export templates (`4.3.stable.mono`) and **Microsoft's .NET 9 SDK** (looked for in `~/.dotnet`; install it with [dotnet-install.sh](https://dot.net/v1/dotnet-install.sh). Homebrew's .NET cannot be used: it would package a runtime that depends on Homebrew), and must run on macOS (`codesign` for ad-hoc signing). Output goes to `build/release/`.

- Packaging checks that nothing depends on Homebrew and that no user paths of the build machine are left; the export has no debug symbols.
- The macOS OPL/MT-32 native libraries (`packaging/native/macos/`) are universal builds from source made with `packaging/native/build-macos.sh` (macOS 11 or later), replacing the NuGet packages' Apple-Silicon-only builds for recent macOS.
- The export reads the translations, Chinese font and SoundFont with `System.IO`, which cannot read inside the `.pck`, so `i18n/` and `soundfonts/` ship as plain folders: next to the exe on Windows, in `.app/Contents/Resources` on macOS (see `src/utility/apppaths.cs`).

### Tests

- Godot scene tests: `tests/godot/*_test.tscn`; each prints `GODOT-TEST-VERDICT PASS/FAIL/SKIP`. See [.github/workflows/ci.yml](.github/workflows/ci.yml) for how they run.
- Unit tests: `tests/Underworld.Save.Tests` (saving and loading) and `tests/Underworld.Sfx.Tests` (sound effects), xUnit. Tests that need game data read your own files.

## Differences from the upstream project (technical)

<details>
<summary>Expand</summary>

### Traditional Chinese

- Chinese glyphs are rasterized at run time from [jf open huninn](https://github.com/justfont/open-huninn-font), sized to match the original bitmap fonts; the English names after proper nouns are drawn smaller.
- The UW1 translation comes from the `feature/zh-tw-native` branch of [pmanyeh/underworld-exhumed](https://github.com/pmanyeh/underworld-exhumed). UW2 was translated with AI (Claude); terms and choices are recorded in [i18n/uw2/zh-TW/GLOSSARY.md](i18n/uw2/zh-TW/GLOSSARY.md).
- Kept in the original on purpose: prayers and passwords the player has to type, cipher text, and developer debug reports.

### HD art

- Integer-scale (2x, 4x…) indexed HD art for walls, floors, creatures, objects, the interface, weapons, full-screen pictures and fonts; the art keeps palette indices.
- The launcher's "Make Pixel HD art" (`src/loaders/hdartgenerator.cs`, `hdartfonts.cs`) builds RotSprite 2x, 4x or 8x sets from the player's game files: the pictures are byte for byte those of upscale-lab's `export_hd.py rotsprite2x`/`4x`/`8x` (`HdArtGeneratorTests`); the three small fonts are redrawn with jf open huninn's Latin letters, and the cutscene font FONTBIG keeps its own shapes with a smooth Gaussian upscale. Other sets you make (under `hdartroot`) can be chosen too.
- Cutscenes are upscaled with a shader as they are shown.
- With HD art, the crosshair and the ranged weapon's target cursor are redrawn smooth (`src/ui/hdcursors.cs`).

### Sound and music

- With `opl`, UW1 plays the Sound Blaster/AdLib music (`AW*.XMI`); UW2 plays `UWA*.XMI` with `opl` and the MT-32 `UWR*.XMI` with the other synths.
- Fixed reading the OPL instrument bank: the percussion instruments were read from the wrong place and are now read one by one from the directory.
- Sound effects are always the Sound Blaster ones: OPL synthesis in UW1, recordings (`SOUND/*.VOC`) in UW2; `SOUNDS.DAT` lengths are read as the original does, fading out when they end.
- Rebalanced volume: OPL music about 8 dB louder, effects and music balanced.

### Saves

- UW2 saving and loading are enabled (upstream blocked them); level blocks are written at the original length, the animation list is tidied as the original does, and the inventory is written as the original's linked lists, so the original UW2.EXE can load them.

### Controls

- Optional modern controls (`src/ui/controls.cs`): context clicks, mouse look, rebindable keys, the click hint by the cursor; the original controls stay unchanged.
- Conversation answers are found by where the text is actually laid out (it used to assume evenly spaced single lines), and the answer under the mouse is drawn in gold.

### Other

- A new launcher: game cards, "Browse…" and "Start" buttons, grouped settings, in Chinese or English with the language setting.
- Window sizes: 1280×800, 1600×1000, 1920×1200, 2560×1600 (all 16:10 like the original 320×200) or full screen.
- Fixed a crash on quitting in Chinese mode (a Godot 4.3 .NET binding issue).
- Opening the options menu with Chinese lettering is fast again (it took up to 40 seconds with 8x art).

</details>

## Credits and licences

- Code: MIT licence, see [LICENSE.md](LICENSE.md). Upstream project © Hank Morgan and contributors.
- UW1 Traditional Chinese translation: MIT licence, from [pmanyeh/underworld-exhumed](https://github.com/pmanyeh/underworld-exhumed), see [i18n/uw1/zh-TW/LICENSE-underworld-exhumed.txt](i18n/uw1/zh-TW/LICENSE-underworld-exhumed.txt).
- Chinese font: [jf open huninn](https://github.com/justfont/open-huninn-font) 2.1, SIL Open Font License 1.1, see [i18n/fonts/jf-openhuninn-OFL.txt](i18n/fonts/jf-openhuninn-OFL.txt).
- Reference for the original program's behaviour: the original code recovered by [abedegno/underworld-exhumed](https://github.com/abedegno/underworld-exhumed).
- Licences of other third-party components (libADLMIDI, Munt, MeltySynth, the SoundFont and more) are in [packaging/THIRD-PARTY-NOTICES.txt](packaging/THIRD-PARTY-NOTICES.txt); more references and thanks in [README.upstream.md](README.upstream.md).

Ultima and Ultima Underworld are trademarks of Electronic Arts. This is an unofficial fan project, not affiliated with Electronic Arts, Origin Systems or Looking Glass Technologies.

The changes in this fork were developed with the help of AI (Claude Code).

*To every adventurer who still remembers the Stygian Abyss: may your torch never go out.*
