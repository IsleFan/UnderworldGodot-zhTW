# 創世紀地下世界 繁體中文版 — Ultima Underworld I & II

> 「你醒來時，四周一片漆黑。冥河深淵的寒氣滲進骨頭裡……」

還記得第一次在 1992 年的螢幕上，舉著火把走進冥河深淵的感覺嗎？還記得在不列顛王的城堡裡，眼睜睜看著黑岩寶石把整座城罩住的那一刻嗎？

這個專案讓《**Ultima Underworld: The Stygian Abyss**》與《**Ultima Underworld II: Labyrinth of Worlds**》在現代的 **Windows** 與 **macOS** 上原汁原味地重新運作，並且**從對話、書本到過場字幕全部翻成繁體中文**。不需要 DOSBox，也不需要猜英文謎題——這次，你可以完整讀懂每一位 NPC 想告訴你的故事。

本專案以 Hank Morgan 的開源重製引擎 [hankmorgan/UnderworldGodot](https://github.com/hankmorgan/UnderworldGodot) 為基礎（Godot 4.3 .NET），加上中文化、高清美術、音效與配樂修正，以及與原版互通的存檔。

> [!IMPORTANT]
> 本專案**不含任何遊戲資料**。你需要自己合法擁有的遊戲（例如 [GOG 的 Ultima Underworld 1+2](https://www.gog.com/)），程式會讀取你的原版遊戲檔案來執行。

## 特色

- **兩代全中文**
  - UW1：對話、書本、卷軸、訊息、物品、過場字幕，以及介面圖片上的英文，全部改成繁體中文；組句訊息（「你看到一把……」）改用中文語序。
  - UW2：125 位 NPC 的對話、書本、法術、牆上文字、過場字幕全數翻譯；用語沿用 UW1 譯文，人名、地名附上英文原名，例如「伊歐洛(Iolo)」。
  - 蜥蜴人的語言保留原文，後面附上中文意思（字義取自遊戲裡穆爾戈的手勢翻譯）。
- **像素高清美術**：除了原版(低像素風格)，在啟動畫面按「產生像素高清素材」，就會用你自己的遊戲檔案在這台電腦產生 2、4 或 8 倍解析度的「像素高清(RotSprite2x／4x／8x)」（大約一分鐘；8 倍約佔 1 GB）：牆面、怪物、物品、介面全部放大，斜線與輪廓補平但保留像素風格；文字與過場字幕用平滑的高清字。素材保留原版調色盤，光影與調色盤動畫照常運作，也不散布任何遊戲美術。
- **原汁原味的音樂**：MT-32／CM-32L（需自備 ROM）、Sound Blaster／AdLib 的 OPL 配樂，或內建的 SoundFont；音效一律是 Sound Blaster 版。
- **存檔與原版 DOS 版互通**：本專案存的 UW2 進度，可以用 GOG 原版 UW2.EXE 讀取（已實測開過的門、生命值、位置、裝備、背包與容器內容）；反過來也行。
- **現代化的便利**：可調整視窗大小或全螢幕、滑鼠可移出視窗、有啟動畫面可以選遊戲、語言、美術與配樂。
- **Windows 與 macOS（Apple Silicon 與 Intel）**，不用安裝 .NET 或其他執行環境。

## 你需要準備

1. **遊戲本體**：GOG 的《Ultima Underworld 1+2》（Windows 或 macOS 版都可以）。GOG 的一代與二代附的是同一張遊戲光碟映像，裝任一個就能取出兩代。
2. （選用）**MT-32 或 CM-32L 的 ROM**：想聽原汁原味的 Roland MT-32 配樂才需要。ROM 有版權，本專案不提供。

## 下載與安裝

到本 repo 的 [Releases](../../releases) 下載你的平台的 zip（還沒有發布時，可以照下方「自行打包」產生）：

| 檔案 | 平台 |
| --- | --- |
| `UnderworldGodot-zhTW-版本-windows-x64.zip` | Windows 10／11（x64） |
| `UnderworldGodot-zhTW-版本-macos.zip` | macOS 12 以上（Apple Silicon 與 Intel） |

解壓縮後打開程式，啟動畫面會**自動尋找遊戲**：程式旁的資料夾（例如把 `UW1`、`UW2` 放在程式旁邊）、一鍵安裝放遊戲的位置，以及 GOG 版的安裝。只找到 GOG 版的光碟映像（`game.gog`）時，會自動把兩代遊戲取出到程式旁的 `UW1`、`UW2`。找到就顯示綠色「已找到遊戲」；沒找到就顯示「沒有找到遊戲目錄」，這時按「瀏覽…」自己選。

也可以用一鍵安裝（會順便處理系統的下載封鎖、Windows 的 Visual C++ 可轉散發套件）：

- **Windows**：按兩下 `INSTALL.bat`。
- **macOS**：打開「終端機」，輸入 `bash ` 再把資料夾裡的 `install.sh` 拖進視窗，按 Return。

一鍵安裝會從 GOG 版取出 UW1、UW2 的遊戲檔案、寫好設定（繁體中文），然後打開遊戲。詳細步驟與手動安裝方式見 zip 裡的 `INSTALL.txt`。

### 需要特別注意

- **macOS 第一次打開會被擋下**：本程式沒有 Apple 公證（需要付費開發者帳號），macOS 會說「無法打開」或「已損毀」。這不是程式壞了：一鍵安裝會自動解除；手動解除方式見 `INSTALL.txt`「三、第一次執行」。
- **Windows 可能出現「Windows 已保護您的電腦」**：程式沒有數位簽章，按「其他資訊」>「仍要執行」。
- **Windows 的 MT-32 配樂**需要「Microsoft Visual C++ 可轉散發套件」：一鍵安裝會自動安裝。沒裝時選 MT-32 會自動改用 OPL 配樂，其他功能都不受影響。
- **高清美術素材**不隨程式附上（那是原版美術的衍生物）：裝好遊戲後在啟動畫面按「產生像素高清素材」即可，約一分鐘。素材放在程式旁邊的 `HD` 資料夾（跟 `ROMS` 一樣，整個資料夾搬家也找得到；程式旁邊寫不進去時，例如放在「應用程式」或 Program Files，改放在使用者資料夾）。刪掉 `HD` 就會回到原版美術。

## 冒險小提示

- 啟動畫面左邊是兩代遊戲的卡片：按「瀏覽…」選遊戲目錄，出現綠色「已找到遊戲」後按「開始遊戲」。右邊可以切換語言、視窗大小、美術與配樂。
- 需要**打字**的地方（對話中輸入關鍵字、祭壇咒語、存檔說明）請輸入**英文**，例如咒語仍是原文。
- UW2 的存檔放在遊戲目錄的 `SAVE1`～`SAVE4`。把 GOG 原版的存檔資料夾複製過來就能接著玩，反過來也可以。
- 設定檔位置：Windows 是 `%APPDATA%\Godot\app_userdata\Underworld\settings.json`，macOS 是 `~/Library/Application Support/Godot/app_userdata/Underworld/settings.json`。設定亂掉時，關閉遊戲後刪除它即可恢復預設。

## 開發者：從原始碼執行

### 需求

- [Godot 4.3](https://godotengine.org/download/archive/4.3-stable/) **.NET 版**（不是一般版）
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- 自備的遊戲檔案；選用：MT-32／CM-32L ROM

macOS 用 Homebrew 安裝 .NET 時，`dotnet@9` 不會自動加入 `PATH`，需要在 shell 設定檔加上：

```sh
export DOTNET_ROOT="$(brew --prefix dotnet@9)/libexec"
export PATH="$(brew --prefix dotnet@9)/bin:$PATH"
```

### 開始使用

1. clone 這個 repo（不需要 Git LFS），或在 GitHub 按「Code > Download ZIP」。
2. 用 Godot 4.3 .NET 版開啟專案，先執行一次 Build；或用下面的啟動檔，它每次都會先編譯。
3. 執行專案，進入啟動畫面後選遊戲目錄、語言、美術與配樂，按「開始遊戲」。

### 啟動檔（不用打指令）

`launcher/` 裡有兩個按兩下就能用的啟動檔，會先編譯最新的 C#，再用本機的 Godot 執行這份原始碼：

- macOS：`launcher/Ultima Underworld 開發版.app`。第一次開啟時系統會問是否允許取用「文件」檔案夾，按「允許」。可以拖到 Dock；搬到別處也能用（記得上次的專案位置）。找 Godot 的順序：環境變數 `GODOT`、專案旁邊的 `godot/`、「應用程式」裡的 Godot .NET 版；.NET 9 從 Homebrew 或 `/usr/local/share/dotnet` 找。編譯訊息在 `~/Library/Logs/UnderworldGodot-launcher.log`。
- Windows：`launcher/Start-Underworld-Dev.bat`。Godot 用環境變數 `GODOT`，或專案旁邊（或裡面）`godot` 資料夾中的 `Godot_v4.3-stable_mono_win64.exe`；.NET 9 SDK 要在 PATH 上（微軟安裝程式會設好）。

### MT-32 ROM

把 ROM 放在專案（或打包後程式旁邊）的 `ROMS` 資料夾，或在啟動畫面（配樂選 mt32／cm32l 時）的「MT-32／CM-32L ROM 目錄」按「瀏覽…」改成其他資料夾。可以接受的檔名有 `MT32_CONTROL.ROM`＋`MT32_PCM.ROM`、`CM32L_CONTROL.ROM`＋`CM32L_PCM.ROM`，以及 MAME 格式的檔名。`ROMS/` 已經加進 `.gitignore`，不會被提交。

### 設定檔

設定存放在 Godot 的使用者資料目錄下的 `settings.json`。大部分項目都可以在啟動畫面調整，完整欄位見 [uwsettings.schema.json](uwsettings.schema.json)。這個分支新增或改變了以下欄位：

| 欄位 | 說明 |
| --- | --- |
| `language` | `en` 或 `zh-TW` |
| `i18npath` | 譯文與字型的資料夾，空白時用專案的 `i18n/` |
| `hdartpath` | 使用中的 HD 素材組資料夾，空白時用原版美術；相對路徑（例如 `HD/rotsprite4x`）以程式所在的資料夾為準 |
| `hdartroot` | 另外列出 HD 素材組的資料夾（選用）。「美術」一律列出程式旁 `HD`、使用者資料夾 `HD` 與這裡的素材組；「產生像素高清素材」一律產生在程式旁的 `HD` |
| `resolution` | `寬x高` 或 `fullscreen`，預設 `1280x800` |
| `synthpath` | `soundfont` 用的 `.sf2` 檔，空白時用內建的 |
| `rompath` | MT-32／CM-32L ROM 資料夾，空白時用程式旁邊的 `ROMS` |

### 自行打包

```sh
GODOT=/path/to/Godot sh packaging/package.sh 0.2.0
```

需要 Godot 4.3 .NET 匯出範本（`4.3.stable.mono`）、**微軟官方的 .NET 9 SDK**（預設找 `~/.dotnet`，可用 [dotnet-install.sh](https://dot.net/v1/dotnet-install.sh) 安裝；不能用 Homebrew 的 .NET，它會把依賴 Homebrew 的執行環境打包進去），並且要在 macOS 上執行（`codesign` 做 ad-hoc 簽章）。輸出在 `build/release/`。

- 打包時會檢查：程式不能依賴 Homebrew，也不能留下建置電腦的使用者路徑；匯出版不含除錯符號。
- macOS 的 OPL／MT-32 原生函式庫（`packaging/native/macos/`）是用 `packaging/native/build-macos.sh` 從原始碼編譯的 universal 版（最低 macOS 11），取代 NuGet 套件裡只支援新版 macOS 的 Apple Silicon 版。
- 匯出版用 `System.IO` 讀取譯文、中文字型與 SoundFont，`.pck` 裡的檔案讀不到，所以打包時會把 `i18n/`、`soundfonts/` 當成一般資料夾附上：Windows 放在 exe 旁邊，macOS 放在 `.app/Contents/Resources`（見 `src/utility/apppaths.cs`）。

### 測試

- Godot 場景測試：`tests/godot/*_test.tscn`，每個測試會印出 `GODOT-TEST-VERDICT PASS/FAIL/SKIP`。執行方式見 [.github/workflows/ci.yml](.github/workflows/ci.yml)。
- 單元測試：`tests/Underworld.Save.Tests`（存讀檔）、`tests/Underworld.Sfx.Tests`（音效），xUnit。需要遊戲資料的測試會讀取你自備的檔案。

## 與原專案的差異（技術細節）

<details>
<summary>展開</summary>

### 繁體中文化

- 中文字形從 [jf open 粉圓](https://github.com/justfont/open-huninn-font) 即時產生，字高對齊原版點陣字；專有名詞後的英文原名用較小的字顯示。
- UW1 譯文來自 [pmanyeh/underworld-exhumed](https://github.com/pmanyeh/underworld-exhumed) 的 `feature/zh-tw-native` 分支。UW2 譯文由 AI（Claude）翻譯，譯名對照與取捨記在 [i18n/uw2/zh-TW/GLOSSARY.md](i18n/uw2/zh-TW/GLOSSARY.md)。
- 刻意保留原文：玩家要照著輸入的祈禱語與密碼、密碼文、開發用除錯報告。

### 高清美術

- 支援整數倍率（2×、4×…）的「索引色」HD 素材：牆面、地板、怪物、物件、介面、武器、全畫面圖、字型。素材保留調色盤索引。
- 啟動畫面的「產生像素高清素材」（`src/loaders/hdartgenerator.cs`、`hdartfonts.cs`）用玩家的遊戲檔案產生 RotSprite 2×、4× 或 8× 素材組：圖片部分與 upscale-lab 的 `export_hd.py rotsprite2x`／`4x`／`8x` 逐位元組相同（`HdArtGeneratorTests`）；三種小字型用 jf open 粉圓的英文字母重畫，過場字幕的 FONTBIG 保留原字形、以高斯加權平滑放大。其他自行產生的素材組（放在 `hdartroot` 底下）也照樣能選用。
- 過場動畫在顯示時用著色器放大。

### 音效與配樂

- 選 `opl` 時，UW1 播放 Sound Blaster／AdLib 版配樂（`AW*.XMI`）；UW2 選 `opl` 播 `UWA*.XMI`，其他合成器播 MT-32 版 `UWR*.XMI`。
- 修正 OPL 音色庫的讀取：打擊樂音色原本讀錯位置，現在依目錄逐一讀取。
- 音效一律用 Sound Blaster 版：UW1 是 OPL 合成，UW2 是錄音（`SOUND/*.VOC`）；`SOUNDS.DAT` 的長度照原版讀取，到期淡出。
- 音量重新平衡：OPL 配樂提高約 8 dB，音效與配樂取得平衡。

### 存檔

- UW2 開放存讀檔（上游原本擋掉），關卡區塊照原版長度寫入、動畫清單照原版整理、物品欄照原版串列寫入，原版 UW2.EXE 可以讀取。

### 其他

- 新的啟動畫面：遊戲卡片、「瀏覽…」與「開始遊戲」按鈕、分組設定，跟著語言顯示中文或英文。
- 解析度選項：1280×800、1600×1000、1920×1200、2560×1600（都是原版 320×200 的 16:10）或全螢幕。
- 修正中文模式下關閉遊戲會當機（Godot 4.3 .NET 的綁定問題）。

</details>

## English summary

A Traditional Chinese (zh-TW) edition of [hankmorgan/UnderworldGodot](https://github.com/hankmorgan/UnderworldGodot), the Godot 4.3 .NET engine recreation of *Ultima Underworld I* and *II*. It adds full Chinese translations of both games, a Pixel HD (RotSprite 2x, 4x or 8x) art style the launcher makes from your own game files, MT-32/OPL/SoundFont music, UW2 saves that load in the original DOS UW2.EXE, and a new launcher. No game data is included: you need your own copy (for example from GOG). Builds run on Windows 10/11 x64 and macOS 12+ (Apple Silicon and Intel) without installing .NET. The upstream English README is kept in [README.upstream.md](README.upstream.md).

## 致謝與授權

- 程式碼：MIT 授權，見 [LICENSE.md](LICENSE.md)。原專案 © Hank Morgan 與貢獻者。
- UW1 繁體中文譯文：MIT 授權，來自 [pmanyeh/underworld-exhumed](https://github.com/pmanyeh/underworld-exhumed)，見 [i18n/uw1/zh-TW/LICENSE-underworld-exhumed.txt](i18n/uw1/zh-TW/LICENSE-underworld-exhumed.txt)。
- 中文字型：[jf open 粉圓](https://github.com/justfont/open-huninn-font) 2.1，SIL Open Font License 1.1，見 [i18n/fonts/jf-openhuninn-OFL.txt](i18n/fonts/jf-openhuninn-OFL.txt)。
- 原版程式行為的參考：[abedegno/underworld-exhumed](https://github.com/abedegno/underworld-exhumed) 還原的原版程式碼。
- 其他第三方元件（libADLMIDI、Munt、MeltySynth、SoundFont 等）的授權見 [packaging/THIRD-PARTY-NOTICES.txt](packaging/THIRD-PARTY-NOTICES.txt)；更多參考資料與致謝見 [README.upstream.md](README.upstream.md)。

Ultima、Ultima Underworld 是 Electronic Arts 的商標。本專案為玩家自製的非官方作品，與 Electronic Arts、Origin Systems 及 Looking Glass Technologies 沒有任何關係。

這個分支的修改有使用 AI（Claude Code）協助開發。

*感謝每一位還記得冥河深淵的冒險者。願你的火把永不熄滅。*
