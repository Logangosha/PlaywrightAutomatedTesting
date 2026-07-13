#requires -Version 5
<#
  Build-and-launch engine for the Runner UI. Building the UI project also rebuilds the
  referenced runner/test project and refreshes the assembly the UI loads, so tests are
  always current. Used two ways, both through this one script:

    - Open the app (launch.bat / the desktop shortcut, via launch-hidden.vbs): no args,
      runs with no visible window. Takes a fast source-manifest snapshot (paths + sizes +
      write-times of everything that affects the build) and compares it to the last
      successfully built snapshot. No change -> launch the existing exe immediately, no
      build. Any file created / deleted / modified -> build first, then launch.
    - In-app "Restart & rebuild" (author loop): -WaitForPid <app pid> -> wait for the app
      to close so its DLLs unlock, then ALWAYS build and relaunch (a deliberate rebuild
      click; the staleness check is skipped). Runs in a visible console (AppRestarter
      launches it that way) so the user can watch/read it.

  Why a manifest, not a DLL-timestamp check: comparing source mtimes against the built
  DLL missed edits in the past (clock skew, files touched without a size change, adds /
  deletes that don't move the newest mtime). Hashing the full file list catches creates,
  deletes, and modifications uniformly.

  Build failures are written to logs\build-error.log (cleared on the next successful
  build). The app itself reads that file on startup and shows a banner — this is how a
  failure surfaces when this script ran with no visible window at all. The one case that
  can't wait for the app (no previous build exists to open) pops a native error dialog.
#>
param([int]$WaitForPid = 0)

$root = Split-Path $PSScriptRoot -Parent   # scripts/ -> repo root
$logsDir = Join-Path $root 'logs'
$errorLog = Join-Path $logsDir 'build-error.log'
$manifestFile = Join-Path $logsDir 'source-manifest.txt'
$interactive = $WaitForPid -gt 0            # true only for the visible, in-app-triggered path

# Directories whose contents never affect a build (or churn on every run). Pruned during
# the scan by name at any depth, so bin/obj/etc. don't slow the snapshot or trigger builds.
$excludeDirs  = @('.git', 'bin', 'obj', 'logs', 'traces', 'results', 'configs', '.vs', 'TestResults', 'node_modules', '.idea', 'packages')
# Files that change at runtime but aren't build inputs.
$excludeFiles = @('storageState.json', 'source-manifest.txt')

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

# True when Windows is in dark mode (AppsUseLightTheme = 0). Defaults to light if unset.
function Test-DarkTheme {
    try {
        $v = Get-ItemProperty -Path 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize' `
                              -Name 'AppsUseLightTheme' -ErrorAction Stop
        return ($v.AppsUseLightTheme -eq 0)
    } catch { return $false }
}

# Runs `dotnet build` while showing a themed, movable splash (icon + live status), the
# way Discord shows an updater. Blocks until the build finishes, then the splash closes.
# Returns @{ ExitCode = <int>; Output = <string[]> }. Only used on the silent open path;
# the interactive path already has a visible console.
function Invoke-BuildWithSplash([string]$csproj, [string]$iconPath) {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    $dark = Test-DarkTheme
    if ($dark) {
        $bg = [System.Drawing.Color]::FromArgb(30, 31, 34)
        $fg = [System.Drawing.Color]::FromArgb(255, 255, 255)
        $sub = [System.Drawing.Color]::FromArgb(181, 186, 193)
        $edge = [System.Drawing.Color]::FromArgb(60, 62, 66)
    } else {
        $bg = [System.Drawing.Color]::FromArgb(242, 243, 245)
        $fg = [System.Drawing.Color]::FromArgb(6, 6, 7)
        $sub = [System.Drawing.Color]::FromArgb(78, 80, 88)
        $edge = [System.Drawing.Color]::FromArgb(210, 212, 216)
    }

    $form = New-Object System.Windows.Forms.Form
    $form.FormBorderStyle = 'None'
    $form.StartPosition = 'CenterScreen'
    $form.Size = New-Object System.Drawing.Size(340, 300)
    $form.BackColor = $bg
    $form.TopMost = $true
    $form.ShowInTaskbar = $true
    $form.Text = 'Playwright Automation Tool'
    # 1px border so a fixed rectangle reads as a window against a same-color desktop.
    $form.Padding = New-Object System.Windows.Forms.Padding(1)
    $form.add_Paint({
        param($s, $e)
        $r = $s.ClientRectangle; $r.Width -= 1; $r.Height -= 1
        $pen = New-Object System.Drawing.Pen $edge
        $e.Graphics.DrawRectangle($pen, $r); $pen.Dispose()
    })

    $pic = New-Object System.Windows.Forms.PictureBox
    $pic.SizeMode = 'Zoom'
    $pic.Size = New-Object System.Drawing.Size(120, 120)
    $pic.Location = New-Object System.Drawing.Point(110, 50)
    if (Test-Path $iconPath) {
        try { $pic.Image = [System.Drawing.Image]::FromFile($iconPath) } catch { }
    }
    $form.Controls.Add($pic)

    $lblTitle = New-Object System.Windows.Forms.Label
    $lblTitle.Text = 'Updating'
    $lblTitle.ForeColor = $fg
    $lblTitle.Font = New-Object System.Drawing.Font('Segoe UI', 13, [System.Drawing.FontStyle]::Bold)
    $lblTitle.TextAlign = 'MiddleCenter'
    $lblTitle.Location = New-Object System.Drawing.Point(20, 185)
    $lblTitle.Size = New-Object System.Drawing.Size(300, 30)
    $form.Controls.Add($lblTitle)

    $lblStatus = New-Object System.Windows.Forms.Label
    $lblStatus.Text = 'Preparing build'
    $lblStatus.ForeColor = $sub
    $lblStatus.Font = New-Object System.Drawing.Font('Segoe UI', 9)
    $lblStatus.TextAlign = 'MiddleCenter'
    $lblStatus.Location = New-Object System.Drawing.Point(20, 218)
    $lblStatus.Size = New-Object System.Drawing.Size(300, 40)
    $form.Controls.Add($lblStatus)

    # --- Make the borderless window draggable anywhere on its surface ---
    $script:dragging = $false
    $script:dragOffset = [System.Drawing.Point]::Empty
    $down = {
        param($s, $e)
        if ($e.Button -eq [System.Windows.Forms.MouseButtons]::Left) {
            $script:dragging = $true
            $script:dragOffset = $e.Location
            # Offset is relative to the child control; translate to the form.
            if ($s -ne $form) {
                $script:dragOffset = New-Object System.Drawing.Point(($s.Left + $e.X), ($s.Top + $e.Y))
            }
        }
    }
    $move = {
        param($s, $e)
        if ($script:dragging) {
            $p = [System.Windows.Forms.Cursor]::Position
            $form.Location = New-Object System.Drawing.Point(($p.X - $script:dragOffset.X), ($p.Y - $script:dragOffset.Y))
        }
    }
    $up = { $script:dragging = $false }
    foreach ($c in @($form, $pic, $lblTitle, $lblStatus)) {
        $c.add_MouseDown($down); $c.add_MouseMove($move); $c.add_MouseUp($up)
    }

    # Build on a background job so the UI thread stays responsive and can show progress.
    $job = Start-Job -ScriptBlock {
        param($proj)
        & dotnet build $proj -v minimal 2>&1
        $LASTEXITCODE
    } -ArgumentList $csproj

    $script:tick = 0
    $script:start = Get-Date
    $timer = New-Object System.Windows.Forms.Timer
    $timer.Interval = 400
    $timer.add_Tick({
        $script:tick++
        $dots = '.' * (($script:tick % 3) + 1)
        $lblTitle.Text = 'Updating' + $dots
        $elapsed = [int]((Get-Date) - $script:start).TotalSeconds
        $last = Receive-Job -Job $job -Keep 2>$null |
                Where-Object { $_ -is [string] -and $_.Trim() -ne '' } |
                Select-Object -Last 1
        if ($last) {
            $t = ([string]$last).Trim()
            if ($t.Length -gt 46) { $t = $t.Substring(0, 45) + [char]0x2026 }
            $lblStatus.Text = "$t   ($elapsed s)"
        } else {
            $lblStatus.Text = "Rebuilding the app   ($elapsed s)"
        }
        if ($job.State -ne 'Running') {
            $timer.Stop()
            $form.Close()
        }
    })

    $form.add_Shown({ $timer.Start() })
    [void]$form.ShowDialog()
    $timer.Dispose()

    # Drain the job: last emitted value is the exit code, everything else is build output.
    $all = Receive-Job -Job $job -Wait -AutoRemoveJob 2>$null
    $exit = 0
    $output = @()
    if ($all) {
        $exit = [int]($all | Select-Object -Last 1)
        $output = @($all | Select-Object -SkipLast 1)
    }
    if ($pic.Image) { $pic.Image.Dispose() }
    $form.Dispose()
    return @{ ExitCode = $exit; Output = $output }
}

# Walk the source tree (pruning $excludeDirs) and produce a stable hash of every build
# input's relative path + length + last-write-time. Uses raw System.IO for speed.
function Get-SourceHash([string]$rootPath, [string[]]$exDirs, [string[]]$exFiles) {
    $lines = New-Object System.Collections.Generic.List[string]
    $stack = New-Object System.Collections.Stack
    $stack.Push($rootPath)
    while ($stack.Count -gt 0) {
        $dir = $stack.Pop()
        try { $entries = [System.IO.Directory]::GetFileSystemEntries($dir) } catch { continue }
        foreach ($entry in $entries) {
            $name = [System.IO.Path]::GetFileName($entry)
            if ([System.IO.Directory]::Exists($entry)) {
                if ($exDirs -notcontains $name) { $stack.Push($entry) }
            } else {
                if ($exFiles -contains $name) { continue }
                $info = New-Object System.IO.FileInfo $entry
                $rel = $entry.Substring($rootPath.Length)
                $lines.Add(("{0}|{1}|{2}" -f $rel, $info.Length, $info.LastWriteTimeUtc.Ticks))
            }
        }
    }
    $lines.Sort([System.StringComparer]::Ordinal)   # order-independent, stable across runs
    $joined = [string]::Join("`n", $lines)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($joined)
    $hash = $sha.ComputeHash($bytes)
    return [System.BitConverter]::ToString($hash) -replace '-', ''
}

if ($interactive) {
    Write-Host "Waiting for the app to close (pid $WaitForPid)..." -ForegroundColor Cyan
    try { Wait-Process -Id $WaitForPid -Timeout 60 -ErrorAction SilentlyContinue } catch { }
}

New-Item -ItemType Directory -Path $logsDir -Force | Out-Null

# --- Fast staleness check (silent open path only) ---------------------------------------
# If nothing that affects the build changed since the last successful build, skip the build
# entirely and just launch the existing exe. Deliberate in-app rebuilds always build.
$currentHash = Get-SourceHash $root $excludeDirs $excludeFiles
$exe = Find-Exe

if (-not $interactive -and $null -ne $exe -and (Test-Path $manifestFile)) {
    $storedHash = (Get-Content $manifestFile -Raw -ErrorAction SilentlyContinue).Trim()
    if ($storedHash -eq $currentHash) {
        # No source changes and a good build already exists -> launch straight away.
        if (Test-Path $errorLog) { Remove-Item $errorLog -Force -ErrorAction SilentlyContinue }
        Start-Process -FilePath $exe.FullName
        exit 0
    }
}

# --- Build path -------------------------------------------------------------------------
if (Test-Path $errorLog) { Remove-Item $errorLog -Force -ErrorAction SilentlyContinue }

$csproj = Join-Path $root 'ui\RunnerUI\RunnerUI.csproj'
$iconPath = Join-Path $root 'ui\RunnerUI\Resources\AppIcon\appicon.png'

if ($interactive) {
    # Deliberate in-app rebuild: keep the visible console, no splash.
    Write-Host "Building (so tests are current)..." -ForegroundColor Cyan
    $buildLines = & dotnet build $csproj -v minimal 2>&1
    $buildExit = $LASTEXITCODE
    $buildLines | ForEach-Object { Write-Host $_ }
} else {
    # Silent open path: rebuild can take ~a minute, so show a themed splash meanwhile.
    $res = Invoke-BuildWithSplash $csproj $iconPath
    $buildLines = $res.Output
    $buildExit = $res.ExitCode
}

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

# Record the post-build snapshot so the next open can skip the build. Recomputed after the
# build (not reusing $currentHash) so anything the build itself touches under a scanned dir
# is baked in and won't force a rebuild on every subsequent launch.
$postBuildHash = Get-SourceHash $root $excludeDirs $excludeFiles
Set-Content -Path $manifestFile -Value $postBuildHash -Encoding utf8

Start-Process -FilePath $exe.FullName
