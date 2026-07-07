# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
dotnet build                          # build (runner + tests are ONE project)
dotnet run -- configs/my-config.json  # run with a config (the only required argument)
```

Run from the repo root — the config path resolves against the CWD. Configs are
copies of `configs/example.json` (everything else in `configs/` is gitignored).

Exit codes: `0` no tests failed (including zero tests matched), `1` tests failed, `2` runner faulted (also `2` for missing config argument).

One-time setup after first build: `pwsh bin/Debug/net8.0/playwright.ps1 install chromium`
(tests launch Chromium only).

Direct `dotnet test` works but needs env vars set — normally the runner sets
these when it spawns the test process:

| Env var | Read by | Meaning |
|---|---|---|
| `BASE_URL` | `TestSettings.BaseUrl` | Target URL — **required**, throws if unset |
| `HEADLESS` | `TestSettings.Headless` | Default `true` |
| `STORAGE_STATE` | `TestBase` | Session file to *reuse* (ignored if the file doesn't exist) |
| `RUN_TIMESTAMP` | `TestSettings.RunTimestamp` | Groups traces per run; falls back to a local timestamp |
| `LOGIN_USERNAME` / `LOGIN_PASSWORD` | login tests | Credentials, supplied by Auth in auto mode only |
| `SAVE_STORAGE_STATE` | `TestBase.DisposeAsync` | Where to *save* the session — set only during an auto-login run |

## Architecture

Config-driven pipeline: the user hands the runner a config file; the runner
walks a state machine and always ends by reporting a `RunResult`.

**Five components, five interfaces** (one folder each under `src/`):

- `IConfig` / `Config` (`src/config`) — loads + validates the config: `site` (routing id), `env` (environment id, any value), `url` (target URL), `auth` (composite: none | manual | auto), `actions` (the slice within the target, e.g. `Category=Smoke` or `all`), `headless` (bool). `site`+`env` identify *which* tests; `actions` narrows *which of those*. JSON is parsed case-insensitively with comments and trailing commas allowed. Validation faults on: missing/invalid `site`/`env` (no `& | = ! ( )` or spaces — they'd break filter syntax), non-http(s) `url`, unknown `auth.mode`, `auto` without credentials, or an `actions` slice using any key other than `Category`/`Module`/`FullyQualifiedName` (the last picks one exact test by its `Namespace.Class.Method` — a built-in vstest property, not a trait).
- `IAuth` / `Auth` (`src/auth`) — establishes a session, saves `src/auth/storageState.json` (path from `Paths.StorageStatePath`). Manual mode opens a headed browser and needs a frontend-supplied `waitForUser` callback (faults if the frontend didn't provide one). **Auto mode is website-agnostic: the runner composes the login selector `Site={site}&Env={env}&Kind=Auth` (must match exactly one test — zero or multiple matches fault), runs that login test — it drives the site's login UI reading `TestSettings.LoginUsername/Password` and asserts success — and `TestBase` saves the session (`SAVE_STORAGE_STATE`).** A failing login test (e.g. wrong password) becomes a Faulted result; so does a passing one that somehow saved no session file. Auth's login-test results go to `results/<timestamp>/auth/` so they never collide with the action run's TRX.
- `IActions` / `Actions` (`src/actions`) — composes the actions selector `Site={site}&Env={env}&Kind=Action` (+ the config's `actions` slice), so `Kind=Auth` login tests are never run as actions. The slice is wrapped in parentheses — `…&Kind=Action&(Module=A|Module=B)` — so an OR inside it can't leak tests from other sites/envs. Defers to the shared `TestProcess` (`src/TestProcess.cs`), which spawns `dotnet test --no-build`, passes config via env vars, streams per-test progress lines, and parses the TRX into `TestResult`s. xUnit owns test iteration — the runner never loops over tests itself.
- `IRunner` / `Runner` (`src/runner`) — state pattern: `IState.RunAsync(ctx)` returns the next state (`null` = done); `RunContext.Current` always holds the current `RunState`, so "where are we" is knowable even mid-crash. States: ReadingConfig → VerifyingEnv → Authenticating → RunningActions → Reporting → Completed. Any exception → Faulted (recording `FaultedDuring`), and the result is still reported. `RunContext` also mints the run's single timestamp that groups traces + results. VerifyingEnv is a 15s GET; *any* HTTP status counts as reachable — only a thrown exception (DNS, refused, timeout) faults.
- `IResults` / `Results` (`src/results`) — console presenter for `RunResult` (PASSED / FAILED / FAULTED verdict, per-test lines, totals, trace/log paths). Swap per frontend. `RunResult` (`src/results/RunResult.cs`) is the single output object: final state, config echo, `TestResult` list, counts, duration, artifact paths.

`Program.cs` (root) is the composition root and the only console-aware wiring
(it also supplies the `waitForUser` callback as a console ReadLine); the
backend must stay frontend-agnostic (no `Console` outside `Program.cs` and
`Results.cs`).

Design docs: `diagrams/sequence.md`, `diagrams/state-chart.md`,
`diagrams/components.md` — illustrative; where a diagram and the code disagree,
the code is authoritative.

## Key invariants

- Test failures are **results**, not faults — `RunningActions → Reporting` happens on pass or fail. `Faulted` means the runner itself broke (bad config, unreachable env, failed login, unreadable results).
- The user only ever edits config files. `configs/*` is gitignored except `example.json` because configs may hold credentials.
- Tests read config exclusively through `TestSettings` (env vars), never from files.
- **Trait convention** — every test carries `Site`, `Env`, `Kind` (`Auth` | `Action`). `Auth` tests carry only those three; `Action` tests add `Category` and `Module`. `Env` is required but free-form (any value, must match the config's `env`); a test valid in several envs carries one `Env` trait per env (see `PortalDevLogin` — tagged both `dev` and `prod`). `Kind=Auth` is what keeps login tests out of action runs. Keys PascalCase; site/env values lowercase; fixed vocab (`Auth`/`Action`/`Smoke`/`Regression`) PascalCase. Folders mirror routing: `src/tests/<site>/<env>/auth|actions/…`.
- The runner composes trait selectors from `site`+`env`; the user never writes `Site=`/`Env=`/`Kind=` by hand — only the `actions` slice (`Category`/`Module`/`FullyQualifiedName` keys only, `&`/`|` combinators).
- Traces save to `traces/<run timestamp>/<Module trait>/<test method>.zip` via `TestBase` (`Uncategorized` when a test has no `Module` trait — correct for auth tests).

## Gotchas

- **One project, two roles.** The csproj is both the runner executable and the xUnit test project (`GenerateProgramFile=false` keeps `Program.cs` as the entry point). `TestProcess` spawns `dotnet test --no-build` because a rebuild would race the running exe — so the binaries the tests run are whatever the last build produced.
- **Runner types live in the global namespace.** `Program.cs` has no `using` directives and `GlobalUsings.cs` imports only `Xunit` — wrapping a `src/` runner type in a namespace breaks compilation. (Test classes under `src/tests/<site>/` do use namespaces; that's fine.)
- **`Paths.ProjectRoot` assumes `bin/Debug/net8.0`** — it walks three levels up from the executing assembly. All artifact paths (`traces/`, `results/`, `storageState.json`) derive from it.
- **"No test matches" is not a fault** — `TestProcess` returns an empty result set (user's selector mistake); any other missing-TRX case throws. An empty action run therefore *Completes* with 0 tests, `Success=true`, exit code 0 — a typo'd `actions` slice fails silently.
- **xUnit runs test classes in parallel** (defaults — there is no `xunit.runner.json`), each `TestBase` launching its own Chromium; only facts within one class are serial. Expect multiple concurrent browsers on `actions: "all"`.
- **The spawned `dotnet test` inherits the runner's full environment** — `TestProcess` only adds/overrides its explicit keys. A `STORAGE_STATE` or `LOGIN_*` variable already set in the user's shell leaks into runs where the runner meant them unset (e.g. `auth.mode: none`).
- **Manual auth mode verifies nothing** — after the `waitForUser` callback returns it waits 1s and saves whatever session exists. A user who confirms without logging in gets a saved logged-out session and downstream action-test failures, not a fault. Only auto mode validates login.
- **`Module` must be a class-level trait** — `TestBase.GetModuleTraitValue` inspects only the class's attributes, so a method-level `Module` is invisible to trace pathing. `Category` is method-level by convention (per-fact) and unused by `TestBase`.
- **`TestBase.DisposeAsync` saves the session before closing the context** — that ordering matters; the `SAVE_STORAGE_STATE` handoff is how a login test's session reaches Auth.
- **`TestBase` reads the `Module` trait and test method name via reflection** (attribute constructor args; xUnit internals for the method name) — renaming those internals or the trait key breaks trace pathing, not compilation.
- Auth's login-test TRX lives in `results/<timestamp>/auth/`; the action run's in `results/<timestamp>/`. Same `TestProcess`, different `resultsDir` — keep it that way or they overwrite each other.

## Adding a new site (recipe)

1. Create `src/tests/<site>/<env>/auth/` and `.../actions/` folders.
2. Write exactly one login test per site+env: extends `TestBase`, tagged `Site`/`Env`/`Kind=Auth`, reads `TestSettings.LoginUsername/Password`, asserts login succeeded. Copy `src/tests/portal/dev/auth/PortalDevLogin.cs`.
3. Write action tests: extend `TestBase`, tagged `Site`/`Env`/`Kind=Action`/`Category`/`Module`, navigate from `TestSettings.BaseUrl`. Copy `src/tests/portal/dev/actions/HomePageTests.cs`.
4. Add a config in `configs/` (gitignored) pointing `site`/`env`/`url` at it.

No runner code changes are needed — routing is entirely trait-driven.

## Branching

`feature/*` → `integration` → `production` (PRs at each step; never commit to
`production` directly). The `/new-feature` skill (`.claude/skills/new-feature`)
creates a `feature/<name>` branch off the latest `integration`.
