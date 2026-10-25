@echo off
rem UnderworldGodot-zhTW one-click install: runs install.ps1 in this folder.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
if errorlevel 1 (
  echo.
  echo Install failed. See the message above, or follow INSTALL.txt.
)
pause
