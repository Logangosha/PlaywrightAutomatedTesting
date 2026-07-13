@echo off
REM Open the Runner UI with no visible console. Builds first so tests are always
REM current, then launches. This is what the desktop shortcut points to.
start "" wscript.exe "%~dp0scripts\launch-hidden.vbs"
