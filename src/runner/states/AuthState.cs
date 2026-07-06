public class AuthState : IState
{
    private readonly Components _components;

    public AuthState(Components components) => _components = components;

    public RunState Name => RunState.Authenticating;

    public async Task<IState?> RunAsync(RunContext ctx)
    {
        // GETS THE AUTHENTICATION MODE FROM THE CONFIGURATION
        var mode = ctx.Config!.Auth.Mode;
        ctx.OnStatus($"[{Name}] Mode: {mode}");

        // AUTHENTICATES WITH THE CONFIGURED AUTHENTICATION MODE
        // IF AUTHENTICATION IS SUCCESSFUL, THE SESSION STATE IS SAVED TO A FILE AND THE PATH IS RETURNED.
        ctx.StorageStatePath = await _components.Auth.AuthenticateAsync(
            ctx.Config,
            ctx.Timestamp,
            line => ctx.OnStatus($"[{Name}] {line}"));

        ctx.OnStatus(ctx.StorageStatePath is null
            ? $"[{Name}] No auth — continuing."
            : $"[{Name}] Session saved.");

        return new RunActionsState(_components);
    }
}
