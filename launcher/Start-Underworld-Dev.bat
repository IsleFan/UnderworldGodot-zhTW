@echo off
rem Ultima Underworld dev launcher for Windows: double-click to build the C# and run this
rem source tree with the local Godot 4.3 .NET.
rem   Godot: the GODOT environment variable, else Godot_v4.3-stable_mono_win64.exe (any
rem          Godot*mono*win64.exe) under the godot folder beside the project or inside it.
rem   .NET : the .NET 9 SDK on PATH (the Microsoft installer puts dotnet there).
rem Messages are in English: cmd reads a batch file in the console code page.
setlocal
set "PROJECT=%~dp0.."
for %%I in ("%PROJECT%") do set "PROJECT=%%~fI"
if not exist "%PROJECT%\project.godot" (
  echo Cannot find the UnderworldGodot project. Keep this file in its launcher folder.
  goto :fail
)

if defined GODOT goto :havegodot
rem FOR /R cannot take a FOR variable as its folder, so the search is a subroutine.
for %%D in ("%PROJECT%\..\godot" "%PROJECT%\godot") do if exist "%%~D" call :findgodot "%%~D"
:havegodot
if not defined GODOT goto :nogodot
if not exist "%GODOT%" goto :nogodot

where dotnet >nul 2>nul
if errorlevel 1 (
  echo Cannot find the .NET 9 SDK. Install it from https://dotnet.microsoft.com/download
  goto :fail
)

echo Building...
dotnet build "%PROJECT%\Underworld.csproj" -nologo -v q
if errorlevel 1 (
  echo.
  echo The build failed. See the errors above.
  goto :fail
)

start "" "%GODOT%" --path "%PROJECT%"
exit /b 0

:findgodot
for /r "%~1" %%G in (Godot*mono*win64.exe) do set "GODOT=%%G"
exit /b 0

:nogodot
echo Cannot find Godot 4.3 .NET (Godot_v4.3-stable_mono_win64.exe).
echo Put it in a folder named godot beside the project, or set the GODOT environment variable.
:fail
echo.
pause
exit /b 1
