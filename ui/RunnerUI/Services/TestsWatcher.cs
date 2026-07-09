namespace RunnerUI.Services;

/// <summary>
/// Watches the src/tests folder for .cs changes while the app runs. When a change
/// settles (debounced), it raises <see cref="Changed"/> so the UI can prompt for a
/// rebuild + restart (the Authoring → Running transition). Only active in a source
/// tree — if src/tests isn't present (a packaged run), it does nothing.
/// </summary>
public class TestsWatcher : IDisposable
{
    private FileSystemWatcher? _watcher;
    private System.Timers.Timer? _debounce;
    private bool _started;

    /// <summary>Raised (on a background thread) after test-source changes settle.</summary>
    public event Action? Changed;

    public void Start()
    {
        if (_started) return;
        _started = true;

        var dir = Path.Combine(Paths.ProjectRoot, "src", "tests");
        if (!Directory.Exists(dir)) return;   // no source tree → nothing to author

        _debounce = new System.Timers.Timer(600) { AutoReset = false };
        _debounce.Elapsed += (_, _) => Changed?.Invoke();

        _watcher = new FileSystemWatcher(dir, "*.cs")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            EnableRaisingEvents = true
        };
        _watcher.Changed += Bump;
        _watcher.Created += Bump;
        _watcher.Deleted += Bump;
        _watcher.Renamed += Bump;
    }

    // Editors save in bursts; restart the debounce so we raise Changed once per lull.
    private void Bump(object sender, FileSystemEventArgs e)
    {
        _debounce?.Stop();
        _debounce?.Start();
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce?.Dispose();
    }
}
