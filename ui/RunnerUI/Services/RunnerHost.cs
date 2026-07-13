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
    /// <param name="onStatus">Optional per-phase status line (used by the live view).</param>
    /// <param name="waitForUser">
    /// Manual-auth callback: invoked with a prompt after the headed login browser opens,
    /// and must complete once the user confirms they've logged in. Required for
    /// auth.mode = manual; ignored otherwise.
    /// </param>
    public async Task<RunResult> RunAsync(
        RunInput input,
        Action<string>? onStatus = null,
        Func<string, Task>? waitForUser = null)
    {
        // Build the config the runner understands, including the chosen auth.
        var config = new Config
        {
            Site = input.Site.Trim(),
            Env = input.Env.Trim(),
            Url = input.Url.Trim(),
            Actions = string.IsNullOrWhiteSpace(input.Actions) ? "all" : input.Actions.Trim(),
            Headless = input.Headless,
            Auth = new AuthConfig
            {
                Mode = input.AuthMode,
                Username = input.Username,
                Password = input.Password
            }
        };

        // The runner loads configs from a path (and validates on load), so write the
        // in-memory config to a temp file. It isn't meant to be kept — delete after.
        var tempPath = Path.Combine(Path.GetTempPath(), $"runnerui-config-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(config, JsonOptions));

        try
        {
            var runner = new Runner(
                loadConfig: Config.Load,
                auth: new Auth(waitForUser),         // used only in manual mode
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
    public string AuthMode { get; set; } = "none";   // none | manual | auto
    public string? Username { get; set; }
    public string? Password { get; set; }
}
