#!/bin/sh
# 匯出 macOS（universal：Apple Silicon 與 Intel）與 Windows（x64）版，補上譯文、字型與 SoundFont，打包成 zip。
# 用法：sh packaging/package.sh 版本號（例如 0.2.0）
# 需求：Godot 4.3 .NET 版（GODOT 環境變數指向執行檔）、已安裝 4.3.stable.mono 匯出範本、
#       微軟官方的 .NET 9 SDK（DOTNET_ROOT，預設 ~/.dotnet，可用 https://dot.net/v1/dotnet-install.sh 安裝）、
#       macOS（ad-hoc 簽章用 codesign、壓縮用 ditto）。
#       不能用 Homebrew 的 .NET：它是從原始碼編譯的，匯出時會把依賴 Homebrew 函式庫的 runtime 打包進去，
#       沒裝 Homebrew 的 Mac 會出錯。
# 輸出：build/release/UnderworldGodot-zhTW-版本號-{macos,windows-x64}.zip
set -e
VERSION=$1
[ -n "$VERSION" ] || { echo "用法：sh packaging/package.sh 版本號" >&2; exit 1; }
ROOT=$(cd "$(dirname "$0")/.." && pwd)
GODOT=${GODOT:-godot}
NAME=UnderworldGodot-zhTW-$VERSION
OUT="$ROOT/build/release"

DOTNET_ROOT=${DOTNET_ROOT:-$HOME/.dotnet}
case "$DOTNET_ROOT" in
    /opt/homebrew/*|/usr/local/Cellar/*|/usr/local/opt/*)
        echo "DOTNET_ROOT 是 Homebrew 的 .NET（$DOTNET_ROOT），請改用微軟官方的 .NET 9 SDK。" >&2; exit 1 ;;
esac
[ -x "$DOTNET_ROOT/dotnet" ] || { echo "找不到 $DOTNET_ROOT/dotnet：請安裝微軟官方的 .NET 9 SDK。" >&2; exit 1; }
export DOTNET_ROOT PATH="$DOTNET_ROOT:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1

# 同一個資料夾可能也裝了較新的 SDK；匯出期間暫時放 global.json，固定用 9.0 版。
GLOBAL="$ROOT/global.json"
[ ! -e "$GLOBAL" ] || { echo "專案裡已經有 global.json，為了不覆蓋它而停止。" >&2; exit 1; }
printf '{ "sdk": { "version": "9.0.100", "rollForward": "latestFeature" } }\n' > "$GLOBAL"

# 在 iCloud 不同步的暫存資料夾打包：專案放在由 iCloud 同步的「文件」裡時，打包中刪除又建立的
# 檔案會被複製成「檔名 2」的衝突副本，跟著進 zip。
STAGE=$(mktemp -d /tmp/uwrelease.XXXXXX)
trap 'rm -f "$GLOBAL"; rm -rf "$STAGE"' EXIT
WIN="$STAGE/$NAME-windows-x64"
MAC="$STAGE/$NAME-macos"
mkdir -p "$WIN" "$MAC" "$ROOT/build" "$OUT"
# build/ 在專案裡，不讓 Godot 把打包結果當成專案檔案匯入。
touch "$ROOT/build/.gdignore"
cd "$ROOT"

# 程式用 System.IO 讀譯文、中文字型與 SoundFont，匯出的 .pck 裡讀不到，所以當成一般資料夾附上（見 src/utility/apppaths.cs）。
copy_data() {
    rsync -a --exclude .DS_Store --exclude .gdignore i18n soundfonts "$1/"
}

# 給玩家放 MT-32 ROM 的資料夾（ROM 要自備，不隨附）。
make_roms_dir() {
    mkdir -p "$1/ROMS"
    printf '%s\n' "把 MT-32 或 CM-32L 的 ROM 放在這裡（例如 MT32_CONTROL.ROM、MT32_PCM.ROM），" \
        "或在啟動畫面的「MT-32／CM-32L ROM 目錄」按「瀏覽…」改選其他資料夾。ROM 需自備，本程式不附。" > "$1/ROMS/README.txt"
}

copy_docs() {
    cp README.md README.en.md LICENSE.md packaging/THIRD-PARTY-NOTICES.txt "$1/"
}

"$GODOT" --headless --path . --export-release UnderworldWindows "$WIN/Underworld.exe"
copy_data "$WIN"
make_roms_dir "$WIN"
copy_docs "$WIN"
cp packaging/INSTALL-windows.txt "$WIN/INSTALL.txt"

"$GODOT" --headless --path . --export-release UnderworldMacOS "$MAC/Underworld.app"
copy_data "$MAC/Underworld.app/Contents/Resources"
make_roms_dir "$MAC"
copy_docs "$MAC"
cp packaging/INSTALL-macos.txt "$MAC/INSTALL.txt"
# NuGet 套件附的 OPL／MT-32 函式庫只有 Apple Silicon 版，而且要求 macOS 26／15；換成自己編譯的
# universal 版（最低 macOS 11，見 packaging/native/build-macos.sh），Intel Mac 也放一份。
for arch in arm64 x86_64; do
    cp packaging/native/macos/libadlmidi.dylib packaging/native/macos/libmt32emu.dylib \
        "$MAC/Underworld.app/Contents/Resources/data_Underworld_macos_$arch/"
done

# 不帶除錯符號（裡面有建置這台電腦的路徑）。
find "$WIN" "$MAC" -name "*.pdb" -delete

# 檢查：不能依賴 Homebrew（別台電腦沒有），也不能留下這台電腦的使用者路徑。
bad=0
for lib in $(find "$MAC" -name "*.dylib") "$MAC/Underworld.app/Contents/MacOS/Underworld"; do
    if otool -L "$lib" | tail -n +2 | grep -q "/opt/homebrew/\|/usr/local/opt/\|/usr/local/Cellar/"; then
        echo "依賴 Homebrew：$lib" >&2; bad=1
    fi
done
if grep -rlF "$HOME" "$WIN" "$MAC" >&2; then
    echo "上面的檔案含有這台電腦的路徑（$HOME）。" >&2; bad=1
fi
[ "$bad" = 0 ] || { echo "打包內容不能給其他電腦使用，停止。" >&2; exit 1; }

# 加了檔案後原本的簽章就失效了；Apple Silicon 不執行沒有簽章的程式，所以重新做 ad-hoc 簽章。
xattr -cr "$MAC"
codesign --force --deep --sign - "$MAC/Underworld.app"
codesign --verify --deep --strict "$MAC/Underworld.app"

(cd "$STAGE" && ditto -c -k --norsrc --noextattr --keepParent "$(basename "$MAC")" "$(basename "$MAC").zip")
(cd "$STAGE" && ditto -c -k --norsrc --noextattr --keepParent "$(basename "$WIN")" "$(basename "$WIN").zip")
cp "$STAGE"/*.zip "$OUT/"
ls -l "$OUT/$NAME"-*.zip
