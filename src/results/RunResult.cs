public class TestResult
{
    public string Name { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public string? Error { get; init; }
    public bool Passed => Outcome == "Passed";
    public bool Failed => Outcome == "Failed";
}
public class RunResult
{
    public RunState FinalState { get; init; }
    public RunState? FaultedDuring { get; init; }
    public string? Error { get; init; }
    public bool Success => FinalState == RunState.Completed && FailedCount == 0;
    public string ConfigPath { get; init; } = string.Empty;
    public string Site { get; init; } = string.Empty;
    public string Env { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string AuthMode { get; init; } = string.Empty;
    public string Actions { get; init; } = string.Empty;
    public bool Headless { get; init; }
    public List<TestResult> Tests { get; init; } = new();
    public int PassedCount => Tests.Count(t => t.Passed);
    public int FailedCount => Tests.Count(t => t.Failed);
    public int SkippedCount => Tests.Count(t => !t.Passed && !t.Failed);
    public TimeSpan Duration { get; init; }
    public string? TracesDir { get; init; }
    public string? LogPath { get; init; }

    public static RunResult From(RunContext ctx, RunState finalState, RunState? faultedDuring = null, string? error = null)
    {
        return new RunResult
        {
            FinalState = finalState,
            FaultedDuring = faultedDuring,
            Error = error,
            ConfigPath = ctx.ConfigPath,
            Site = ctx.Config?.Site ?? string.Empty,
            Env = ctx.Config?.Env ?? string.Empty,
            Url = ctx.Config?.Url ?? string.Empty,
            AuthMode = ctx.Config?.Auth.Mode ?? string.Empty,
            Actions = ctx.Config?.Actions ?? string.Empty,
            Headless = ctx.Config?.Headless ?? true,
            Tests = ctx.Tests,
            Duration = DateTime.Now - ctx.Started,
            TracesDir = ctx.Tests.Count > 0 ? Path.Combine(Paths.TracesRoot, ctx.Timestamp) : null,
            LogPath = ctx.LogPath
        };
    }
}
