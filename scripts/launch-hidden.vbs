' Launches rebuild.ps1 with zero visible window (not even a console flash).
' This is what the desktop shortcut and launch.bat point to for the everyday
' "open the app" flow. Errors are surfaced in the app itself (see BuildStatus /
' the banner in MainLayout.razor) — not on a terminal, since there isn't one.
Dim fso, shell, scriptDir
Set fso = CreateObject("Scripting.FileSystemObject")
scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
Set shell = CreateObject("WScript.Shell")
shell.Run "powershell -NoProfile -ExecutionPolicy Bypass -File """ & scriptDir & "\rebuild.ps1""", 0, False
