using Microsoft.Playwright;
using Xunit.Abstractions;

namespace PlaywrightAutomatedTesting.Portal.Dev.Actions
{
    [Trait("Site", "portal")]
    [Trait("Env", "dev")]
    [Trait("Env", "prod")]
    [Trait("Kind", "Action")]
    [Trait("Module", "Test")]
    public class Test : TestBase
    {
        public Test(ITestOutputHelper output) : base(output) { }

        [Fact]
        [Trait("Category", "Smoke")]
        public async Task Test_Test()
        {
            // --- ARRANGE ---
            var baseUrl = TestSettings.BaseUrl;

            await Page.GotoAsync(baseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // --- ACT ---
            var globalSearch = Page.GetByRole(AriaRole.Textbox, new() { Name = "Search..." });

            await globalSearch.FillAsync("member search");

            await Task.WhenAll(
                Page.WaitForLoadStateAsync(LoadState.NetworkIdle),
                globalSearch.PressAsync("Enter")
            );

            await Page.GetByRole(AriaRole.Link, new() { Name = " Member Search" })
                .Nth(1)
                .ClickAsync();

            var memberName = Page.GetByRole(AriaRole.Textbox, new() { Name = "Member Name" });

            await memberName.FillAsync("sam willis");

            await Page.GetByRole(AriaRole.Button, new() { Name = " Search" })
                .ClickAsync();

            var memberRow = Page.Locator("tr").Filter(new()
            {
                HasText = "Sam Willis"
            });

            // --- ASSERT ---
            await memberRow.WaitForAsync(new()
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5000
            });

            Console.WriteLine($"Hello world!");

            Assert.True(await memberRow.IsVisibleAsync());
        }
    }
}
