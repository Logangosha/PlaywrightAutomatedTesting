#requires -Version 5
<#
  One-time setup for a fresh machine. Idempotent — safe to re-run.
    1. Ensure the .NET 8 SDK (installs via winget if missing).
    2. Ensure the WebView2 runtime (the UI renders in it).
    3. Build the app (which builds the runner + tests, and restores NuGet packages
       including the Playwright .NET library).
    4. Install the Playwright Chromium browser (the binaries aren't bundled).
    5. Create a "Playwright Automation Tool" desktop shortcut to launch.bat.
  Then it opens the app itself and closes this window. Next time, use the shortcut.
#>
$root = Split-Path $PSScriptRoot -Parent   # scripts/ -> repo root

Write-Host "== Playwright Automation Tool setup ==" -ForegroundColor Cyan

function Test-Sdk8 {
    try { return [bool](& dotnet --list-sdks 2>$null | Where-Object { $_ -match '^8\.' }) }
    catch { return $false }
}

function Test-WebView2 {
    # Evergreen WebView2 Runtime registers under this GUID; a non-zero 'pv' means installed.
    $guid = '{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
    $keys = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\$guid",
        "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$guid",
        "HKCU:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$guid"
    )
    foreach ($k in $keys) {
        try {
            $pv = (Get-ItemProperty -Path $k -Name pv -ErrorAction Stop).pv
            if ($pv -and $pv -ne '0.0.0.0') { return $true }
        } catch { }
    }
    return $false
}

# 1. .NET 8 SDK
if (Test-Sdk8) {
    Write-Host "[1/5] .NET 8 SDK found." -ForegroundColor Green
} else {
    Write-Host "[1/5] .NET 8 SDK not found - installing..." -ForegroundColor Yellow
    if (Get-Command winget -ErrorAction SilentlyContinue) {
        winget install --id Microsoft.DotNet.SDK.8 -e --accept-source-agreements --accept-package-agreements
        # Refresh PATH so 'dotnet' is usable in THIS session (avoids "open a new terminal").
        $env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
                    [Environment]::GetEnvironmentVariable('Path', 'User')
    } else {
        Write-Host "winget isn't available. Install the .NET 8 SDK manually, then re-run setup:" -ForegroundColor Yellow
        Write-Host "  https://dotnet.microsoft.com/download/dotnet/8.0"
        Read-Host "Press Enter to close"; exit 1
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Host "dotnet still isn't on PATH. Open a NEW terminal and re-run setup.bat." -ForegroundColor Red
        Read-Host "Press Enter to close"; exit 1
    }
}

# 2. WebView2 runtime (preinstalled on modern Windows). Check the registry first so we
#    don't sit through a slow winget resolve when it's already there.
Write-Host "[2/5] Checking WebView2 runtime..." -ForegroundColor Cyan
if (Test-WebView2) {
    Write-Host "  Found." -ForegroundColor Green
} elseif (Get-Command winget -ErrorAction SilentlyContinue) {
    Write-Host "  Not found - installing..." -ForegroundColor Yellow
    winget install --id Microsoft.EdgeWebView2Runtime -e --accept-source-agreements --accept-package-agreements
} else {
    Write-Host "  Not found and winget unavailable - if the app window is blank, install from:" -ForegroundColor Yellow
    Write-Host "  https://developer.microsoft.com/microsoft-edge/webview2/"
}

# 3. Build (also restores NuGet, incl. the Playwright .NET library)
Write-Host "[3/5] Building..." -ForegroundColor Cyan
& dotnet build (Join-Path $root 'ui\RunnerUI\RunnerUI.csproj') -v minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed - see errors above." -ForegroundColor Red
    Read-Host "Press Enter to close"; exit 1
}

# 4. Playwright Chromium browser (the binaries are not bundled)
Write-Host "[4/5] Installing Chromium for Playwright..." -ForegroundColor Cyan
$pw = Join-Path $root 'bin\Debug\net8.0\playwright.ps1'
if (Test-Path $pw) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $pw install chromium
} else {
    Write-Host "  playwright.ps1 not found (expected after build) - skipping." -ForegroundColor Yellow
}

# 5. Desktop shortcut -> launch-hidden.vbs (via wscript, so opening the app never
#    flashes a console window; launch.bat is the same thing for manual double-clicks).
Write-Host "[5/5] Creating desktop shortcut..." -ForegroundColor Cyan
try {
    $lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Playwright Automation Tool.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($lnk)
    $shortcut.TargetPath = Join-Path $env:WINDIR 'System32\wscript.exe'
    $shortcut.Arguments = "`"" + (Join-Path $root 'scripts\launch-hidden.vbs') + "`""
    $shortcut.WorkingDirectory = $root

    # wscript.exe's own icon would otherwise show, so point at our generated .ico
    # explicitly (regenerate with scripts\make-icon.ps1 if appicon.png changes).
    $ico = Join-Path $root 'ui\RunnerUI\Resources\AppIcon\appicon.ico'
    if (Test-Path $ico) { $shortcut.IconLocation = "$ico,0" }

    $shortcut.Save()
    Write-Host "  Created: $lnk" -ForegroundColor Green
} catch {
    Write-Host "  Couldn't create the shortcut - you can still run launch.bat directly." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Setup complete." -ForegroundColor Green

# Launch the app now and close this window — no need to keep the console around.
$exe = Get-ChildItem -Path (Join-Path $root 'ui\RunnerUI\bin') -Filter 'RunnerUI.exe' -Recurse -ErrorAction SilentlyContinue |
       Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -ne $exe) {
    Write-Host "Opening the app..." -ForegroundColor Cyan
    Start-Process -FilePath $exe.FullName
    Start-Sleep -Seconds 2   # brief pause so the message above is visible before the window closes
} else {
    Write-Host "Couldn't find RunnerUI.exe to launch automatically." -ForegroundColor Yellow
    Write-Host "Open it from the 'Playwright Automation Tool' desktop shortcut instead."
    Read-Host "Press Enter to close"
}
