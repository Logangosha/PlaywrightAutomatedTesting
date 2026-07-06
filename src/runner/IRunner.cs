/// <summary>
/// Orchestrates a run: takes the config the user provides, walks the state
/// machine, and always ends with a RunResult — whatever frontend started it.
/// </summary>
public interface IRunner
{
    Task<RunResult> RunAsync(string configPath);
}
