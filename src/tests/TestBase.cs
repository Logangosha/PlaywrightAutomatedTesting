using Microsoft.Playwright;
using Xunit;

public class TestBase : IAsyncLifetime
{
    protected IPlaywright Playwright = null!;
    protected IBrowser Browser = null!;
    protected IBrowserContext Context = null!;
    protected IPage Page = null!;

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        Browser = await Playwright.Chromium.LaunchAsync(new()
        {
            Headless = TestRunContext.Current["headless"] == "true"      
        });

        Context = await Browser.NewContextAsync(new()
        {
            StorageStatePath = AuthenticationProvider.StorageStatePath
        });

        await Context.Tracing.StartAsync(new()
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        Page = await Context.NewPageAsync();
        Page.SetDefaultTimeout(30000);
        Page.SetDefaultNavigationTimeout(30000);
    }

    public async Task DisposeAsync()
    {

        var testName = GetType().Name;

        await Context.Tracing.StopAsync(new() 
        { 
            Path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "traces", $"{testName}--{DateTime.Now:yyyyMMddHHmmss}.zip")
        });
        
        await Context.CloseAsync();
        await Browser.CloseAsync();
        Playwright.Dispose();
    }
}