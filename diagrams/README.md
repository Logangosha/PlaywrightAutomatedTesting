# Playwright Automated Testing

A desktop app + config-driven runner for Playwright tests on .NET. Pick a site,
environment, and tests in the UI; it authenticates, runs them, and shows the results,
traces, and logs — no file-system spelunking.

## Getting started (new machine)

Windows only. On a blank machine:

1. **Get the repo** — clone it, or download the ZIP and extract it. **Put it in a short
   folder path** like `C:\PAT` or `C:\dev\`, *not* a deep `Downloads\…\…` path: MAUI
   builds create very long nested paths and can hit Windows' 260-character limit.
2. **Double-click `setup.bat`.** One time — it installs the .NET 8 SDK + MAUI workload
   (if missing), builds, installs the browser, adds a **Playwright Automation Tool**
   desktop shortcut, and opens the app when it's done. If Windows SmartScreen warns
   ("Windows protected your PC"), choose **More info → Run anyway** — the scripts aren't
   code-signed.
3. **Next time, use that shortcut** (or `launch.bat`).

**What `setup.bat` installs** (all one-time, all automatic):

| Component | Why | Notes |
|---|---|---|
| .NET 8 SDK | build + run tests | via winget if missing |
| .NET MAUI workload | the desktop UI is a MAUI app | a fresh SDK has none; large download |
| WebView2 runtime | the UI renders in it | preinstalled on modern Windows |
| Playwright **Chromium** | tests drive a real browser | the ~150 MB browser binaries |
| NuGet packages | Playwright .NET lib, xUnit, … | restored on build — no separate step |

Installing the SDK and the MAUI workload requires admin, so `setup.bat` will prompt for
elevation (UAC) **only if** one of them is missing; if everything's already present it
runs without a prompt. The MAUI workload is a sizeable download on a blank machine — the
first setup can take several minutes.

Note the two halves of Playwright: the **.NET library** is a NuGet package restored
automatically on build; the **browser binaries** are a separate one-time download that
`setup.bat` installs. The Windows App SDK the UI needs is bundled into the app, so
there's nothing to install for it.

### What happens every time you open the app

The shortcut doesn't just launch the app — **it builds first, silently, every time**,
so the tests you see are always the ones currently on disk. No console window appears;
you just see the app open a few seconds later (an unchanged build takes a couple of
seconds, a real rebuild a bit longer). If that build fails, the app still opens — using
the last one that worked — and shows a banner explaining the previous build failed,
with a link to the error log, instead of silently running stale tests.

### What happens while the app is open

Editing anything under `src/tests` while the app is running shows a banner —
*"Tests changed — rebuild and restart to apply."* Tests are compiled, so a change only
takes effect after a rebuild, and the app can't rebuild itself while it's running (it
has the test assembly open). Click **Restart & rebuild** and it closes, rebuilds, and
reopens for you. See [src/tests/README.md](src/tests/README.md) for the full authoring
guide, including the manual fallback.

- **Add or edit tests:** see [src/tests/README.md](src/tests/README.md).
- **Work on the runner/UI internals:** see [CLAUDE.md](CLAUDE.md) and [diagrams/](diagrams/).

---

The rest of this page documents the **console runner** (the same engine the UI drives),
useful for scripting, CI, or debugging a single run.

## Prerequisites (console / manual)

`setup.bat` does all of this for you; do it by hand only if you prefer the console:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Playwright browsers (one-time install, after the first build):

```bash
dotnet build
pwsh bin/Debug/net8.0/playwright.ps1 install chromium
```

> Tests run on Chromium (see [TestBase.cs](src/tests/TestBase.cs)), so
> installing just `chromium` is enough. If you skip this step, tests fail with
> "Executable doesn't exist" — see [Troubleshooting](#troubleshooting).

## Quick start (console)

From the repo root (the config path is resolved relative to where you run):

```bash
# 1. Copy the example config and fill it in (your copy is gitignored)
cp configs/example.json configs/portal-dev.json          # bash
Copy-Item configs/example.json configs/portal-dev.json   # PowerShell

