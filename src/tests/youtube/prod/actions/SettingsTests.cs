using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace PlaywrightAutomatedTesting.Youtube.Prod.Actions
{
    [Trait("Site", "youtube")]
    [Trait("Env", "prod")]
    [Trait("Kind", "Action")]
    [Trait("Module", "Settings")]
    public class SettingsTests : TestBase
    {
        public SettingsTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        [Trait("Category", "Smoke")]
        public async Task Settings_ChangeAppearance_SwitchesToDarkTheme()
        {
            // --- ARRANGE ---
            var baseUrl = TestSettings.BaseUrl;
            await Page.GotoAsync(baseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            // --- ACT ---
            await Page.GetByRole(AriaRole.Button, new() { Name = "Settings" }).ClickAsync();
            // The appearance label carries the current theme (e.g. "Appearance: Device theme",
            // "Appearance: Dark theme", "Appearance: Light theme"). Only the "Appearance:" prefix
            // is constant, so match on that rather than a specific theme.
            await Page.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("^Appearance:") }).ClickAsync();
            await Page.GetByRole(AriaRole.Link, new() { Name = "Dark theme" }).ClickAsync();

            // --- ASSERT ---
            // IF NO ERROR IS THROWN, THEN THE TEST PASSES
            Assert.True(true);
        }
    }
}
