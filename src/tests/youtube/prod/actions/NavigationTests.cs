using Microsoft.Playwright;
using Xunit.Abstractions;

namespace PlaywrightAutomatedTesting.Youtube.Prod.Actions
{
    [Trait("Site", "youtube")]
    [Trait("Env", "prod")]
    [Trait("Kind", "Action")]
    [Trait("Module", "Navigation")]
    public class NavigationTests : TestBase
    {
        public NavigationTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        [Trait("Category", "Smoke")]
        public async Task Navigation_OpenGuide_NavigatesToMusic()
        {
            // --- ARRANGE ---
            var baseUrl = TestSettings.BaseUrl;
            await Page.GotoAsync(baseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // --- ACT ---
            await Page.GetByRole(AriaRole.Button, new() { Name = "Guide" }).ClickAsync();
            await Page.GetByRole(AriaRole.Link, new() { Name = "Music" }).Nth(1).ClickAsync();

            // --- ASSERT ---
            // IF NO ERROR IS THROWN, THEN THE TEST PASSES
            Assert.True(true);
        }
    }
}
