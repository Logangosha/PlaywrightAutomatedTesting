#requires -Version 5
<#
  Build-and-launch engine for the Runner UI. Building the UI project also rebuilds the
  referenced runner/test project and refreshes the assembly the UI loads, so tests are
  always current. Used two ways, both through this one script:

    - Open the app (launch.bat / the desktop shortcut, via launch-hidden.vbs): no args,
      runs with no visible window — build, then launch. Always up to date on open.
    - In-app "Restart & rebuild" (author loop): -WaitForPid <app pid> — wait for the app
      to close so its DLLs unlock, then build and relaunch. Runs in a visible console
      (AppRestarter launches it that way) so the user can watch/read it.

  Build failures are written to logs\build-error.log (cleared on the next successful
  build). The app itself reads that file on startup and shows a banner — this is how a
  failure surfaces when this script ran with no visible window at all. The one case that
  can't wait for the app (no previous build exists to open) pops a native error dialog.
#>
param([int]$WaitForPid = 0)

$root = Split-Path $PSScriptRoot -Parent   # scripts/ -> repo root
$logsDir = Join-Path $root 'logs'
$errorLog = Join-Path $logsDir 'build-error.log'
$interactive = $WaitForPid -gt 0            # true only for the visible, in-app-triggered path

function Find-Exe {
    Get-ChildItem -Path (Join-Path $root 'ui\RunnerUI\bin') -Filter 'RunnerUI.exe' -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

function Show-Error([string]$message) {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show(
        $message, "Playwright Automation Tool",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
}

if ($interactive) {
    Write-Host "Waiting for the app to close (pid $WaitForPid)..." -ForegroundColor Cyan
    try { Wait-Process -Id $WaitForPid -Timeout 60 -ErrorAction SilentlyContinue } catch { }
}

New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
if (Test-Path $errorLog) { Remove-Item $errorLog -Force -ErrorAction SilentlyContinue }

if ($interactive) { Write-Host "Building (so tests are current)..." -ForegroundColor Cyan }
$buildLines = & dotnet build (Join-Path $root 'ui\RunnerUI\RunnerUI.csproj') -v minimal 2>&1
$buildExit = $LASTEXITCODE
if ($interactive) { $buildLines | ForEach-Object { Write-Host $_ } }

$exe = Find-Exe

if ($buildExit -ne 0) {
    $buildLines | Out-String | Set-Content -Path $errorLog -Encoding utf8

    if ($interactive) {
        Write-Host ""
        Write-Host "Build FAILED (exit $buildExit). Your previous build is unchanged." -ForegroundColor Red
        if ($null -ne $exe) {
            $ans = Read-Host "Launch the previous build anyway? (y/N)"
            if ($ans -match '^(y|yes)$') { Start-Process -FilePath $exe.FullName }
        } else {
            Read-Host "No previous build found. Press Enter to close"
        }
    } elseif ($null -ne $exe) {
        # Silent path: just open the previous good build. It reads logs\build-error.log
        # on startup and shows a banner, so the failure isn't silent — it's in the app.
        Start-Process -FilePath $exe.FullName
    } else {
        # Nothing has ever built successfully — there's no app to show a banner in, so
        # this is the one case a hidden run has to surface directly.
        Show-Error "The build failed, so there's nothing to open yet.`n`nSee $errorLog for details."
    }
    exit $buildExit
}

if ($null -eq $exe) {
    $msg = "Build succeeded but RunnerUI.exe wasn't found under ui\RunnerUI\bin."
    if ($interactive) { Write-Host $msg -ForegroundColor Red; Read-Host "Press Enter to close" }
    else { Show-Error $msg }
    exit 1
}

Start-Process -FilePath $exe.FullName
