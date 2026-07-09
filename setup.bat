@echo off
REM One-time setup on a new machine: installs the .NET 8 SDK (if needed), builds,
REM installs the browser, and adds a desktop shortcut. Double-click to run.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\setup.ps1"
