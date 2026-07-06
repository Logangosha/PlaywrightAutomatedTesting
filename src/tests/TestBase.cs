using Microsoft.Playwright;
using Xunit.Abstractions;
using System.Reflection;

public class TestBase : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;

    protected IPlaywright Playwright = null!;
    protected IBrowser Browser = null!;
    protected IBrowserContext Context = null!;
    protected IPage Page = null!;

    public TestBase(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        Browser = await Playwright.Chromium.LaunchAsync(new()
        {
            Headless = TestSettings.Headless
        });

        var contextOptions = new BrowserNewContextOptions();
        if (TestSettings.StorageStatePath is not null)
            contextOptions.StorageStatePath = TestSettings.StorageStatePath;

        Context = await Browser.NewContextAsync(contextOptions);

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
        var traceDirectory = Path.Combine(
            Paths.TracesRoot,
            TestSettings.RunTimestamp,
            GetModuleTraitValue());

        Directory.CreateDirectory(traceDirectory);

        var tracePath = Path.Combine(traceDirectory, $"{GetTestMethodName()}.zip");

        await Context.Tracing.StopAsync(new() { Path = tracePath });

        if (TestSettings.SaveStorageStatePath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TestSettings.SaveStorageStatePath)!);
            await Context.StorageStateAsync(new() { Path = TestSettings.SaveStorageStatePath });
        }

        await Context.CloseAsync();
        await Browser.CloseAsync();
        Playwright.Dispose();
    }

    private string GetModuleTraitValue()
    {
        foreach (var data in GetType().GetCustomAttributesData())
        {
            if (data.AttributeType != typeof(TraitAttribute))
                continue;

            var args = data.ConstructorArguments;
            if (args.Count == 2 &&
                args[0].Value?.ToString() == "Module")
            {
                var value = args[1].Value?.ToString();
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
        }

        return "Uncategorized";
    }

    private string GetTestMethodName()
    {
        var testField = _output.GetType()
            .GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);

        if (testField?.GetValue(_output) is ITest test)
        {
            var methodName = test.TestCase?.TestMethod?.Method?.Name;
            if (!string.IsNullOrEmpty(methodName))
                return methodName;
        }

        return "UnknownTest";
    }
}
