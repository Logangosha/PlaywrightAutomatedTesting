using Microsoft.Playwright;

// ESTABLISHES A SESSION AND SAVES IT TO STORAGESTATE.JSON. 
// THREE MODES: 
// NONE — NO LOGIN; RETURNS NULL (NO SESSION).
// MANUAL — OPENS A HEADED BROWSER, WAITS FOR THE USER TO LOG IN, SAVES THE SESSION.
// AUTO — RUNS THE SITE'S LOGIN TEST (SELECTED BY SITE + ENV + KIND=AUTH)
//      WHICH LOGS IN AND LETS TESTBASE SAVE THE SESSION. 
//      WEBSITE-AGNOSTIC: EACH SITE OWNS ITS LOGIN TEST; THE RUNNER ONLY COMPOSES THE SELECTOR.
public class Auth : IAuth
{
    // _waitForUser IS A CALLBACK SUPPLIED BY THE FRONTEND (CLI, API, ...) THAT BLOCKS UNTIL THE USER CONFIRMS THEY ARE LOGGED IN.
    // IT IS ONLY USED IN MANUAL MODE; AUTO AND NONE DO NOT REQUIRE IT.
    private readonly Func<string, Task>? _waitForUser;

    public Auth(Func<string, Task>? waitForUser = null)
    {
        _waitForUser = waitForUser;
    }

    public async Task<string?> AuthenticateAsync(IConfig config, string timestamp, Action<string>? onProgress = null)
    {
        return config.Auth.Mode.ToLowerInvariant() switch
        {
            "none" => null,
            "manual" => await ManualLoginAsync(config),
            "auto" => await AutoLoginAsync(config, timestamp, onProgress),
            _ => throw new Exception($"Unknown auth mode '{config.Auth.Mode}'.")
        };
    }

    private async Task<string> ManualLoginAsync(IConfig config)
    {
        if (_waitForUser is null)
            throw new Exception(
                "auth.mode is 'manual' but this frontend cannot wait for a user. " +
                "Use auto or none, or run from an interactive frontend.");

        // OPEN A HEADED BROWSER AND NAVIGATE TO THE SITE. 
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(config.Url);

        // BLOCK UNTIL THE USER CONFIRMS THEY ARE LOGGED IN (E.G., BY PRESSING ENTER IN THE CLI).
        await _waitForUser("Log into the application in the opened browser.");

        // WAIT A BIT TO ENSURE THE LOGIN IS COMPLETE AND THE SESSION IS STABLE, THEN SAVE IT AND RETURN THE PATH.
        await Task.Delay(1000);
        Directory.CreateDirectory(Path.GetDirectoryName(Paths.StorageStatePath)!);
        await context.StorageStateAsync(new() { Path = Paths.StorageStatePath });
        return Paths.StorageStatePath;
    }

    // AUTO MODE RUNS THE SITE'S LOGIN TEST, SELECTED BY SITE + ENV + KIND=AUTH.
    private async Task<string> AutoLoginAsync(IConfig config, string timestamp, Action<string>? onProgress)
    {
        var selector = $"Site={config.Site}&Env={config.Env}&Kind=Auth";
        var env = new Dictionary<string, string>
        {
            ["BASE_URL"] = config.Url,
            ["HEADLESS"] = config.Headless ? "true" : "false",
            ["RUN_TIMESTAMP"] = timestamp,
            ["LOGIN_USERNAME"] = config.Auth.Username!,
            ["LOGIN_PASSWORD"] = config.Auth.Password!,
            ["SAVE_STORAGE_STATE"] = Paths.StorageStatePath
        };
        var resultsDir = Path.Combine(Paths.ResultsRoot, timestamp, "auth");

        // RUN THE LOGIN TESTS IN A SEPARATE PROCESS, PASSING THE ENV AND RESULTS DIR.
        var (tests, logPath) = await TestProcess.RunAsync(selector, resultsDir, env, onProgress);

        // CHECK IF NO LOGIN TESTS WERE FOUND AND THROW AN EXCEPTION IF SO.
        if (tests.Count == 0)
            throw new Exception(
                $"No login test found for '{selector}'. Write one tagged " +
                $"[Trait(\"Site\",\"{config.Site}\")] [Trait(\"Env\",\"{config.Env}\")] [Trait(\"Kind\",\"Auth\")]. " +
                $"Log: {logPath}");

        // CHECK IF MULTIPLE LOGIN TESTS WERE FOUND AND THROW AN EXCEPTION IF SO.
        if (tests.Count > 1)
            throw new Exception(
                $"Login selector '{selector}' matched {tests.Count} tests, expected exactly 1: " +
                string.Join(", ", tests.Select(t => t.Name)));

        // CHECK IF THE LOGIN TEST FAILED AND THROW AN EXCEPTION WITH THE ERROR MESSAGE.
        var failed = tests.FirstOrDefault(t => t.Failed);
        if (failed is not null)
            throw new Exception($"Login failed in '{failed.Name}': {FirstLine(failed.Error)}");

        // ENSURE THE LOGIN TEST SAVED A SESSION.
        if (!File.Exists(Paths.StorageStatePath))
            throw new Exception(
                "Login test passed but no session was saved. Ensure the login test extends TestBase.");

        return Paths.StorageStatePath;
    }

    private static string FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "(no error message)";

        var index = text.IndexOfAny(new[] { '\r', '\n' });
        return index < 0 ? text : text[..index];
    }
}
