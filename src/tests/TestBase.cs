using Microsoft.Playwright;
using Xunit;
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
        var moduleTraitValue = GetModuleTraitValue();
        var testMethodName = GetTestMethodName();
        var testRunTimestamp = TestRunContext.Current["testRunTimestamp"];

        var traceDirectory = Path.Combine(
            Directory.GetCurrentDirectory(), "..", "..", "..", "traces",
            testRunTimestamp,
            moduleTraitValue);

        Directory.CreateDirectory(traceDirectory);

        var tracePath = Path.Combine(traceDirectory, $"{testMethodName}.zip");

        await Context.Tracing.StopAsync(new() { Path = tracePath });

        await Context.CloseAsync();
        await Browser.CloseAsync();
        Playwright.Dispose();
    }

    // Reads the [Trait("Module", "...")] value from metadata.
    // xUnit's TraitAttribute has an empty constructor body, so the values are not
    // stored in instance fields — we must read the constructor arguments directly.
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

    // Retrieves the currently-running test method name. DisposeAsync is invoked by
    // the xUnit framework (not from the test call stack), so we reflect into the
    // ITestOutputHelper's internal ITest to get the real method name.
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
