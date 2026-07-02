using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace PlaywrightAutomatedTesting.MemberManagement.Tests
{
    [Trait("Module", "MemberManagement")]
    public class MemberSearchTests : TestBase
    {
        [Fact]
        [Trait("Category", "Smoke")]
        public async Task MemberSearch_SearchExistingMember_DisplaysMatchingMember()
        {
            // --- ARRANGE ---
            var baseUrl = EnvironmentProvider.Current.BaseUrl;

            await Page.GotoAsync(baseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // --- ACT ---
            var globalSearch = Page.GetByRole(AriaRole.Textbox, new() { Name = "Search..." });

            await globalSearch.FillAsync("member search");

            await Task.WhenAll(
                Page.WaitForLoadStateAsync(LoadState.NetworkIdle),
                globalSearch.PressAsync("Enter")
            );

            await Page.GetByRole(AriaRole.Link, new() { Name = " Member Search" })
                .Nth(1)
                .ClickAsync();

            var memberName = Page.GetByRole(AriaRole.Textbox, new() { Name = "Member Name" });

            await memberName.FillAsync("sam willis");

            await Page.GetByRole(AriaRole.Button, new() { Name = " Search" })
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

            Assert.True(await memberRow.IsVisibleAsync());
        }
    }
}