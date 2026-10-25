#!/bin/bash
# UnderworldGodot-zhTW 一鍵安裝（macOS）
#
# 用法：打開「終端機」，輸入 bash 加一個空格，把這個檔案從 Finder 拖進視窗，按 Return。
#
# 會做的事：
#   1. 解除 Underworld.app 的安全性封鎖（本程式沒有經過 Apple 公證）
#   2. 從 GOG 版的 app 取出遊戲光碟映像，把 UW、UW2 複製到 ~/Games/UWGOG/UW1、UW2，並設為可寫入
#      （缺哪個補哪個；已經存在的資料夾不覆蓋，裡面可能有存檔）
#   3. 在遊戲設定寫入遊戲目錄，語言設為繁體中文
#   4. 打開遊戲
#
# 可用環境變數：
#   UW_GAMES     遊戲檔案要放的資料夾（預設 ~/Games/UWGOG）
#   UW_GOG_APP   GOG 版 app 的位置（預設在「應用程式」裡自動尋找）
#
# GOG 的《Ultima Underworld I》與《II》附的是同一張光碟映像（game.gog，兩者 md5 相同），裡面同時有
# UW 與 UW2 兩個資料夾，所以只要裝了其中一個，兩個遊戲都能取出。
set -e

HERE=$(cd "$(dirname "$0")" && pwd)
APP="$HERE/Underworld.app"
DEST=${UW_GAMES:-$HOME/Games/UWGOG}
SETTINGS_DIR="$HOME/Library/Application Support/Godot/app_userdata/Underworld"
SETTINGS="$SETTINGS_DIR/settings.json"

if [ ! -d "$APP" ]; then
    echo "找不到 $APP。請把 install.sh 留在解壓縮出來的資料夾裡，和 Underworld.app 放在一起。" >&2
    exit 1
fi

echo "== 1/4 解除安全性封鎖"
xattr -dr com.apple.quarantine "$APP" 2>/dev/null || true

echo "== 2/4 準備遊戲檔案"
has_game() { [ -f "$1/$2" ] && [ -d "$1/DATA" ] && [ -d "$1/SOUND" ]; }

find_gog_image() {
    if [ -n "$UW_GOG_APP" ]; then
        echo "$UW_GOG_APP/Contents/Resources/game/game.gog"
        return
    fi
    for app in /Applications/*Underworld*.app "$HOME/Applications"/*Underworld*.app; do
        if [ -f "$app/Contents/Resources/game/game.gog" ]; then
            echo "$app/Contents/Resources/game/game.gog"
            return
        fi
    done
}

need=
has_game "$DEST/UW1" UW.EXE || need="$need UW1"
has_game "$DEST/UW2" UW2.EXE || need="$need UW2"
if [ -z "$need" ]; then
    echo "已經有 $DEST/UW1 與 $DEST/UW2，略過。"
else
    IMAGE=$(find_gog_image)
    if [ -z "$IMAGE" ] || [ ! -f "$IMAGE" ]; then
        echo "在「應用程式」裡找不到 GOG 版《Ultima Underworld》或《Ultima Underworld II》。" >&2
        echo "請先從 GOG 安裝 macOS 版，或照 INSTALL.txt「一、準備遊戲檔案」手動取出。" >&2
    else
        echo "從 $IMAGE 取出遊戲檔案…"
        TMP=$(mktemp -d)
        trap 'hdiutil detach "$MOUNT" -quiet 2>/dev/null; rm -rf "$TMP"' EXIT
        cp "$IMAGE" "$TMP/uw.iso"
        MOUNT=$(hdiutil attach -readonly -nobrowse -mountrandom "$TMP" "$TMP/uw.iso" | awk -F'\t' 'NF > 2 { print $NF }' | tail -1)
        mkdir -p "$DEST"
        # copy_game 遊戲 光碟上的資料夾 執行檔
        copy_game() {
            local dst="$DEST/$1"
            if has_game "$dst" "$3"; then
                return
            elif [ -e "$dst" ]; then
                echo "$dst 已經存在但不是完整的遊戲目錄，為了保護裡面的檔案不覆蓋，請自行檢查。" >&2
            elif [ -d "$MOUNT/$2" ]; then
                ditto "$MOUNT/$2" "$dst"
                # 光碟上的檔案是唯讀的，不改就無法存檔。
                chmod -R u+w "$dst"
                echo "已複製到 $dst"
            else
                echo "光碟映像裡沒有 $2 資料夾，無法取出 $1。" >&2
            fi
        }
        copy_game UW1 UW UW.EXE
        copy_game UW2 UW2 UW2.EXE
        hdiutil detach "$MOUNT" -quiet
        MOUNT=
    fi
fi
UW1PATH=""; UW2PATH=""
has_game "$DEST/UW1" UW.EXE && UW1PATH="$DEST/UW1"
has_game "$DEST/UW2" UW2.EXE && UW2PATH="$DEST/UW2"
if [ -z "$UW1PATH$UW2PATH" ]; then
    echo "沒有可用的遊戲檔案，無法繼續。請照 INSTALL.txt「一、準備遊戲檔案」手動處理。" >&2
    exit 1
fi

echo "== 3/4 寫入遊戲設定"
mkdir -p "$SETTINGS_DIR"
if [ -f "$SETTINGS" ]; then
    # 已有設定：只改找到的遊戲目錄與語言，其他保留。plutil 會照原本的 JSON 格式寫回。
    [ -n "$UW1PATH" ] && plutil -replace pathuw1 -string "$UW1PATH" "$SETTINGS"
    [ -n "$UW2PATH" ] && plutil -replace pathuw2 -string "$UW2PATH" "$SETTINGS"
    plutil -replace language -string "zh-TW" "$SETTINGS"
else
    GAME=UW1
    [ -z "$UW1PATH" ] && GAME=UW2
    cat > "$SETTINGS" <<EOF
{
  "pathuw1": "$UW1PATH",
  "pathuw2": "$UW2PATH",
  "gametoload": "$GAME",
  "language": "zh-TW"
}
EOF
fi
echo "設定檔：$SETTINGS"

echo "== 4/4 完成，打開遊戲"
echo "MT-32 配樂需要自備 ROM，放進 $HERE/ROMS（見 INSTALL.txt「五、MT-32 配樂」）。"
open "$APP"
