# Setup — First-Run on a Fresh Machine

What happens the first time the user runs `setup.bat` (→ `scripts/setup.ps1`). It's
**one-time and idempotent** — safe to re-run; each step is skipped if already satisfied.
It ends by opening the app; every launch after that uses the shortcut (see `launch.md`).

Only two steps need admin — installing the **SDK** and the **MAUI workload** — so setup
checks what's missing *first* and self-elevates (one UAC prompt) only if one of those is
absent. If everything's present, it runs without elevation.

```mermaid
flowchart TD
    A["User runs setup.bat"] --> B["setup.ps1"]
    B --> C["Detect what's missing:<br/>.NET 8 SDK?  WebView2 runtime?  MAUI workload?"]
    C --> D{"SDK or MAUI workload missing<br/>AND not running as admin?"}
    D -->|"Yes"| E["Relaunch elevated<br/>(one UAC prompt)"]
    E --> B
    D -->|"No"| F["1. Ensure .NET 8 SDK<br/>(winget install if missing)"]

    F --> G["2. Ensure WebView2 runtime<br/>(registry-checked; winget install if missing)"]
    G --> H["3. Ensure .NET MAUI workload<br/>(dotnet workload restore — the build needs it)"]
    H --> I["4. dotnet build RunnerUI.csproj<br/>(builds runner + tests, restores NuGet incl. Playwright)"]
    I --> J["5. Install Playwright Chromium<br/>(playwright.ps1 install chromium — binaries aren't bundled)"]
    J --> K["6. Create 'Playwright Automation Tool' desktop shortcut<br/>(→ wscript + launch-hidden.vbs)"]
    K --> L["Open the app, close the setup window"]
    L --> Z(["Ready — use the shortcut from now on"])
```

> **Long-path caveat:** MAUI build artifacts nest deeply (`obj\Debug\net8.0-windows…\win10-x64\…`),
> so a long starting folder can exceed Windows' 260-char limit and fail the build. Setup
> *warns* (doesn't block) when the repo path is long; if the build fails with "path too
> long", move the folder somewhere short (e.g. `C:\PAT`) and re-run.
