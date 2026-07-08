namespace RunnerUI.Services;

/// <summary>
/// A no-op <see cref="IResults"/> for the UI. The console frontend prints the
/// RunResult here; the UI instead reads the RunResult that <c>Runner.RunAsync</c>
/// returns and renders it itself, so this presenter does nothing.
/// </summary>
public class NullResults : IResults
{
    public void Report(RunResult result) { }
}
