-- Ultima Underworld 開發版：macOS 啟動程式的外殼。
-- 用 AppleScript 程式（applet）當外殼，是因為專案常放在「文件」裡：macOS 只會替簽過章的程式
-- 跳出「是否允許取用文件資料夾」的詢問，用 bash 當主程式的 .app 會被直接擋下、也不詢問。
-- 實際工作在 Contents/Resources/launch.sh。改了這個檔要重新產生 .app：
--   osacompile -o "Ultima Underworld 開發版.app" launcher.applescript
--   再把 launch.sh 複製進 Contents/Resources（見 launch.sh 開頭）。
on run
	set launcher to POSIX path of (path to me) & "Contents/Resources/launch.sh"
	-- 放到背景執行：遊戲開著的時候，這個外殼不用一直掛著。
	do shell script "/bin/bash " & quoted form of launcher & " > /dev/null 2>&1 &"
end run
