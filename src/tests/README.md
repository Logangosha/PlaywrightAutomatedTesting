# Writing tests

This is the guide for adding tests to the suite. It's the reference the runner and
the UI are built around — read it once top to bottom, then copy the patterns you need.

Everything here is **trait-driven**: you tag each test with `[Trait(...)]` attributes,
and the runner composes selectors from your config's `site`/`env` to decide what runs.
You never write `Site=`/`Env=`/`Kind=` by hand — only the tests carry them.

## How a test is organized

Every test class extends [`TestBase`](TestBase.cs), which hands you a ready
Playwright `Page` (browser launched, tracing on, session loaded if auth saved one)
and saves the trace + session on cleanup. Tests read all configuration from
[`TestSettings`](TestSettings.cs) (env vars the runner sets) — never from files, and
never hardcoded.

Two kinds of test:

- **Auth** (`Kind=Auth`) — the single login test per site+env that *auto* mode runs to
  establish a session. Carries **only** `Site` + `Env` + `Kind`.
- **Action** (`Kind=Action`) — the real tests. Start already logged in (the saved
  session is loaded), navigate, and assert. Add `Category` + `Module`.

## Trait convention

| Trait | On | Values | Meaning |
|---|---|---|---|
| `Site` | class | lowercase id (`portal`) | which project/site |
| `Env` | class | lowercase id (`dev`) | which environment — **one `[Trait("Env", …)]` per env** the same flow serves |
| `Kind` | class | `Auth` \| `Action` | login test vs real test |
| `Category` | **method** | `Smoke` \| `Regression` | severity/scope, one per `[Fact]` |
| `Module` | **class** | PascalCase (`HomePage`) | feature area — **also names the trace folder** |

Rules:

- **`Site`/`Env`/`Kind` are required on every test.** Auth carries *only* those three;
  Action adds `Category` + `Module`.
- **`Module` must be class-level** — `TestBase` reads it via reflection to build the
  trace path `traces/<run>/<Module>/<method>.zip`. A method-level `Module` is invisible.
- **`Category` is method-level** by convention (one per `[Fact]`).
- A test valid in several envs carries **one `Env` trait per env** (e.g. `dev` + `prod`).
  If two envs log in / behave differently, write separate classes instead.
- Keys are PascalCase; the fixed vocab (`Auth`/`Action`/`Smoke`/`Regression`) is PascalCase;
  site/env values are lowercase.

## Folder structure

Folders mirror the routing traits so the repo is navigable at a glance:

```
src/tests/<site>/<env>/auth/      ← the one login test
src/tests/<site>/<env>/actions/   ← action tests, one class per Module
```

Traits route; folders only organize. A multi-env test lives in its *home* env's folder
(the portal tests live in `portal/dev/` but also carry `Env=prod`).

## Adding a new site (recipe)

1. Create `src/tests/<site>/<env>/auth/` and `.../actions/`.
2. Write **exactly one** login test (below). Copy [`portal/dev/auth/PortalDevLogin.cs`](portal/dev/auth/PortalDevLogin.cs).
3. Write action tests (below). Copy [`portal/dev/actions/HomePageTests.cs`](portal/dev/actions/HomePageTests.cs).
4. Add a config in `configs/` pointing `site`/`env`/`url` at it (gitignored; copy `configs/example.json`).

No runner changes needed — routing is entirely trait-driven. Confirm the runner sees
your tests with `dotnet run -- discover`.

## Applying your changes (rebuild)

Tests are **compiled**, and the UI loads the test assembly at startup — so a new or
edited test only shows up after a **rebuild**. This is handled for you in two spots:

- **When you open the app:** launch it via **`launch.bat`** (or the desktop shortcut,
  which points at it). It **builds first, then launches**, so whatever you changed
  while the app was closed — added, edited, or deleted tests — is always current on
  open. (A clean build is quick; you only wait longer when something actually changed.
  If the build fails, it shows the errors and offers to launch your previous build.)
- **While the app is open:** editing anything under `src/tests` raises a banner —
  *"Tests changed — rebuild and restart to apply."* Click **Restart & rebuild**; the
  app closes, rebuilds, and relaunches (it can't rebuild itself while running because
  it holds the assembly locked).

Authoring from the **console** picks up changes automatically too — each
`dotnet run` / `dotnet test` is a fresh process that builds first. (Launching the raw
`RunnerUI.exe` directly is the one path that skips the build — use `launch.bat`.)

