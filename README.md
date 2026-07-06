# Playwright Automated Testing

A config-driven Playwright test runner for .NET. You hand the runner a config
file — it verifies the environment is reachable, authenticates, runs the
selected actions (xUnit + Playwright tests), and reports the results. **The
config is the only thing you ever edit to run tests.**

New to the repo? This page gets you running and pointed in the right direction.
Working *on* the runner itself? Start with [CLAUDE.md](CLAUDE.md) and
[diagrams/](diagrams/) for the architecture.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Playwright browsers (one-time install, after the first build):

```bash
dotnet build
pwsh bin/Debug/net8.0/playwright.ps1 install chromium
```

> Tests run on Chromium (see [TestBase.cs](src/tests/TestBase.cs)), so
> installing just `chromium` is enough. If you skip this step, tests fail with
> "Executable doesn't exist" — see [Troubleshooting](#troubleshooting).

## Quick start

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
`Category` and/or `Module`:

```
"all"                                       — every action test for this site + env
"Category=Smoke"                            — one category
"Category=Smoke&Module=HomePage"            — category AND module
"Module=HomePage|Module=MemberManagement"   — either module (OR)
```

Only `Category` and `Module` are allowed in the slice — the config loader
rejects anything else (including hand-written `Site=`/`Env=`/`Kind=`).
Note that values aren't checked against your tests: a typo like
`Category=Smok` loads fine, matches zero tests, and the run completes with
exit code `0` — if you expected tests and got none, check the slice first.

### Secrets

Configs may contain credentials, so everything in `configs/` except
`example.json` is gitignored — secrets never leave your machine.

## Architecture

Five components behind five interfaces — see [diagrams/](diagrams/) for the
sequence diagram, state chart, and component table, and [CLAUDE.md](CLAUDE.md)
for the deeper conventions.

| Component | Folder | Role |
|---|---|---|
| Config  | [src/config](src/config)   | Loads and validates the config the user provides |
| Auth    | [src/auth](src/auth)       | Establishes a session, saves `storageState.json` |
| Actions | [src/actions](src/actions) | Delegates the selected tests to xUnit (`dotnet test`), parses the TRX |
| Runner  | [src/runner](src/runner)   | State machine orchestrating the run — always ends in a result |
| Results | [src/results](src/results) | Presents the run result to the user |

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

## Writing an auth test

Auto mode runs exactly **one** login test per `site`+`env` — zero matches or
more than one both fault the run. Write one that:

- extends [TestBase](src/tests/TestBase.cs),
- is tagged `Site`, `Env`, and `Kind=Auth` (no `Category`/`Module` — auth doesn't use them),
- reads credentials from `TestSettings.LoginUsername` / `TestSettings.LoginPassword`
  (the runner supplies these from the config — never hardcode them),
- drives the site's login UI and **asserts** it worked.

`TestBase` saves the session automatically. Because it's a real test with an assertion,
a wrong password fails the assertion → the run **faults and tells you**, instead of
quietly running actions with a broken session.

```csharp
[Trait("Site", "acme")]
[Trait("Env", "prod")]
[Trait("Kind", "Auth")]
public class AcmeProdLogin : TestBase
{
    public AcmeProdLogin(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task Login_Succeeds()
    {
        await Page.GotoAsync(TestSettings.BaseUrl);

        await Page.Locator("#email").FillAsync(TestSettings.LoginUsername);
        await Page.Locator("#password").FillAsync(TestSettings.LoginPassword);
        await Page.Locator("button[type=submit]").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert something only true once logged in.
        Assert.False(await Page.Locator("#password").IsVisibleAsync(),
            "Login failed — still on the login form.");
        // TestBase saves the session on cleanup.
    }
}
```

A working reference lives at
[PortalDevLogin.cs](src/tests/portal/dev/auth/PortalDevLogin.cs).

## Writing an action test

Action tests are the real work. Write ones that:

- extend [TestBase](src/tests/TestBase.cs) (browser, tracing, and session reuse are handled),
- are tagged `Site`, `Env`, `Kind=Action`, plus `Category` and `Module`,
- navigate using `TestSettings.BaseUrl` (the `url` from the config).

The session the auth step saved is reused automatically, so the test starts logged in.

```csharp
[Trait("Site", "acme")]
[Trait("Env", "prod")]
[Trait("Kind", "Action")]
[Trait("Module", "HomePage")]
public class AcmeHomePageTests : TestBase
{
    public AcmeHomePageTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HomePage_AfterLogin_ShowsWelcome()
    {
        await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        var welcome = Page.GetByRole(AriaRole.Heading, new() { Name = "Welcome" });
        await welcome.WaitForAsync();

        Assert.True(await welcome.IsVisibleAsync());
    }
}
```

A working reference lives at
[HomePageTests.cs](src/tests/portal/dev/actions/HomePageTests.cs).

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
