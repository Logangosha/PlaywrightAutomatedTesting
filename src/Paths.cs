/// <summary>
/// Well-known locations, all relative to the project root.
/// Both the runner and the spawned test process run from bin/Debug/net8.0,
/// so the project root is three levels up from the executing assembly.
/// </summary>
public static class Paths
{
    private static string? _projectRootOverride;

    /// <summary>
    /// Lets a frontend hosted outside the runner's own bin/Debug/net8.0 set the
    /// project root explicitly (e.g. the Blazor UI, whose base directory is its own
    /// bin — the three-levels-up default would resolve wrongly there). The console
    /// runner never calls this and keeps the default heuristic.
    /// </summary>
    public static void SetProjectRoot(string root) => _projectRootOverride = Path.GetFullPath(root);

    public static string ProjectRoot =>
        _projectRootOverride ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));

    /// <summary>Playwright session file (Playwright's conventional name is kept).</summary>
    public static string StorageStatePath =>
        Path.Combine(ProjectRoot, "src", "auth", "storageState.json");

    public static string TracesRoot => Path.Combine(ProjectRoot, "traces");

    public static string ResultsRoot => Path.Combine(ProjectRoot, "results");
}
