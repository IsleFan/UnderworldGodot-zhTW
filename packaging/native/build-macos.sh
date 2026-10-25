#!/bin/sh
# 重新編譯 macOS 用的原生函式庫：libadlmidi（OPL 配樂與 UW1 音效）與 libmt32emu（MT-32 配樂）。
# 用法：sh packaging/native/build-macos.sh   （需要 Xcode 命令列工具、cmake、git）
# 輸出：packaging/native/macos/libadlmidi.dylib、libmt32emu.dylib（arm64＋x86_64，最低 macOS 11）
#
# 為什麼不用 NuGet 套件裡的：ADLMidi.NET 1.2.0 附的 dylib 要求 macOS 26、Munt.NET 0.2.2 的要求 macOS 15，
# 而且都只有 arm64；較舊的 macOS 與 Intel Mac 會沒有 OPL／MT-32。版本與編譯參數照兩個套件的說明
# （libADLMIDI v1.6.2、munt libmt32emu_2_8_3），只另外指定架構與最低系統版本。package.sh 匯出後
# 用這兩個檔案取代匯出結果裡的同名檔案。
set -e
OUT=$(cd "$(dirname "$0")" && pwd)/macos
WORK=$(mktemp -d /tmp/uwnative.XXXXXX)
trap 'rm -rf "$WORK"' EXIT
COMMON="-DCMAKE_BUILD_TYPE=Release -DCMAKE_OSX_ARCHITECTURES=arm64;x86_64 -DCMAKE_OSX_DEPLOYMENT_TARGET=11.0"

git clone --quiet --depth 1 --branch v1.6.2 https://github.com/Wohlstand/libADLMIDI.git "$WORK/libADLMIDI"
cmake -S "$WORK/libADLMIDI" -B "$WORK/adl" $COMMON \
    -DlibADLMIDI_STATIC=OFF -DlibADLMIDI_SHARED=ON -DWITH_EMBEDDED_BANKS=OFF -DWITH_MUS_SUPPORT=OFF \
    -DWITH_XMI_SUPPORT=ON -DUSE_DOSBOX_EMULATOR=OFF -DUSE_NUKED_EMULATOR=ON -DUSE_OPAL_EMULATOR=OFF \
    -DUSE_JAVA_EMULATOR=OFF -DUSE_ESFMU_EMULATOR=OFF -DUSE_MAME_EMULATOR=OFF -DUSE_YMFM_EMULATOR=OFF >/dev/null
cmake --build "$WORK/adl" --config Release -j 8 >/dev/null
cp -L "$WORK/adl/libADLMIDI.dylib" "$OUT/libadlmidi.dylib"

git clone --quiet --depth 1 --branch libmt32emu_2_8_3 https://github.com/munt/munt.git "$WORK/munt"
cmake -S "$WORK/munt/mt32emu" -B "$WORK/mt" $COMMON -Dlibmt32emu_SHARED=ON >/dev/null
cmake --build "$WORK/mt" --config Release -j 8 >/dev/null
cp -L "$WORK/mt/libmt32emu.dylib" "$OUT/libmt32emu.dylib"

for f in "$OUT"/*.dylib; do
    codesign --force --sign - "$f"
    echo "$(basename "$f"): $(lipo -archs "$f"), minos $(vtool -show-build "$f" | awk '/minos/ { print $2; exit }')"
done
