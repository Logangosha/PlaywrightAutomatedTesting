using System.Text.Json;

namespace RunnerUI.Services;

/// <summary>
/// Drives the existing runner backend from the UI — the "remote control" seam.
/// It never reimplements run logic: it builds a config from the form, writes it to
/// a temp file (the runner loads and validates configs from a path), runs it through
/// the real <see cref="Runner"/>, and returns the <see cref="RunResult"/>.
/// </summary>
public class RunnerHost
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <param name="input">The form values for this run.</param>
    /// <param name="onStatus">Optional per-phase status line (used by the live view in M3).</param>
    public async Task<RunResult> RunAsync(RunInput input, Action<string>? onStatus = null)
    {
        // Build the config the runner understands. M2 is auth = none.
        var config = new Config
        {
            Site = input.Site.Trim(),
            Env = input.Env.Trim(),
            Url = input.Url.Trim(),
            Actions = string.IsNullOrWhiteSpace(input.Actions) ? "all" : input.Actions.Trim(),
            Headless = input.Headless,
            Auth = new AuthConfig { Mode = "none" }
        };

        // The runner loads configs from a path (and validates on load), so write the
        // in-memory config to a temp file. It isn't meant to be kept — delete after.
        var tempPath = Path.Combine(Path.GetTempPath(), $"runnerui-config-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(config, JsonOptions));

        try
        {
            var runner = new Runner(
                loadConfig: Config.Load,
                auth: new Auth(),                    // no waitForUser — M2 is auth = none
                actions: new Actions(),
                results: new NullResults(),          // we use the returned RunResult, not a presenter
                onStatus: onStatus ?? (_ => { }));

            return await runner.RunAsync(tempPath);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
        }
    }
}

/// <summary>Mutable form model for the Configure step (bound with @bind in the UI).</summary>
public class RunInput
{
    public string Site { get; set; } = "";
    public string Env { get; set; } = "";
    public string Url { get; set; } = "";
    public string Actions { get; set; } = "all";
    public bool Headless { get; set; } = true;
}
