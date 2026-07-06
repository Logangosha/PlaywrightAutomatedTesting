public class RunActionsState : IState
{
    private readonly Components _components;

    public RunActionsState(Components components) => _components = components;

    public RunState Name => RunState.RunningActions;

    public async Task<IState?> RunAsync(RunContext ctx)
    {
        ctx.OnStatus($"[{Name}] Running actions: {ctx.Config!.Actions}");

        // RUNS THE CONFIGURED ACTIONS USING THE COMPONENTS SERVICE
        var output = await _components.Actions.RunAsync(
            ctx.Config,
            ctx.StorageStatePath,
            ctx.Timestamp,
            line => ctx.OnStatus($"[{Name}] {line}"));

        // SAVES THE TEST RESULTS AND LOG PATH TO THE CONTEXT FOR REPORTING
        ctx.Tests = output.Tests;
        ctx.LogPath = output.LogPath;

        if (ctx.Tests.Count == 0)
            ctx.OnStatus($"[{Name}] No tests matched '{ctx.Config.Actions}'.");

        return new ReportingState(_components);
    }
}
