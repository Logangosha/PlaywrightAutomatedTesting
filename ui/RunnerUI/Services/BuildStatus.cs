namespace RunnerUI.Services;

/// <summary>
/// Reads the marker rebuild.ps1 leaves behind when the automatic build-on-open fails
/// (logs/build-error.log). That build can run with no visible window (see
/// launch-hidden.vbs), so this is how the failure surfaces — as a banner in the app
/// you're now looking at (which is the previous good build), instead of on a console
/// nobody saw. rebuild.ps1 clears the file at the start of its next attempt.
/// </summary>
public class BuildStatus
{
    public string? LogPath { get; private set; }
    public string? Preview { get; private set; }

    public bool HasError()
    {
        var path = Path.Combine(Paths.ProjectRoot, "logs", "build-error.log");
        if (!File.Exists(path)) return false;

        LogPath = path;
        try
        {
            var text = File.ReadAllText(path);
            Preview = text.Length > 600 ? text[..600] + "…" : text;
        }
        catch
        {
            Preview = null;
        }
        return true;
    }
}
