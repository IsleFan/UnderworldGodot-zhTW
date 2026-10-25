#!/bin/bash
# Ultima Underworld 開發版：按兩下就用本機的 Godot 4.3 .NET 執行這份原始碼（先編譯最新的 C#）。
# 由 .app 的 AppleScript 外殼（launcher/launcher.applescript）呼叫；這份放在 .app 的
# Contents/Resources/launch.sh，原始的也只有這一份。
# 從 Finder 開啟的程式拿不到終端機的環境變數，所以 Godot 與 .NET 都在這裡自己找。
#
# 找專案：從這個 .app 往上找 project.godot（放在 repo 的 launcher 資料夾裡時）；
#         .app 被搬到別處時，用上次成功啟動時記下的位置。
# 找 Godot：環境變數 GODOT，或專案旁邊的 godot/、「應用程式」裡名稱有 mono 的 Godot*.app。
# 找 .NET：Homebrew 的 dotnet@9、dotnet，或微軟安裝程式的 /usr/local/share/dotnet。
# 記錄檔：~/Library/Logs/UnderworldGodot-launcher.log

LOG="$HOME/Library/Logs/UnderworldGodot-launcher.log"
REMEMBER="$HOME/Library/Application Support/Godot/app_userdata/Underworld/devproject.txt"

fail() {
    osascript -e 'on run argv' -e 'display alert "Ultima Underworld 開發版" message (item 1 of argv) as critical' -e 'end run' "$1" >/dev/null
    exit 1
}

# 1. 專案
PROJECT=
dir=$(cd "$(dirname "$0")" && pwd)
while [ "$dir" != "/" ]; do
    if [ -f "$dir/project.godot" ] && [ -f "$dir/Underworld.csproj" ]; then PROJECT=$dir; break; fi
    dir=$(dirname "$dir")
done
if [ -z "$PROJECT" ] && [ -f "$REMEMBER" ]; then
    PROJECT=$(cat "$REMEMBER")
    [ -f "$PROJECT/project.godot" ] || PROJECT=
fi
[ -n "$PROJECT" ] || fail "找不到 UnderworldGodot 專案。請從專案裡的 launcher 資料夾開啟這個程式一次，之後搬到別處也能用。"

# 2. Godot 4.3 .NET
GODOT_BIN=${GODOT:-}
if [ -z "$GODOT_BIN" ]; then
    for app in "$PROJECT/../godot"/Godot*.app "$PROJECT/godot"/Godot*.app /Applications/Godot*mono*.app /Applications/Godot*.app "$HOME/Applications"/Godot*mono*.app; do
        if [ -x "$app/Contents/MacOS/Godot" ] && [ -d "$app/Contents/Resources/GodotSharp" ]; then GODOT_BIN="$app/Contents/MacOS/Godot"; break; fi
    done
fi
[ -x "$GODOT_BIN" ] || fail "找不到 Godot 4.3 .NET 版。請把 Godot_mono.app 放在專案旁邊的 godot 資料夾或「應用程式」裡。"

# 3. .NET 9 SDK
DOTNET_ROOT=${DOTNET_ROOT:-}
if [ -z "$DOTNET_ROOT" ]; then
    for d in /opt/homebrew/opt/dotnet@9/libexec /opt/homebrew/opt/dotnet/libexec /usr/local/opt/dotnet@9/libexec /usr/local/share/dotnet; do
        if [ -x "$d/dotnet" ]; then DOTNET_ROOT=$d; break; fi
    done
fi
[ -x "$DOTNET_ROOT/dotnet" ] || fail "找不到 .NET 9 SDK。請安裝（例如 brew install dotnet@9）。"
export DOTNET_ROOT PATH="$DOTNET_ROOT:$PATH"

mkdir -p "$(dirname "$LOG")" "$(dirname "$REMEMBER")"
echo "== $(date) $PROJECT" > "$LOG"

# 4. 編譯（沒有改動時幾秒就好），失敗就停下來
if ! "$DOTNET_ROOT/dotnet" build "$PROJECT/Underworld.csproj" -nologo -v q >> "$LOG" 2>&1; then
    open -a Console "$LOG" 2>/dev/null
    fail "程式編譯失敗，錯誤訊息在 $LOG。"
fi
echo "$PROJECT" > "$REMEMBER"

# 5. 開遊戲（先進啟動畫面）
exec "$GODOT_BIN" --path "$PROJECT" >> "$LOG" 2>&1
