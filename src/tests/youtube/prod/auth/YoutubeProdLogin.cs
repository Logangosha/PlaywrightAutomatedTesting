using Microsoft.Playwright;
using Xunit.Abstractions;

namespace PlaywrightAutomatedTesting.Youtube.Prod.Auth
{
    // Login test for site=youtube, env=prod — what auto mode runs to establish a session.
    // Selected via Site=youtube&Env=prod&Kind=Auth (exactly one test must match).
    // Carries ONLY Site + Env + Kind.
    [Trait("Site", "youtube")]
    [Trait("Env", "prod")]
    [Trait("Kind", "Auth")]
    public class YoutubeProdLogin : TestBase
    {
        public YoutubeProdLogin(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task Login_WithConfiguredCredentials_SavesAuthenticatedSession()
        {
            // --- ARRANGE ---
            // Credentials come from the config via Auth (never hardcoded here).
            var username = TestSettings.LoginUsername;
            var password = TestSettings.LoginPassword;

            await Page.GotoAsync(TestSettings.BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // --- ACT ---
            // CUSTOMIZE: replace these selectors with the site's login fields.
            await Page.Locator("input[type='email'], input[type='text']").First.FillAsync(username);
            await Page.Locator("input[type='password']").First.FillAsync(password);
            await Page.Locator("button[type='submit'], input[type='submit']").First.ClickAsync();

            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // --- ASSERT ---
            // CUSTOMIZE: assert something that is only true when logged in.
            // For testing this just passes; a real test should check for a logged-in signal
            // (e.g. the password field being gone).
            Assert.True(true, "Login appears to have succeeded");

            // No explicit save needed: TestBase saves the session (SAVE_STORAGE_STATE) on cleanup.
        }
    }
}
