# RunnerUI

A local desktop UI (.NET MAUI **Blazor Hybrid**, Windows-only) for driving the
Playwright test runner: browse tests, set up a run, watch it execute, review results.

It is a **remote control**, not a reimplementation — it references the runner project
and drives it through its public interfaces (`IDiscovery`, `IRunner`, `IConfig`, …).
All run logic stays in the backend; this project only presents it. See the repo-root
[CLAUDE.md](../../CLAUDE.md) and [diagrams/](../../diagrams) for the backend.

## Where things live

```
RunnerUI/
├─ MauiProgram.cs         Composition root. DI wiring — this is where backend
│                         components (Discovery, Runner, …) are registered so
│                         Blazor pages can inject them. START HERE.
│
├─ App.xaml(.cs)          MAUI application object (minimal — see MainPage).
├─ MainPage.xaml(.cs)     The one native page; hosts the BlazorWebView that the
│                         entire UI renders inside. You rarely touch this.
├─ Routes.razor           Blazor router — maps URLs to pages, sets the layout.
├─ _Imports.razor         Global @using directives for all .razor files.
│
├─ Components/
│  ├─ Layout/             App chrome that wraps every screen (header + content).
│  │                      MainLayout.razor(.css)
│  ├─ Pages/              Routable screens (one @page each). The run workflow —
│  │                      pre-run, live-run, post-run — is built out here.
│  │                      Home.razor
│  ├─ Shared/             Reusable, non-routable UI pieces.
│  │                      SelectMenu.razor(.css) ← in-DOM dropdown (see below)
│  └─ Tests/             Feature components for the test browser.
│                         TestBrowser.razor  ← PARKED (built, not yet wired in)
│
├─ wwwroot/
│  ├─ index.html          The BlazorWebView host page (loads css + framework).
│  └─ css/app.css         The single hand-owned global stylesheet.
│
├─ Platforms/Windows/     Windows-specific MAUI startup (WinUI). Only Windows is
│                         targeted, so this is the only Platforms folder.
├─ Resources/             App icon, splash, fonts.
└─ Properties/
   └─ launchSettings.json `dotnet run` profile (commandName: Project).
```

## Conventions

- **Screens go in `Components/Pages/`** (routable, one `@page`). **Reusable pieces
  go in a feature folder under `Components/`** (e.g. `Components/Tests/`).
- **Styling:** global base is `wwwroot/css/app.css`; anything component-specific is
  a scoped `Foo.razor.css` (or a `<style>` block) next to the component. No Bootstrap.
- **Backend access:** inject an interface (e.g. `IDiscovery`) — never `new` a backend
  type in a component. Register it once in `MauiProgram.cs`.
- **No native `<select>`:** use `SelectMenu` (`Components/Shared`) for dropdowns.
  WebView2 renders a native `<select>`'s option list as a separate OS popup window
  positioned from cached screen coordinates; in this unpackaged app those go stale when
  the window moves, so the list opens offset from the control. `SelectMenu` renders its
  options inside the page, so they move with the window. It's a drop-in for
  `<select @bind-Value>` (supports `@bind-Value:after`, `Disabled`, `Placeholder`).

## Run it

From the repo root:

```powershell
dotnet run --project ui/RunnerUI/RunnerUI.csproj -f net8.0-windows10.0.19041.0
```

Or launch the built exe directly:

```powershell
& "ui\RunnerUI\bin\Debug\net8.0-windows10.0.19041.0\win10-x64\RunnerUI.exe"
```

> The app is built **unpackaged + self-contained** (`WindowsPackageType=None`,
> `WindowsAppSDKSelfContained=true`) so it launches without a system-wide Windows
> App SDK install. Removing those would bring back a `REGDB_E_CLASSNOTREG` crash.
