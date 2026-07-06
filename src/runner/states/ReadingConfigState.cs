public class ReadingConfigState : IState
{
    private readonly Components _components;

    public ReadingConfigState(Components components) => _components = components;

    public RunState Name => RunState.ReadingConfig;

    public Task<IState?> RunAsync(RunContext ctx)
    {
        ctx.OnStatus($"[{Name}] Loading {ctx.ConfigPath}...");

        ctx.Config = _components.LoadConfig(ctx.ConfigPath);

        // REPORTS THE CONFIG THAT WAS LOADED, INCLUDING SITE, ENV, URL, AUTH MODE, ACTIONS, AND HEADLESS MODE.
        ctx.OnStatus($"[{Name}] site={ctx.Config.Site} env={ctx.Config.Env} url={ctx.Config.Url} " +
                     $"auth={ctx.Config.Auth.Mode} actions={ctx.Config.Actions} headless={ctx.Config.Headless}");

        return Task.FromResult<IState?>(new VerifyingEnvState(_components));
    }
}
