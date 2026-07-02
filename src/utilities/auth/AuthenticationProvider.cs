using Microsoft.Playwright;

public static class AuthenticationProvider
{
    public static string StorageStatePath =>
        Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "src",
                "utilities",
                "auth",
                "storageState.json"));
    public static async Task AuthenticateAsync(string authMode)
    {
        switch (authMode.ToLower())
        {
            case "manual":
                await ManualLoginAsync();
                break;

            case "auto":
                await AutoLoginAsync();
                break;

            default:
                throw new Exception($"Unknown auth mode '{authMode}'.");
        }
    }

    private static async Task ManualLoginAsync()
    {
        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = false
        });

        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(EnvironmentProvider.Current.BaseUrl);

        Console.WriteLine();
        Console.WriteLine("Please log into the application.");
        Console.WriteLine("Once you are on the home page, press ENTER...");

        Console.ReadLine();
        await Task.Delay(1000);

        Directory.CreateDirectory(Path.GetDirectoryName(StorageStatePath)!);

        await context.StorageStateAsync(new()
        {
            Path = StorageStatePath
        });
    }

    private static async Task AutoLoginAsync()
    {
    }
}