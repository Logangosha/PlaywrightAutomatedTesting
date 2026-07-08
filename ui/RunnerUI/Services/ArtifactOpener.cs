using System.ComponentModel;
using System.Diagnostics;

namespace RunnerUI.Services;

/// <summary>
/// Opens run artifacts using native OS access (the MAUI shell allows this): the log
/// file / traces folder in their default apps, and a per-test Playwright trace in the
/// trace viewer via the runner's bundled playwright.ps1.
/// </summary>
public class ArtifactOpener
{
    /// <summary>Opens a file in its default app, or a folder in the file explorer.</summary>
    public void OpenPath(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    /// <summary>
    /// Launches the Playwright trace viewer for a trace .zip via the runner's bundled
    /// playwright.ps1. Prefers pwsh (PowerShell 7) but falls back to Windows PowerShell,
    /// which is always present — so it works whether or not pwsh is installed.
    /// </summary>
    public void OpenTrace(string zipPath)
    {
        var script = Path.Combine(Paths.ProjectRoot, "bin", "Debug", "net8.0", "playwright.ps1");
        if (!File.Exists(script))
            throw new FileNotFoundException($"playwright.ps1 not found at {script}. Build the runner first.");

        var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" show-trace \"{zipPath}\"";

        if (!TryStart("pwsh", args) && !TryStart("powershell", args))
            throw new Exception("Could not launch PowerShell (pwsh or powershell) to open the trace viewer.");
    }

    // Runs an executable, returning false (instead of throwing) if it isn't installed,
    // so the caller can try the next candidate.
    private static bool TryStart(string exe, string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false });
            return true;
        }
        catch (Win32Exception)
        {
            return false;   // executable not found on PATH
        }
    }

    /// <summary>
    /// Lists every trace .zip under a run's traces folder, as (relative path, full path),
    /// so the UI can offer a picker scoped to that folder.
    /// </summary>
    public IReadOnlyList<(string Relative, string FullPath)> ListTraces(string? tracesDir)
    {
        if (string.IsNullOrEmpty(tracesDir) || !Directory.Exists(tracesDir))
            return Array.Empty<(string, string)>();

        return Directory.EnumerateFiles(tracesDir, "*.zip", SearchOption.AllDirectories)
                        .Select(f => (Path.GetRelativePath(tracesDir, f), f))
                        .OrderBy(x => x.Item1, StringComparer.OrdinalIgnoreCase)
                        .ToList();
    }

    /// <summary>
    /// Finds a test's trace zip under the run's traces folder. Traces are saved as
    /// &lt;tracesDir&gt;/&lt;Module&gt;/&lt;method&gt;.zip; the TRX test name is
    /// Namespace.Class.Method, so match on the last segment.
    /// </summary>
    public string? FindTraceZip(string? tracesDir, string testName)
    {
        if (string.IsNullOrEmpty(tracesDir) || !Directory.Exists(tracesDir))
            return null;

        var method = testName.Contains('.') ? testName[(testName.LastIndexOf('.') + 1)..] : testName;

        // A [Theory] test name may carry a "(args…)" suffix; the trace file is named
        // after the bare method, so drop it.
        var paren = method.IndexOf('(');
        if (paren >= 0) method = method[..paren];

        return Directory.EnumerateFiles(tracesDir, method.Trim() + ".zip", SearchOption.AllDirectories)
                        .FirstOrDefault();
    }
}
