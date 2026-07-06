using Microsoft.Playwright;
using Xunit.Abstractions;

namespace PlaywrightAutomatedTesting.Portal.Dev.Actions
{
    [Trait("Site", "portal")]
    [Trait("Env", "dev")]
    [Trait("Env", "prod")]
    [Trait("Kind", "Action")]
    [Trait("Module", "HomePage")]
    public class HomePageTests : TestBase
    {
        public HomePageTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        [Trait("Category", "Smoke")]
        public async Task Auth_PreTestState_UserIsLoggedInAndSeesWelcomeMessage()
        {
            // --- ARRANGE ---
            // Pre-auth already handled by storage state in TestBase
            var baseUrl = TestSettings.BaseUrl;

            // --- ACT ---
            await Page.GotoAsync(baseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            var welcomeHeader = Page.Locator("xpath=/html/body/div[1]/div[2]/div[1]/div/div[2]/h4");

            // --- ASSERT ---
            await welcomeHeader.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5000
            });

            var text = await welcomeHeader.InnerTextAsync();

            Assert.StartsWith("Welcome back,", text);
            Assert.EndsWith("!", text);
        }

        [Fact]
        [Trait("Category", "Smoke")]
        public async Task MemberManagement_AfterLogin_PageHeadingIsVisible()
        {
            // --- ARRANGE ---
            var baseUrl = TestSettings.BaseUrl;

            // --- ACT ---
            await Page.GotoAsync(baseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            var heading = Page.GetByRole(AriaRole.Heading, new() { Name = "Member Management" });

            // --- ASSERT ---
            await heading.WaitForAsync();

            Assert.True(await heading.IsVisibleAsync());
        }

        [Fact]
        [Trait("Category", "Regression")]
        public async Task MemberSearch_AfterLogin_SearchTabIsVisible()
        {
            // --- ARRANGE ---
            var baseUrl = TestSettings.BaseUrl;

            // --- ACT ---
            await Page.GotoAsync(baseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            var memberSearch = Page
                .Locator("#page0")
                .GetByText("Member Search", new() { Exact = true });

            // --- ASSERT ---
            await memberSearch.WaitForAsync();

            Assert.True(await memberSearch.IsVisibleAsync());
        }
    }
}
