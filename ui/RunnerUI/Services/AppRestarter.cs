using System.Diagnostics;

namespace RunnerUI.Services;

/// <summary>
/// Method A of the author loop: hands off to rebuild.ps1 (passing this process's id so
/// it waits for us to close and the test DLL unlocks), then exits the app. The helper
/// rebuilds and relaunches. The app can't rebuild itself because it has the test
/// assembly loaded/locked — hence the external hand-off.
/// </summary>
public class AppRestarter
{
    /// <summary>True only in a source tree, where a rebuild is possible.</summary>
    public bool CanRebuild => File.Exists(ScriptPath);

    private static string ScriptPath => Path.Combine(Paths.ProjectRoot, "scripts", "rebuild.ps1");

    public void RebuildAndRestart()
    {
        if (!CanRebuild) return;

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{ScriptPath}\" -WaitForPid {Environment.ProcessId}",
            UseShellExecute = true   // its own console window shows build progress
        });

        // Exit so the helper's Wait-Process returns and the DLL unlocks for the build.
        Environment.Exit(0);
    }
}