# 2. Run it
dotnet run -- configs/portal-dev.json
```

Exit codes: `0` no tests failed · `1` some tests failed · `2` the runner faulted
(or no config argument was given).

To see what's available before writing a config, list every test grouped by
site → env → module (this runs nothing):

```bash
dotnet run -- discover
```

## What a run looks like

The runner walks a fixed sequence of states and always ends with a report:

```
ReadingConfig → VerifyingEnv → Authenticating → RunningActions → Reporting → Completed
```

1. **ReadingConfig** — loads and validates your config; a bad config stops the run immediately.
2. **VerifyingEnv** — sends a GET to your `url` to confirm the environment is reachable.
3. **Authenticating** — establishes a session per your `auth.mode` (see below) and saves it.
4. **RunningActions** — spawns `dotnet test` with a trait filter composed from your config; xUnit runs the tests.
5. **Reporting** — prints the verdict, per-test results, totals, and where the traces/log landed.

Test failures are normal results — you still get a full report and exit code `1`.
`FAULTED` (exit code `2`) means the runner itself couldn't finish: bad config,
unreachable environment, failed login, or unreadable results. Either way you
always get a report — the run never dies silently.

```
===================================
 RUN RESULT : PASSED
===================================
 Config   : configs/portal-dev.json
 Site     : portal
 Env      : dev
 ...
-----------------------------------
  PASS  Auth_PreTestState_UserIsLoggedInAndSeesWelcomeMessage  (4.2s)
-----------------------------------
 Total 1 | Passed 1 | Failed 0 | Skipped 0 | 12.3s
 Traces : C:\...\traces\2026-07-05_17-07-20
 Log    : C:\...\results\2026-07-05_17-07-20\log.txt
===================================
```

## The config

A config identifies one **site + env** target and how to run it.
[configs/example.json](configs/example.json) is a fully commented template —
JSON comments and trailing commas are allowed, so keep the comments in your copy.

```jsonc
{
  "site": "portal",                // which project/site (routes to its tests)
  "env":  "dev",                   // environment — any value you choose
  "url":  "https://portal-dev.example.com/",  // target URL for this site + env
  "auth": {
    "mode": "auto",                // none | manual | auto
    "username": "user@example.com", // auto mode only
    "password": "..."              // auto mode only
  },
  "actions": "Category=Smoke",     // which action tests to run ("all" or a slice)
  "headless": true                 // run browsers without a visible window
}
```

### Auth modes

- **none** — no login; tests start from a fresh, logged-out browser context.
- **manual** — a headed browser opens at your `url`; log in yourself, then press
  ENTER in the terminal. The session is saved and reused by every test.
  ⚠️ Nothing verifies you actually logged in — press ENTER too early and a
  logged-out session is saved, and the tests fail later in confusing ways.
- **auto** — runs this site+env's login test (see
  [Writing an auth test](#writing-an-auth-test)) with the config's credentials.
  The login test asserts it worked, so a wrong password **faults the run and
  tells you** instead of quietly running tests with a broken session.

### The `actions` slice

**You never write trait selectors by hand.** The runner turns `site` + `env`
into them for you: it selects `Site=portal & Env=dev & Kind=Auth` for the
login, and `Site=portal & Env=dev & Kind=Action` (plus your `actions` slice)
for the run. So `actions` only needs the slice *within* the target, using
`Category` and/or `Module` to group, or `FullyQualifiedName` to pick one exact
test:

```
"all"                                       — every action test for this site + env
"Category=Smoke"                            — one category
"Category=Smoke&Module=HomePage"            — category AND module
"Module=HomePage|Module=MemberManagement"   — either module (OR)
"FullyQualifiedName=PortalTests.Dev.Actions.HomePageTests.ClickLogin"
                                            — one specific test
"FullyQualifiedName=…HomePageTests.ClickLogin|FullyQualifiedName=…HomePageTests.SearchBar"
                                            — two specific tests (OR them)
"Category=Smoke|FullyQualifiedName=…HomePageTests.SearchBar"
                                            — a category plus one extra test
