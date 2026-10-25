# UnderworldGodot-zhTW 一鍵安裝（Windows）
#
# 用法：在解壓縮出來的資料夾裡按兩下 INSTALL.bat（它會用 PowerShell 執行這個檔案）。
#
# 會做的事：
#   1. 解除下載封鎖（讓 Windows 不再擋下 Underworld.exe）
#   2. 沒有安裝時，下載並安裝 Microsoft Visual C++ 可轉散發套件（x64）（MT-32 配樂需要，會跳出系統管理員確認）
#   3. 從 GOG 版取出遊戲檔案到 %USERPROFILE%\Games\UWGOG\UW1、UW2，並取消唯讀
#      （缺哪個補哪個；已經存在的資料夾不覆蓋，裡面可能有存檔）
#   4. 在遊戲設定寫入遊戲目錄，語言設為繁體中文
#   5. 打開遊戲
#
# 可用參數：-GamesDir 遊戲檔案要放的資料夾；-GogDir GOG 版的安裝資料夾（預設自動尋找）
#
# GOG 的《Ultima Underworld I》與《II》附的是同一張光碟映像（game.gog），裡面同時有 UW 與 UW2 兩個
# 資料夾（macOS 版實測兩者 md5 相同），所以只要裝了其中一個，兩個遊戲都能取出。
param(
    [string]$GamesDir = (Join-Path $env:USERPROFILE 'Games\UWGOG'),
    [string]$GogDir = ''
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $here 'Underworld.exe'
$settingsDir = Join-Path $env:APPDATA 'Godot\app_userdata\Underworld'
$settings = Join-Path $settingsDir 'settings.json'

if (-not (Test-Path $exe)) {
    throw "找不到 $exe。請把 install.ps1 留在解壓縮出來的資料夾裡，和 Underworld.exe 放在一起。"
}

Write-Host '== 1/5 解除下載封鎖'
Get-ChildItem -Path $here -Recurse -File | Unblock-File

Write-Host '== 2/5 檢查 Visual C++ 可轉散發套件'
function Test-VcRedist {
    foreach ($key in 'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64',
                     'HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64') {
        $v = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
        if ($v -and $v.Installed -eq 1) { return $true }
    }
    return $false
}
if (Test-VcRedist) {
    Write-Host '已安裝，略過。'
} else {
    $installer = Join-Path $env:TEMP 'vc_redist.x64.exe'
    Write-Host '下載微軟官方安裝程式…'
    Invoke-WebRequest -Uri 'https://aka.ms/vs/17/release/vc_redist.x64.exe' -OutFile $installer -UseBasicParsing
    Write-Host '安裝中（請在系統管理員確認視窗按「是」）…'
    $p = Start-Process -FilePath $installer -ArgumentList '/install', '/quiet', '/norestart' -Verb RunAs -Wait -PassThru
    # 0 成功；3010 成功但建議重新開機；1638 已有較新版本
    if ($p.ExitCode -notin 0, 3010, 1638) {
        Write-Warning "Visual C++ 可轉散發套件安裝失敗（代碼 $($p.ExitCode)）。MT-32 配樂可能無法使用，其他功能不受影響。"
    }
    Remove-Item $installer -ErrorAction SilentlyContinue
}

Write-Host '== 3/5 準備遊戲檔案'
function Test-Game([string]$dir, [string]$exeName) {
    (Test-Path (Join-Path $dir $exeName)) -and (Test-Path (Join-Path $dir 'DATA')) -and (Test-Path (Join-Path $dir 'SOUND'))
}
function Find-GogDirs {
    if ($GogDir) { return @($GogDir) }
    $dirs = @()
    # GOG Galaxy 或安裝程式安裝的遊戲都記在這裡（UW1、UW2 各一筆，名稱都有 Underworld）。
    foreach ($key in 'HKLM:\SOFTWARE\WOW6432Node\GOG.com\Games\*', 'HKLM:\SOFTWARE\GOG.com\Games\*') {
        Get-ItemProperty -Path $key -ErrorAction SilentlyContinue |
            Where-Object { $_.gameName -like '*Underworld*' -and $_.path } |
            ForEach-Object { $dirs += $_.path }
    }
    foreach ($root in 'C:\GOG Games', "${env:ProgramFiles(x86)}\GOG Galaxy\Games", "$env:ProgramFiles\GOG Galaxy\Games") {
        if (Test-Path $root) {
            Get-ChildItem -Path $root -Directory -Filter '*Underworld*' -ErrorAction SilentlyContinue |
                ForEach-Object { $dirs += $_.FullName }
        }
    }
    return $dirs | Select-Object -Unique
}
function Clear-ReadOnly([string]$dir) {
    Get-ChildItem -Path $dir -Recurse -File | ForEach-Object { $_.IsReadOnly = $false }
}

$uw1 = Join-Path $GamesDir 'UW1'
$uw2 = Join-Path $GamesDir 'UW2'
# 每個遊戲：目的地、執行檔、安裝資料夾裡可能放現成檔案的子資料夾、光碟上的資料夾
$games = @(
    @{ Name = 'UW1'; Dest = $uw1; Exe = 'UW.EXE'; Subs = @('', 'UW', 'UW1'); Disc = 'UW' },
    @{ Name = 'UW2'; Dest = $uw2; Exe = 'UW2.EXE'; Subs = @('', 'UW2'); Disc = 'UW2' }
)
function Get-Needed { @($games | Where-Object { -not (Test-Game $_.Dest $_.Exe) }) }
# 目的地已經存在（即使不完整）就不覆蓋，免得蓋掉裡面的檔案。
function Test-Free($game) {
    if (Test-Path $game.Dest) {
        Write-Warning "$($game.Dest) 已經存在但不是完整的遊戲目錄，為了保護裡面的檔案不覆蓋，請自行檢查。"
        return $false
    }
    return $true
}
function Copy-Game($game, [string]$source) {
    New-Item -ItemType Directory -Force -Path $GamesDir | Out-Null
    Copy-Item -Path $source -Destination $game.Dest -Recurse
    # 光碟上的檔案是唯讀的，不改就無法存檔。
    Clear-ReadOnly $game.Dest
    Write-Host "已複製到 $($game.Dest)"
}

$needed = @(Get-Needed)  # @() 包住：只剩一個時函式回傳值會被攤開成單一個雜湊表
if ($needed.Count -eq 0) {
    Write-Host "已經有 $uw1 與 $uw2，略過。"
} else {
    $needed = @($needed | Where-Object { Test-Free $_ })
    foreach ($dir in Find-GogDirs) {
        if ($needed.Count -eq 0) { break }
        # 安裝資料夾裡已經有現成的遊戲檔案
        foreach ($game in $needed) {
            foreach ($sub in $game.Subs) {
                $candidate = if ($sub) { Join-Path $dir $sub } else { $dir }
                if (-not (Test-Path $game.Dest) -and (Test-Game $candidate $game.Exe)) {
                    Copy-Game $game $candidate
                }
            }
        }
        $needed = @($needed | Where-Object { -not (Test-Path $_.Dest) })
        if ($needed.Count -eq 0) { break }
        # GOG 版通常把遊戲包在 game.gog 光碟映像裡
        $image = Get-ChildItem -Path $dir -Filter 'game.gog' -Recurse -Depth 2 -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($image) {
            Write-Host "從 $($image.FullName) 取出遊戲檔案…"
            $iso = Join-Path $env:TEMP 'uw-gog.iso'
            Copy-Item -Path $image.FullName -Destination $iso -Force
            $mounted = Mount-DiskImage -ImagePath $iso -PassThru
            try {
                $drive = ($mounted | Get-Volume).DriveLetter + ':\'
                foreach ($game in $needed) {
                    $source = Join-Path $drive $game.Disc
                    if (Test-Path $source) { Copy-Game $game $source }
                }
            } finally {
                Dismount-DiskImage -ImagePath $iso | Out-Null
                Remove-Item $iso -ErrorAction SilentlyContinue
            }
            $needed = @($needed | Where-Object { -not (Test-Path $_.Dest) })
        }
    }
    foreach ($game in $needed) {
        Write-Warning "沒有取得 $($game.Name) 的遊戲檔案。"
    }
}
if (-not (Test-Game $uw1 'UW.EXE') -and -not (Test-Game $uw2 'UW2.EXE')) {
    throw '找不到 GOG 版《Ultima Underworld》或《Ultima Underworld II》。請先從 GOG 安裝，或用 -GogDir 指定安裝資料夾，或照 INSTALL.txt「二、準備遊戲檔案」手動處理。'
}

Write-Host '== 4/5 寫入遊戲設定'
New-Item -ItemType Directory -Force -Path $settingsDir | Out-Null
if (Test-Path $settings) {
    $json = Get-Content -Path $settings -Raw -Encoding UTF8 | ConvertFrom-Json
} else {
    $json = [pscustomobject]@{ gametoload = $(if (Test-Game $uw1 'UW.EXE') { 'UW1' } else { 'UW2' }) }
}
if (Test-Game $uw1 'UW.EXE') {
    $json | Add-Member -NotePropertyName pathuw1 -NotePropertyValue $uw1 -Force
}
if (Test-Game $uw2 'UW2.EXE') {
    $json | Add-Member -NotePropertyName pathuw2 -NotePropertyValue $uw2 -Force
}
$json | Add-Member -NotePropertyName language -NotePropertyValue 'zh-TW' -Force
# 不帶 BOM 的 UTF-8
[IO.File]::WriteAllText($settings, ($json | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding $false))
Write-Host "設定檔：$settings"

Write-Host '== 5/5 完成，打開遊戲'
Write-Host "MT-32 配樂需要自備 ROM，放進 $(Join-Path $here 'ROMS')（見 INSTALL.txt「五、MT-32 配樂」）。"
Start-Process -FilePath $exe -WorkingDirectory $here