## Writing an Auth (login) test

The one test auto mode runs to establish a session, selected via
`Site={site}&Env={env}&Kind=Auth` — **exactly one** test must match (zero or many →
the run faults). It gets credentials as env vars and a save path; it logs in, asserts
success, and `TestBase` saves the session on cleanup. A failing assertion faults the
run (good — the user learns login broke instead of getting mysterious action failures).

```csharp
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace PlaywrightAutomatedTesting.Acme.Prod.Auth
{
    // Carries ONLY Site + Env + Kind. One [Trait("Env", …)] per env the SAME login
    // flow serves. If dev and prod log in differently, write two Auth classes.
    [Trait("Site", "acme")]
    [Trait("Env", "prod")]
    [Trait("Kind", "Auth")]
    public class AcmeProdLogin : TestBase
    {
        public AcmeProdLogin(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task Login_WithConfiguredCredentials_SavesAuthenticatedSession()
        {
            // --- ARRANGE ---
            // Credentials ALWAYS come from the config via env vars — never hardcode.
            // These getters throw clearly if run outside auto mode (the correct failure).
            var username = TestSettings.LoginUsername;
            var password = TestSettings.LoginPassword;

            await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // EDGE CASE — consent / cookie banners float over the login form on first
            // visit. Dismiss if present, but DON'T fail when absent (returning users
            // won't see it): check visibility with a short timeout, act only if shown.
            var cookieAccept = Page.GetByRole(AriaRole.Button, new() { Name = "Accept" });
            if (await cookieAccept.IsVisibleAsync())
                await cookieAccept.ClickAsync();

            // --- ACT ---
            // Prefer role/label locators (GetByRole/GetByLabel/GetByPlaceholder) over
            // CSS/XPath — they survive markup changes and read like the UI.
            //
            // EDGE CASE — two-step ("identifier-first") logins: fill email → click Next
            // → WaitForAsync(password field) → fill password.
            await Page.Locator("input[type='email'], input[type='text']").First.FillAsync(username);
            await Page.Locator("input[type='password']").First.FillAsync(password);

            // EDGE CASE — submit + navigation race. Awaiting the click alone can return
            // before the page settles (flaky). Kick off the wait and click together.
            await Task.WhenAll(
                Page.WaitForLoadStateAsync(LoadState.NetworkIdle),
                Page.Locator("button[type='submit'], input[type='submit']").First.ClickAsync()
            );

            // --- ASSERT — something ONLY true once logged in ---
            // Negative signal: after a good login the password field is gone. Wrong
            // credentials re-render the form and this fails — how Auth learns login failed.
            var passwordStillVisible = await Page.Locator("input[type='password']").IsVisibleAsync();
            Assert.False(passwordStillVisible,
                "Login appears to have failed — the password field is still visible.");

            // EDGE CASE — a page that stays put but shows an inline error banner. Surface
            // it so the failure says WHY, instead of a downstream action test failing oddly.
            var errorBanner = Page.GetByRole(AriaRole.Alert);
            if (await errorBanner.IsVisibleAsync())
                Assert.Fail($"Login returned an error: {await errorBanner.InnerTextAsync()}");

            // No explicit save needed — TestBase saves the session on DisposeAsync.
        }
    }
}
```

## Writing Action tests

The real work. Selected via `Site={site}&Env={env}&Kind=Action` (+ the config's
`actions` slice). They start **already logged in** — no login step, just navigate from
`TestSettings.BaseUrl`. One class == one `Module`; split unrelated features into
separate classes/files. Each `[Fact]`/`[Theory]` below is a reusable pattern.

