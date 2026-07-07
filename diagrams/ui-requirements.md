# UI Requirements — Runner Remote Control

Requirements for a UI that drives the existing runner. This is a **requirements**
doc, not a design — it says *what* the UI must do, not *how* it's built. Where a
requirement leans on runner behavior, the code (`src/`) is authoritative.

## Guiding principle

**The app is a remote control, not a reimplementation.** It drives the existing
runner through its interfaces (`IRunner` / `IConfig` / `IAuth` / `IActions` /
`IResults`) — it never forks or duplicates run logic. Every action the UI offers
maps to something the runner already does. This is what keeps the backend
authoritative and the UI extensible.

The user flow is a linear loop: **set up → watch → review → (new run)**. The three
MVP UIs below mirror that loop.

## Functional requirements — MVP

### FR-1 · Pre-run UI (set up and launch)

| ID | Requirement |
|---|---|
| FR-1.1 | View all tests for a site, grouped **Site → Env → Module**, so the user sees what exists before choosing. |
| FR-1.2 | Build a run config on the spot: `site`, `env`, `url`, `auth`, `headless`. Config need not be saved — MVP builds it in memory. |
| FR-1.3 | Select *which* tests to run from the discovered set (narrow the `actions` slice). |
| FR-1.4 | Validate the config before launch and surface errors clearly (mirrors the runner's own validation rules). |
| FR-1.5 | Launch the run. |

> ✅ **Backend capability — built.** FR-1.1 needs **test discovery** (enumerating
> tests + traits). This is now the `IDiscovery` / `Discovery` component
> (`src/discovery`) — a leaf that reflects over the `[Trait]` attributes and returns
> `DiscoveredTest`s (`FullyQualifiedName`, `Site`, `Envs` multi-valued, `Kind`,
> nullable `Module`/`Category`). Exercisable now via `dotnet run -- discover`.
> Chosen approach: **reflection (Option B)** — no new dependency; the documented
> constraint is `[Fact]`/`[Theory]` + literal `[Trait]` (a `[Theory]` lists as one
> node). Swap to the VSTest TranslationLayer behind the same interface if per-data-row
> discovery is ever needed.

### FR-2 · Auth (both modes must work in MVP)

Auth is the trickiest area of the MVP because the two modes need very different
things from the UI, and neither is a plain "call and wait."

| ID | Requirement |
|---|---|
| FR-2.1 | **Manual mode** must work in MVP. The UI supplies the `waitForUser` callback the runner requires — in a GUI this is a "**I've logged in**" confirmation the user clicks after completing login in the headed browser. (Runner faults if no callback is supplied.) |
| FR-2.2 | **Auto mode** must work in MVP. For the chosen `site`+`env`, the UI drives the runner to locate and run that site's auto-login test — the runner composes the `Site={site}&Env={env}&Kind=Auth` selector, which **must match exactly one** login test (zero or multiple → fault). The UI must surface which login test was matched, or why matching failed. *Now resolvable up front via `IDiscovery`: filter discovered tests by `Kind=Auth` + `Site` + `Env` and check the count before launching.* |
| FR-2.3 | Auth failures are shown clearly as **faults** (bad credentials, no session saved, no/many matching login tests) and distinguished from ordinary test failures. |

> ⚠️ **Known-tricky.** Manual mode's callback (FR-2.1) means the live-run UI has to
> pause and wait on a user click mid-run. Auto mode (FR-2.2) means the UI must be
> able to see/resolve the site's login test up front — which ties back to test
> discovery (FR-1.1). Both are in MVP; both need care.

### FR-3 · Live-run UI (show what's happening)

| ID | Requirement |
|---|---|
| FR-3.1 | Show the runner's current **phase** (Reading Config → Verifying Env → Authenticating → Running Actions → Reporting). |
| FR-3.2 | Stream **per-test results** as they complete (pass/fail), not only at the end. |
| FR-3.3 | Show running **totals** (passed / failed / remaining). |
| FR-3.4 | Make it obvious the run is **alive vs. finished vs. faulted** — never looks frozen. |

### FR-4 · Post-run UI (results and artifacts)

| ID | Requirement |
|---|---|
| FR-4.1 | Show the overall **verdict**: PASSED / FAILED / FAULTED. |
| FR-4.2 | Show the **summary**: counts, duration, and (if faulted) *where* it faulted. |
| FR-4.3 | List **per-test results**. |
| FR-4.4 | Give access to **logs** (open/read the TRX / run logs). |
| FR-4.5 | Give access to **traces** (open the Playwright trace for a test). |
| FR-4.6 | Start a **new run** (return to FR-1). |

All of FR-4 is essentially "render a `RunResult` richly" — that object already
carries verdict, counts, per-test results, and artifact paths.

## Non-functional requirements

| ID | Requirement |
|---|---|
| NFR-1 | **Local desktop application** — runs on the user's own machine. |
| NFR-2 | **Full local access** — reads/writes the filesystem and executes commands (spawning the runner, opening traces). |
| NFR-3 | **Stays in the .NET / C# ecosystem** — no meaningful deviation into other language toolchains. |
| NFR-4 | **Extensible** — new functionality added later with ease; MVP features shouldn't need rewriting to grow. |
| NFR-5 | **Single repo** — frontend lives alongside the existing project (preferred; split only if impractical). |
| NFR-6 | **Single-user, single-machine** — one user at a time; no concurrency / race-condition design needed. |
| NFR-7 | **Native-quality feel** acceptable/preferred — as long as it works well and stays in C#. |

## Explicitly out of scope for MVP (parked for v2/v3)

- Saving / loading named configs (FR-1.2 stays in-memory for now).
- True per-test cherry-picking beyond group-level selection.
- Embedded / in-app trace viewer (MVP just launches the external one).
- Run history / comparing past runs.
- Any multi-user, hosted, or remote-machine scenario.

## Open questions

- **Exact UI tech.** NFR-3 + NFR-5 + NFR-7 (C#-native, same repo, native feel) are
  the deciding filter; the pick is deferred but must satisfy all three.
- **Manual-auth callback UX.** How the live-run UI pauses on FR-2.1 and resumes on
  the user's confirmation click.
- ~~**Test-discovery mechanism.**~~ **Resolved** — `IDiscovery` / `Discovery`
  (reflection, Option B). See FR-1.1.
