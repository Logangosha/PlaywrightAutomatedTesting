#requires -Version 5
<#
  One-time setup for a fresh machine. Idempotent — safe to re-run.
    1. Ensure the .NET 8 SDK (installs via winget if missing).
    2. Ensure the WebView2 runtime (the UI renders in it).
    3. Ensure the .NET MAUI workload (a fresh SDK has none; the build needs it).
    4. Build the app (which builds the runner + tests, and restores NuGet packages
       including the Playwright .NET library).
    5. Install the Playwright Chromium browser (the binaries aren't bundled).
    6. Create a "Playwright Automation Tool" desktop shortcut.
  Then it opens the app itself and closes this window. Next time, use the shortcut.

  Installing the SDK and the MAUI workload needs admin, so if either is missing this
  script relaunches itself elevated (one UAC prompt). If everything's already present,
  it runs without elevation.
#>
$root = Split-Path $PSScriptRoot -Parent   # scripts/ -> repo root

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-Sdk8 {
    try { return [bool](& dotnet --list-sdks 2>$null | Where-Object { $_ -match '^8\.' }) }
    catch { return $false }
}

function Test-MauiWorkload {
    try { return [bool](& dotnet workload list 2>$null | Select-String -SimpleMatch 'maui') }
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

Write-Host "== Playwright Automation Tool setup ==" -ForegroundColor Cyan

# Long path warning. MAUI build artifacts nest deeply (obj\Debug\net8.0-windows...\
# win10-x64\...), so a long starting folder can exceed Windows' 260-char limit and the
# build fails with "path too long". Extracting a ZIP to a deep Downloads folder is the
# usual cause. Warn (don't block) so the user can relocate before hitting it.
if ($root.Length -gt 70) {
    Write-Host ""
    Write-Host "WARNING: this folder path is long ($($root.Length) chars):" -ForegroundColor Yellow
    Write-Host "  $root"
    Write-Host "  MAUI builds create deeply-nested paths and can hit Windows' 260-char limit."
    Write-Host "  If the build fails with 'path too long', move this folder somewhere short"
    Write-Host "  (e.g. C:\PAT) and run setup again." -ForegroundColor Yellow
    Write-Host ""
}

# Figure out what's missing before deciding whether we need admin.
$needSdk  = -not (Test-Sdk8)
$needWv2  = -not (Test-WebView2)
$needMaui = -not (Test-MauiWorkload)

# Installing the SDK / MAUI workload requires admin — relaunch elevated if needed.
if (($needSdk -or $needMaui) -and -not (Test-Admin)) {
    Write-Host "Administrator access is needed to install the SDK and/or MAUI workload." -ForegroundColor Yellow
    Write-Host "Relaunching with elevation (accept the prompt)..."
    Start-Process powershell -Verb RunAs -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    exit
}

# 1. .NET 8 SDK
if (-not $needSdk) {
    Write-Host "[1/6] .NET 8 SDK found." -ForegroundColor Green
} else {
    Write-Host "[1/6] .NET 8 SDK not found - installing..." -ForegroundColor Yellow
    if (Get-Command winget -ErrorAction SilentlyContinue) {
        winget install --id Microsoft.DotNet.SDK.8 -e --accept-source-agreements --accept-package-agreements
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

# 2. WebView2 runtime (registry-checked so we don't sit through a slow winget resolve)
Write-Host "[2/6] Checking WebView2 runtime..." -ForegroundColor Cyan
if (-not $needWv2) {
    Write-Host "  Found." -ForegroundColor Green
} elseif (Get-Command winget -ErrorAction SilentlyContinue) {
    Write-Host "  Not found - installing..." -ForegroundColor Yellow
    winget install --id Microsoft.EdgeWebView2Runtime -e --accept-source-agreements --accept-package-agreements
} else {
    Write-Host "  Not found and winget unavailable - if the app window is blank, install from:" -ForegroundColor Yellow
    Write-Host "  https://developer.microsoft.com/microsoft-edge/webview2/"
}

# 3. .NET MAUI workload (a fresh SDK ships with none; the UI won't build without it).
Write-Host "[3/6] Checking .NET MAUI workload..." -ForegroundColor Cyan
if (-not $needMaui) {
    Write-Host "  Found." -ForegroundColor Green
} else {
    Write-Host "  Not found - installing (this can take several minutes / a large download)..." -ForegroundColor Yellow
    & dotnet workload restore (Join-Path $root 'ui\RunnerUI\RunnerUI.csproj')
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  'dotnet workload restore' failed. Try running, in an elevated terminal:" -ForegroundColor Red
        Write-Host "    dotnet workload install maui"
        Read-Host "Press Enter to close"; exit 1
    }
}

# 4. Build (also restores NuGet, incl. the Playwright .NET library)
Write-Host "[4/6] Building..." -ForegroundColor Cyan
& dotnet build (Join-Path $root 'ui\RunnerUI\RunnerUI.csproj') -v minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed - see errors above." -ForegroundColor Red
    Read-Host "Press Enter to close"; exit 1
}

# 5. Playwright Chromium browser (the binaries are not bundled)
Write-Host "[5/6] Installing Chromium for Playwright..." -ForegroundColor Cyan
$pw = Join-Path $root 'bin\Debug\net8.0\playwright.ps1'
if (Test-Path $pw) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $pw install chromium
} else {
    Write-Host "  playwright.ps1 not found (expected after build) - skipping." -ForegroundColor Yellow
}

# 6. Desktop shortcut -> launch-hidden.vbs (via wscript, so opening the app never
#    flashes a console window; launch.bat is the same thing for manual double-clicks).
Write-Host "[6/6] Creating desktop shortcut..." -ForegroundColor Cyan
try {
    $lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Playwright Automation Tool.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($lnk)
    $shortcut.TargetPath = Join-Path $env:WINDIR 'System32\wscript.exe'
    $shortcut.Arguments = "`"" + (Join-Path $root 'scripts\launch-hidden.vbs') + "`""
    $shortcut.WorkingDirectory = $root

    $ico = Join-Path $root 'ui\RunnerUI\Resources\AppIcon\appicon.ico'
    if (Test-Path $ico) { $shortcut.IconLocation = "$ico,0" }

    $shortcut.Save()
    Write-Host "  Created: $lnk" -ForegroundColor Green
} catch {
    Write-Host "  Couldn't create the shortcut - you can still run launch.bat directly." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Setup complete." -ForegroundColor Green

# Launch the app now and close this window.
$exe = Get-ChildItem -Path (Join-Path $root 'ui\RunnerUI\bin') -Filter 'RunnerUI.exe' -Recurse -ErrorAction SilentlyContinue |
       Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -ne $exe) {
    Write-Host "Opening the app..." -ForegroundColor Cyan
    Start-Process -FilePath $exe.FullName
    Start-Sleep -Seconds 2
} else {
    Write-Host "Couldn't find RunnerUI.exe to launch automatically." -ForegroundColor Yellow
    Write-Host "Open it from the 'Playwright Automation Tool' desktop shortcut instead."
    Read-Host "Press Enter to close"
}
