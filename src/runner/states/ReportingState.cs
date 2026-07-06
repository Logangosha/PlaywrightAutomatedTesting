public class ReportingState : IState
{
    private readonly Components _components;

    public ReportingState(Components components) => _components = components;

    public RunState Name => RunState.Reporting;

    public Task<IState?> RunAsync(RunContext ctx)
    {
        ctx.Result = RunResult.From(ctx, RunState.Completed);

        _components.Results.Report(ctx.Result);

        return Task.FromResult<IState?>(null);
    }
}
