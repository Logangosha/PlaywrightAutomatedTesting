# Launch — Opening the App

What happens when the user opens the app via the **desktop shortcut** or `launch.bat`.
The everyday open path is *silent* (no console) and only rebuilds when source actually
changed — a fast source-manifest check, not an unconditional build. See CLAUDE.md
("Setup, launch, and the build/watch lifecycle") for the mechanism, and `scripts/rebuild.ps1`.

Key rule: **the app never rebuilds itself** — it holds the test assembly loaded (locked),
so building is always an external script (`rebuild.ps1`) that runs *before* the app opens.

```mermaid
flowchart TD
    A["User double-clicks the desktop shortcut<br/>(or launch.bat)"] --> B["wscript runs launch-hidden.vbs<br/>window style 0 — no console flash"]
    B --> C["rebuild.ps1 (no -WaitForPid)"]

    C --> D["Scan repo, hash every build input<br/>path + size + last-write-time<br/>(prune bin / obj / logs / traces / results / configs)"]
    D --> E{"Hash matches logs/source-manifest.txt<br/>AND a built exe exists?"}

    E -->|"Yes — nothing changed"| F["Launch existing RunnerUI.exe"]

    E -->|"No — a file was<br/>created / deleted / modified"| G["Show themed splash<br/>(OS light/dark, app icon, live status;<br/>build runs on a background job)"]
    G --> H["dotnet build RunnerUI.csproj"]
    H --> I{"Build OK?"}

    I -->|"Yes"| J["Store new hash in source-manifest.txt<br/>(recomputed post-build), close splash"]
    J --> K["Launch new RunnerUI.exe"]

    I -->|"No"| L{"Previous good build exists?"}
    L -->|"Yes"| M["Launch previous exe — it reads<br/>logs/build-error.log on startup and<br/>shows a red banner (MainLayout)"]
    L -->|"No"| N["Native MessageBox error<br/>(nothing to open yet)"]

    F --> Z(["App running"])
    K --> Z
    M --> Z
```

## The other entry point: in-app "Restart & rebuild"

The same `rebuild.ps1` also drives the author loop. When `TestsWatcher` sees `src/tests/*.cs`
change, `MainLayout.razor` shows a banner whose **Restart & rebuild** button hands off to
`rebuild.ps1 -WaitForPid <own pid>` in a **visible** console, then exits. That path is a
deliberate rebuild click, so it **skips the staleness check and always builds** (and shows
the console instead of the splash) — it first waits for the app to close so the test DLL
unlocks, then builds and relaunches.
