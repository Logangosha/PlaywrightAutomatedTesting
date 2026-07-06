using Microsoft.Playwright;
using Xunit.Abstractions;

namespace PlaywrightAutomatedTesting.Example.Example.Actions
{
    // ============================================================================
    // TEMPLATE / EXAMPLE — ACTION tests (the real work: navigate + assert).
    //
    // Copy this file when adding action tests for a new site+env (step 3 of the
    // "Adding a new site" recipe in CLAUDE.md). Each [Fact]/[Theory] below is a
    // self-contained example of one pattern or edge case — keep the ones you need.
    //
    // What an Action test IS:
    //   - Selected via Site={site}&Env={env}&Kind=Action (+ the config's actions slice).
    //   - Starts already logged in: TestBase loads the session that Auth saved, so
    //     there is NO login step here — just navigate from TestSettings.BaseUrl.
    //
    // Trait rules for an Action test (see CLAUDE.md "Trait convention"):
    //   - Class-level: Site, Env (one per env), Kind=Action, and Module.
    //     Module MUST be class-level — TestBase reads it via reflection to build the
    //     trace path (traces/<run>/<Module>/<method>.zip). A method-level Module is
    //     invisible to trace pathing.
    //   - Method-level: Category ("Smoke" | "Regression"), one per [Fact], by convention.
    //   - One class == one Module. Split unrelated features into separate classes/files.
    // ============================================================================
    [Trait("Site", "example")]
    [Trait("Env", "example")]
    [Trait("Env", "prod")]
    [Trait("Kind", "Action")]
    [Trait("Module", "ExampleModule")]
    public class ExampleActionTests : TestBase
    {
        public ExampleActionTests(ITestOutputHelper output) : base(output) { }

        // ------------------------------------------------------------------------
        // BASELINE — the shape every action test follows: ARRANGE (navigate),
        // ACT (find), ASSERT (wait + verify). Prefer role locators; they auto-wait.
        // ------------------------------------------------------------------------
        [Fact]
        [Trait("Category", "Smoke")]
        public async Task Dashboard_AfterLogin_HeadingIsVisible()
        {
            // --- ARRANGE ---
            await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // --- ACT ---
            var heading = Page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard" });

            // --- ASSERT ---
            // WaitForAsync gives a clear "element never appeared" failure. Assertions
            // like Expect(...).ToBeVisibleAsync() also auto-retry — either is fine.
            await heading.WaitForAsync();
            Assert.True(await heading.IsVisibleAsync());
        }

        // ------------------------------------------------------------------------
        // EDGE CASE — dynamic / async content.
        // Content that loads after an XHR won't be there on first query. Give the
        // specific element an explicit timeout instead of sprinkling Task.Delay
        // (fixed sleeps are the #1 source of flaky/slow tests — never use them).
        // ------------------------------------------------------------------------
        [Fact]
        [Trait("Category", "Smoke")]
        public async Task Search_ForKnownRecord_ShowsMatchingRow()
        {
            // --- ARRANGE ---
            await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // --- ACT ---
            var search = Page.GetByRole(AriaRole.Textbox, new() { Name = "Search..." });
            await search.FillAsync("known record");

            // Pair the network wait with the action so we don't query results too early.
            await Task.WhenAll(
                Page.WaitForLoadStateAsync(LoadState.NetworkIdle),
                search.PressAsync("Enter")
            );

            // Filtering to the row you expect is more robust than an index like .Nth(3).
            var row = Page.Locator("tr").Filter(new() { HasText = "known record" });

            // --- ASSERT ---
            await row.WaitForAsync(new()
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5000
            });
            Assert.True(await row.IsVisibleAsync());
        }

        // ------------------------------------------------------------------------
        // EDGE CASE — asserting a NEGATIVE (something must be absent).
        // You can't WaitForAsync(Visible) on something that shouldn't exist — that
        // just times out. Assert the count is zero, or wait for the Hidden state.
        // ------------------------------------------------------------------------
        [Fact]
        [Trait("Category", "Regression")]
        public async Task RestrictedArea_AsStandardUser_IsNotShown()
        {
            // --- ARRANGE ---
            await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // --- ACT ---
            var adminLink = Page.GetByRole(AriaRole.Link, new() { Name = "Admin Settings" });

            // --- ASSERT ---
            // CountAsync resolves immediately (no wait for appearance), so a normally
            // absent element doesn't cost you a 30s timeout.
            Assert.Equal(0, await adminLink.CountAsync());
        }

        // ------------------------------------------------------------------------
        // EDGE CASE — optional / conditional UI.
        // An element that may or may not be there (a "dismiss" banner, a first-run
        // tooltip). Branch on visibility; don't hard-assert its presence.
        // ------------------------------------------------------------------------
        [Fact]
        [Trait("Category", "Regression")]
        public async Task Notifications_WhenPresent_CanBeDismissed()
        {
            // --- ARRANGE ---
            await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // --- ACT / ASSERT ---
            var banner = Page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "notification" });
            if (await banner.IsVisibleAsync())
            {
                await Page.GetByRole(AriaRole.Button, new() { Name = "Dismiss" }).ClickAsync();
                await banner.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
                Assert.False(await banner.IsVisibleAsync());
            }
            // Nothing to do when it's absent — the test still passes, correctly.
        }

        // ------------------------------------------------------------------------
        // EDGE CASE — native dialogs (confirm/alert/beforeunload).
        // Playwright auto-DISMISSES dialogs unless you register a handler FIRST.
        // Register before the click that triggers it, or the click hangs/cancels.
        // ------------------------------------------------------------------------
        [Fact]
        [Trait("Category", "Regression")]
        public async Task DeleteItem_Confirmed_RemovesItem()
        {
            // --- ARRANGE ---
            await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // Register the handler BEFORE triggering the dialog.
            Page.Dialog += async (_, dialog) => await dialog.AcceptAsync();

            // --- ACT ---
            await Page.GetByRole(AriaRole.Button, new() { Name = "Delete" }).First.ClickAsync();

            // --- ASSERT ---
            var confirmation = Page.GetByText("Item deleted");
            await confirmation.WaitForAsync();
            Assert.True(await confirmation.IsVisibleAsync());
        }

        // ------------------------------------------------------------------------
        // EDGE CASE — data-driven coverage with [Theory].
        // One method, many inputs. Each [InlineData] is a separate test case in the
        // TRX with its own pass/fail. Keeps you from copy-pasting near-identical Facts.
        // ------------------------------------------------------------------------
        [Theory]
        [Trait("Category", "Regression")]
        [InlineData("Reports")]
        [InlineData("Settings")]
        [InlineData("Profile")]
        public async Task NavMenu_EachEntry_IsReachable(string navLabel)
        {
            // --- ARRANGE ---
            await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // --- ACT ---
            var link = Page.GetByRole(AriaRole.Link, new() { Name = navLabel });

            // --- ASSERT ---
            await link.WaitForAsync();
            Assert.True(await link.IsVisibleAsync(), $"Nav entry '{navLabel}' was not visible.");
        }
    }
}
