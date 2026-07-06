/// <summary>
/// Runs the selected test actions via xUnit. Acts like a playlist: it knows *what*
/// to run but delegates the actual iteration to xUnit. Called from RunActionsState
/// in the runner's state machine.
///
/// Selection follows the trait convention: the runner-composed base selector
/// (Site + Env + Kind=Action) scopes the run to this config's target, and the
/// config's actions slice narrows within it. Because actions always require
/// Kind=Action, login tests (Kind=Auth) can never run as actions.
///
/// The spawn/parse mechanics live in the shared <see cref="TestProcess"/>.
/// </summary>
public class Actions : IActions
{
    /// <summary>
    /// Runs the selected actions and returns parsed results.
    /// </summary>
    /// <param name="config">The user's config (site, env, url, actions slice, headless).</param>
    /// <param name="storageStatePath">Session file from Auth, or null when auth was none.</param>
    /// <param name="timestamp">Groups this run's traces/results (e.g. "2026-07-03_21-08-44").</param>
    /// <param name="onProgress">Optional per-test progress callback.</param>
    public async Task<ActionsOutput> RunAsync(IConfig config, string? storageStatePath, string timestamp, Action<string>? onProgress = null)
    {
        // Environment the test process needs. TestBase/TestSettings read these.
        var env = new Dictionary<string, string>
        {
            ["BASE_URL"] = config.Url,
            ["HEADLESS"] = config.Headless ? "true" : "false",
            ["RUN_TIMESTAMP"] = timestamp
        };

        // Reuse the session Auth saved (manual/auto). None mode leaves this unset,
        // so TestBase starts each test from a fresh, logged-out context.
        if (storageStatePath is not null)
            env["STORAGE_STATE"] = storageStatePath;

        // Actions results go under the timestamp root; Auth's login test uses a separate
        // "auth" subfolder, so their TRX files never overwrite each other.
        var resultsDir = Path.Combine(Paths.ResultsRoot, timestamp);

        var (tests, logPath) = await TestProcess.RunAsync(
            BuildFilter(config), resultsDir, env, onProgress);

        return new ActionsOutput(tests, logPath);
    }

    /// <summary>
    /// Composes the full filter from the trait convention:
    ///   Site={site} &amp; Env={env} &amp; Kind=Action [&amp; (the config's actions slice)]
    /// The user never writes Site=/Env=/Kind= by hand — only the slice
    /// (e.g. "Category=Smoke&amp;Module=HomePage", or "all" for no narrowing).
    ///
    /// The slice is wrapped in parentheses so any OR (|) inside it stays scoped to
    /// the base selector. Without the parens, "Module=A|Module=B" would bind as
    /// "…&amp;Module=A" OR "Module=B", letting Module=B tests from other sites/envs
    /// leak in. With them it reads "…&amp;(Module=A OR Module=B)" as intended.
    /// </summary>
    private static string BuildFilter(IConfig config)
    {
        var filter = $"Site={config.Site}&Env={config.Env}&Kind=Action";

        var slice = config.Actions;
        if (!string.IsNullOrWhiteSpace(slice) && !slice.Equals("all", StringComparison.OrdinalIgnoreCase))
            filter += $"&({slice})";

        return filter;
    }
}