```csharp
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace PlaywrightAutomatedTesting.Acme.Prod.Actions
{
    // Class-level: Site, Env (one per env), Kind=Action, Module. Method-level: Category.
    [Trait("Site", "acme")]
    [Trait("Env", "prod")]
    [Trait("Kind", "Action")]
    [Trait("Module", "Dashboard")]
    public class DashboardTests : TestBase
    {
        public DashboardTests(ITestOutputHelper output) : base(output) { }

        // BASELINE — the shape every action test follows: ARRANGE (navigate),
        // ACT (find), ASSERT (wait + verify). Role locators auto-wait.
        [Fact]
        [Trait("Category", "Smoke")]
        public async Task Dashboard_AfterLogin_HeadingIsVisible()
        {
            await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            var heading = Page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard" });

            await heading.WaitForAsync();   // clear "never appeared" failure
            Assert.True(await heading.IsVisibleAsync());
        }

        // EDGE CASE — dynamic / async content. Content loaded via XHR isn't there on
        // first query. Give the specific element an explicit timeout — NEVER Task.Delay
        // (fixed sleeps are the #1 cause of flaky/slow tests).
        [Fact]
        [Trait("Category", "Smoke")]
        public async Task Search_ForKnownRecord_ShowsMatchingRow()
        {
            await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            var search = Page.GetByRole(AriaRole.Textbox, new() { Name = "Search..." });
            await search.FillAsync("known record");

            await Task.WhenAll(
                Page.WaitForLoadStateAsync(LoadState.NetworkIdle),
                search.PressAsync("Enter")
            );

            // Filter to the row you expect — more robust than an index like .Nth(3).
            var row = Page.Locator("tr").Filter(new() { HasText = "known record" });

            await row.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            Assert.True(await row.IsVisibleAsync());
        }

        // EDGE CASE — asserting a NEGATIVE (something must be absent). You can't
        // WaitForAsync(Visible) on what shouldn't exist (it just times out). Assert
        // the count is zero — CountAsync resolves immediately, no 30s timeout.
        [Fact]
        [Trait("Category", "Regression")]
        public async Task RestrictedArea_AsStandardUser_IsNotShown()
        {
            await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            var adminLink = Page.GetByRole(AriaRole.Link, new() { Name = "Admin Settings" });
            Assert.Equal(0, await adminLink.CountAsync());
        }

        // EDGE CASE — optional / conditional UI (a dismissable banner, first-run tooltip).
        // Branch on visibility; don't hard-assert its presence.
        [Fact]
        [Trait("Category", "Regression")]
        public async Task Notifications_WhenPresent_CanBeDismissed()
        {
            await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            var banner = Page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "notification" });
            if (await banner.IsVisibleAsync())
            {
                await Page.GetByRole(AriaRole.Button, new() { Name = "Dismiss" }).ClickAsync();
                await banner.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
                Assert.False(await banner.IsVisibleAsync());
            }
            // Nothing to do when absent — the test still passes, correctly.
        }

        // EDGE CASE — native dialogs (confirm/alert/beforeunload). Playwright
        // auto-DISMISSES them unless you register a handler FIRST — before the click
        // that triggers the dialog, or the click hangs/cancels.
        [Fact]
        [Trait("Category", "Regression")]
        public async Task DeleteItem_Confirmed_RemovesItem()
        {
            await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            Page.Dialog += async (_, dialog) => await dialog.AcceptAsync();  // register FIRST

            await Page.GetByRole(AriaRole.Button, new() { Name = "Delete" }).First.ClickAsync();

            var confirmation = Page.GetByText("Item deleted");
            await confirmation.WaitForAsync();
            Assert.True(await confirmation.IsVisibleAsync());
        }

        // EDGE CASE — data-driven coverage with [Theory]. One method, many inputs; each
        // [InlineData] is a separate case in the TRX with its own pass/fail.
        //
        // ⚠️ Discovery note: the UI/`discover` list a [Theory] as a SINGLE node (its data
        // rows aren't enumerated) — it still RUNS every row. See CLAUDE.md.
        [Theory]
        [Trait("Category", "Regression")]
        [InlineData("Reports")]
        [InlineData("Settings")]
        [InlineData("Profile")]
        public async Task NavMenu_EachEntry_IsReachable(string navLabel)
        {
            await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            var link = Page.GetByRole(AriaRole.Link, new() { Name = navLabel });
            await link.WaitForAsync();
            Assert.True(await link.IsVisibleAsync(), $"Nav entry '{navLabel}' was not visible.");
        }
    }
}
```

## Rules of thumb

- **Prefer role/label locators** (`GetByRole`, `GetByLabel`, `GetByText`) — they
  auto-wait and survive markup changes better than CSS/XPath.
- **Never use fixed sleeps** (`Task.Delay`). Wait for the specific element/state with a
  timeout, or pair a network wait with the action via `Task.WhenAll`.
- **Assert something only true in the target state**, and phrase the message so a
  failure explains *why*.
- **One class per `Module`**; keep the class's traits accurate — they drive both what
  runs and where traces land.

For the deeper conventions and gotchas, see the repo-root [CLAUDE.md](../../CLAUDE.md).
