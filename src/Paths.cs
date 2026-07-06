/// <summary>
/// Well-known locations, all relative to the project root.
/// Both the runner and the spawned test process run from bin/Debug/net8.0,
/// so the project root is three levels up from the executing assembly.
/// </summary>
public static class Paths
{
    public static string ProjectRoot { get; } =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));

    /// <summary>Playwright session file (Playwright's conventional name is kept).</summary>
    public static string StorageStatePath =>
        Path.Combine(ProjectRoot, "src", "auth", "storageState.json");

    public static string TracesRoot => Path.Combine(ProjectRoot, "traces");

    public static string ResultsRoot => Path.Combine(ProjectRoot, "results");
}
