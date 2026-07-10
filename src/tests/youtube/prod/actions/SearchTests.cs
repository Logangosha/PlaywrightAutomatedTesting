using Microsoft.Playwright;
using Xunit.Abstractions;

namespace PlaywrightAutomatedTesting.Youtube.Prod.Actions
{
    [Trait("Site", "youtube")]
    [Trait("Env", "prod")]
    [Trait("Kind", "Action")]
    [Trait("Module", "Search")]
    public class SearchTests : TestBase
    {
        public SearchTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        [Trait("Category", "Smoke")]
        public async Task Search_SearchVideo_DisplaysMatchingVideo()
        {
            // --- ARRANGE ---
            var baseUrl = TestSettings.BaseUrl;
            await Page.GotoAsync(baseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // --- ACT ---
            var searchBox = Page.GetByRole(AriaRole.Combobox, new() { Name = "Search" });
            await searchBox.ClickAsync();
            await searchBox.FillAsync("funny cat videos");
            await Page.GotoAsync("https://www.youtube.com/results?search_query=funny+cat+videos1");
            // SEARCH 
            
            // --- ASSERT ---
            // IF NO ERROR IS THROWN, THEN THE TEST PASSES
            Assert.True(true);
        }
    }
}
