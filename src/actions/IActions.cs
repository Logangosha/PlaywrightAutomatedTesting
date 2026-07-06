/// <summary>What a test run produced: parsed per-test results plus the full output log.</summary>
public record ActionsOutput(List<TestResult> Tests, string LogPath);

/// <summary>
/// Runs the selected actions (xUnit tests). Like a playlist: it holds what to
/// run — xUnit is the speaker that actually plays it. Iteration, per-test
/// tracing, and result recording are delegated to xUnit + TestBase.
/// </summary>
public interface IActions
{
    /// <param name="config">The user's config (actions selector, env, headless).</param>
    /// <param name="storageStatePath">Session file from Auth, or null when auth was none.</param>
    /// <param name="timestamp">Run timestamp — groups traces and results per run.</param>
    /// <param name="onProgress">Optional live progress lines (per-test pass/fail as they happen).</param>
    Task<ActionsOutput> RunAsync(IConfig config, string? storageStatePath, string timestamp, Action<string>? onProgress = null);
}