```

Only `Category`, `Module`, and `FullyQualifiedName` are allowed in the slice —
the config loader rejects anything else (including hand-written
`Site=`/`Env=`/`Kind=`).

`FullyQualifiedName` is not a trait you tag on a test — it's the built-in
`Namespace.Class.Method` identity every test already has, so nothing changes in
your test files to use it. You normally don't type it by hand (a UI fills it in
from test discovery); the trade-off is that renaming or moving a test changes
its `FullyQualifiedName`, so an old config that pins a specific test silently
stops matching it after a refactor.

Note that values aren't checked against your tests: a typo like
`Category=Smok` — or a stale `FullyQualifiedName` — loads fine, matches zero
tests, and the run completes with exit code `0`. If you expected tests and got
none, check the slice first.

### Secrets

Configs may contain credentials, so everything in `configs/` except
`example.json` is gitignored — secrets never leave your machine.

## Architecture

Five components behind five interfaces — see [diagrams/](diagrams/) for the
sequence diagram, state chart, and component table, and [CLAUDE.md](CLAUDE.md)
for the deeper conventions.

| Component | Folder | Role |
|---|---|---|
| Config    | [src/config](src/config)       | Loads and validates the config the user provides |
| Auth      | [src/auth](src/auth)           | Establishes a session, saves `storageState.json` |
| Actions   | [src/actions](src/actions)     | Delegates the selected tests to xUnit (`dotnet test`), parses the TRX |
| Runner    | [src/runner](src/runner)       | State machine orchestrating the run — always ends in a result |
| Results   | [src/results](src/results)     | Presents the run result to the user |
| Discovery | [src/discovery](src/discovery) | Lists the available tests (the "menu") so a frontend can browse and select — runs nothing |

[Program.cs](Program.cs) is only the console wiring; the backend is
frontend-agnostic, so an API or UI could drive the same `Runner`. The runner
and the tests live in **one project** — `dotnet run` builds everything, then
the runner spawns `dotnet test --no-build` in a child process and passes the
config to the tests as environment variables.

## Artifacts (per run, gitignored)

Every run is stamped with one timestamp that groups all its output:

| Path | What it is |
|---|---|
| `traces/<timestamp>/<Module>/<test>.zip` | Playwright trace per test (screenshots, snapshots, sources) |
| `results/<timestamp>/tests.trx` | Raw xUnit results for the action run |
| `results/<timestamp>/log.txt` | Full test process output for the action run |
| `results/<timestamp>/auth/` | Same pair for the auto-login test (auto mode only) |
| `src/auth/storageState.json` | The saved browser session Auth hands to the tests |

Auth tests have no `Module` trait, so their traces land in
`traces/<timestamp>/Uncategorized/`. Nothing is cleaned up automatically —
every run adds a new timestamped folder, so prune `traces/` and `results/`
occasionally.

Open a trace with `pwsh bin/Debug/net8.0/playwright.ps1 show-trace <file>.zip`
or at [trace.playwright.dev](https://trace.playwright.dev).

## Trait convention

Many sites, environments, and both login and action tests live in one repo. **Traits
are how the runner tells them apart** — so every test must be tagged consistently.

| Trait | On | Example value | Meaning |
|---|---|---|---|
| `Site` | every test | `portal` | which project/site |
| `Env` | every test | `dev` | which environment (**required**, any value you choose) |
| `Kind` | every test | `Auth` / `Action` | a login test vs a normal test |
| `Category` | action tests | `Smoke`, `Regression` | severity / scope |
| `Module` | action tests | `HomePage` | feature area (also names the traces folder) |

Rules of the road:

- **`Site`, `Env`, `Kind` are required on every test.** An `Auth` test carries *only*
  those three. An `Action` test adds `Category` and `Module`.
- **`Env` can be any value** — pick your own naming (`dev`, `qa`, `client-staging`, …).
  It just has to match the `env` in the config that runs the test.
- **A test that applies to several envs carries one `Env` trait per env** — e.g.
  `[Trait("Env","dev")] [Trait("Env","prod")]` when the flow is identical in both.
  Write a separate test only where an env genuinely behaves differently.
- **Keys are PascalCase.** For values: site/env are lowercase ids; the fixed vocab
  (`Auth`, `Action`, `Smoke`, `Regression`) is PascalCase.
- **`Kind=Auth` keeps login tests out of action runs automatically** — the runner only
  ever selects `Kind=Action` for actions, so a login test can never run as an action.
- **Mirror the routing keys in folders** so the repo is navigable at a glance:
  `src/tests/<site>/<env>/auth/…` and `src/tests/<site>/<env>/actions/…`. A
  multi-env test lives in its *home* env's folder (the portal tests live in
  `portal/dev/` but also carry `Env=prod`) — traits route, folders organize.
- **`Module` goes on the class, `Category` on the test method** (see the portal
  examples). The class-level `Module` is also what names the trace folder.

The live examples under [src/tests/portal/dev](src/tests/portal/dev) follow all
of these — copy them when starting a new site.

## Writing tests

The full guide — how to structure auth and action tests, the trait rules, the
"add a new site" recipe, and copy-paste patterns for the common edge cases (cookie
banners, two-step logins, dynamic content, negative assertions, native dialogs,
data-driven `[Theory]`) — lives in **[src/tests/README.md](src/tests/README.md)**.

In short: every test extends [TestBase](src/tests/TestBase.cs), reads config from
`TestSettings`, and is tagged with `[Trait]`s (`Site`/`Env`/`Kind`, plus
`Category`/`Module` for actions). Auto mode runs the one `Kind=Auth` login test per
site+env; `Kind=Action` tests start already logged in. The live examples under
[src/tests/portal/dev](src/tests/portal/dev) follow every convention — copy them.

## Running tests without the runner

Normally the runner spawns the tests and supplies their configuration as
environment variables. For debugging a single test you can run `dotnet test`
directly if you set those variables yourself:

```powershell
$env:BASE_URL = "https://portal-dev.example.com/"   # required
$env:HEADLESS = "false"                             # optional, default true
dotnet test --filter "Site=portal&Env=dev&Kind=Action&Module=HomePage"
```

All the variables tests understand are defined in
[TestSettings.cs](src/tests/TestSettings.cs): `BASE_URL`, `HEADLESS`,
`STORAGE_STATE` (session file to reuse), `RUN_TIMESTAMP` (trace grouping), and
— for login tests only — `LOGIN_USERNAME`, `LOGIN_PASSWORD`,
`SAVE_STORAGE_STATE`.

Two things to know when running directly:

- **Always pass a `--filter`.** An unfiltered `dotnet test` also runs the
  `Kind=Auth` login tests, which throw without `LOGIN_USERNAME`/`LOGIN_PASSWORD`.
- **Test classes run in parallel** (xUnit's default), each launching its own
  Chromium — several browser windows at once is normal, not a bug.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `BASE_URL is not set` | You ran `dotnet test` directly without the env vars — run through the runner, or set them (see above). |
| `Executable doesn't exist ... chromium` | Playwright browsers not installed — run the [prerequisites](#prerequisites) install step. |
| `FAULTED — Environment not reachable` | The `url` is wrong, down, or needs VPN. The runner checks it before anything else. |
| `No login test found for 'Site=…&Env=…&Kind=Auth'` | `auth.mode` is `auto` but no test carries those traits — write one ([Writing an auth test](#writing-an-auth-test)). |
| `Login selector … matched N tests, expected exactly 1` | Only one `Kind=Auth` test may match per site+env — retag the extras. |
| `Login failed in '…'` | The login test's assertion failed — usually wrong credentials in the config. |
| `No tests matched '…'` (0 tests reported) | The `actions` slice doesn't match any test's traits — check `Category`/`Module` values and the site/env tags. |

## Branching strategy

Three branches: `feature/*` → `integration` → `production`.

```
production    stable, always releasable — only merges from integration via PR
    ↑
integration   staging — feature branches merge here first and get validated
    ↑
feature/*     one branch per test/feature, created off integration
```

Never commit to `production` directly. Start work by branching off the latest
`integration` (`feature/add-homepage-tests`, `feature/fix-auth-flow`, …), open
a PR back to `integration`, and delete the branch after merge. When
`integration` is validated, PR it into `production`.

> Using Claude Code? The `/new-feature` skill does the branch setup for you:
> it fetches the latest `integration`, asks for a name, and creates + checks
> out `feature/<name>`.
