using Microsoft.Playwright;
using Xunit.Abstractions;

namespace PlaywrightAutomatedTesting.Example.Example.Auth
{
    // ============================================================================
    // TEMPLATE / EXAMPLE — AUTH (login) test.
    //
    // Copy this file when adding the login test for a new site+env (step 2 of the
    // "Adding a new site" recipe in CLAUDE.md). It is heavily commented on purpose:
    // read it top to bottom once, then delete the commentary you don't need.
    //
    // What an Auth test IS:
    //   - The ONE test auto mode runs to establish a session for a site+env.
    //   - Selected by the runner via the composed selector Site={site}&Env={env}&Kind=Auth.
    //     EXACTLY ONE test must match that selector (zero or many => the run faults).
    //   - Given credentials as env vars (TestSettings.LoginUsername/Password) and a
    //     SAVE_STORAGE_STATE path. It logs in, asserts success, and TestBase saves
    //     the session on cleanup. A failing assertion => Auth faults the run (good:
    //     the user learns login broke instead of getting mysterious action failures).
    //
    // Trait rules for an Auth test (see CLAUDE.md "Trait convention"):
    //   - Carries ONLY Site + Env + Kind. NEVER Category or Module.
    //   - site/env values are lowercase; Kind is the fixed vocab value "Auth".
    //   - One [Trait("Env", ...)] per env the SAME login flow serves (dev+prod here).
    //     If dev and prod log in differently, write two separate Auth classes instead.
    //
    // To make this a real login test: change the namespace + traits to your site/env,
    // then replace the CUSTOMIZE sections with your site's login UI.
    // ============================================================================
    [Trait("Site", "example")]
    [Trait("Env", "example")]
    [Trait("Kind", "Auth")]
    public class ExampleLogin : TestBase
    {
        public ExampleLogin(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task Login_WithConfiguredCredentials_SavesAuthenticatedSession()
        {
            // --- ARRANGE ---
            // Credentials ALWAYS come from the config via Auth (env vars) — never
            // hardcode them here. These getters throw with a clear message if the
            // test was run outside auto mode, which is the correct failure.
            var username = TestSettings.LoginUsername;
            var password = TestSettings.LoginPassword;

            await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // EDGE CASE — consent / cookie banners.
            // Many sites float a cookie or consent dialog over the login form on first
            // visit; clicking "email" underneath it silently fails. Dismiss it if present,
            // but DON'T fail when it's absent (returning users won't see it). Pattern:
            // check visibility with a short timeout, act only if shown.
            var cookieAccept = Page.GetByRole(AriaRole.Button, new() { Name = "Accept" });
            if (await cookieAccept.IsVisibleAsync())
                await cookieAccept.ClickAsync();

            // --- ACT ---
            // CUSTOMIZE: drive the site's login UI.
            //
            // Prefer role/label locators (GetByRole, GetByLabel, GetByPlaceholder) over
            // CSS/XPath — they survive markup changes and read like the UI. The generic
            // attribute selectors below are a starting point; tighten them to your site.
            //
            // EDGE CASE — two-step ("identifier-first") logins: some sites take the email,
            // click Next, THEN reveal the password field. If so, split this into:
            //   fill email -> click Next -> WaitForAsync(password field) -> fill password.
            await Page.Locator("input[type='email'], input[type='text']").First.FillAsync(username);
            await Page.Locator("input[type='password']").First.FillAsync(password);

            // EDGE CASE — submit + navigation race.
            // Clicking submit triggers a navigation. Awaiting the click alone can return
            // before the page settles, making the assertion flaky. Kick off the wait and
            // the click together so we don't miss a fast redirect.
            await Task.WhenAll(
                Page.WaitForLoadStateAsync(LoadState.NetworkIdle),
                Page.Locator("button[type='submit'], input[type='submit']").First.ClickAsync()
            );

            // --- ASSERT ---
            // CUSTOMIZE: assert something that is ONLY true once logged in.
            //
            // Two complementary checks below — keep whichever fits your site:
            //
            // 1) Negative signal: after a good login the password field is gone. If the
            //    credentials were wrong the form re-renders and this fails — which is
            //    exactly how Auth learns the login failed and faults the run.
            var passwordStillVisible = await Page.Locator("input[type='password']").IsVisibleAsync();
            Assert.False(passwordStillVisible,
                "Login appears to have failed — the password field is still visible.");

            // 2) EDGE CASE — a page that stays put but shows an inline error.
            //    Some sites don't navigate on bad credentials; they render an error banner.
            //    Surface it explicitly so the failure message tells the user WHY, instead
            //    of a downstream action test failing for a confusing reason.
            var errorBanner = Page.GetByRole(AriaRole.Alert);
            if (await errorBanner.IsVisibleAsync())
            {
                var message = await errorBanner.InnerTextAsync();
                Assert.Fail($"Login returned an error: {message}");
            }

            // No explicit save needed: TestBase saves the session (SAVE_STORAGE_STATE)
            // on DisposeAsync. That handoff is how the session reaches the action run.
        }
    }
}
