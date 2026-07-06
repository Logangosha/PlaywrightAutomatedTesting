public class VerifyingEnvState : IState
{
    private readonly Components _components;

    public VerifyingEnvState(Components components) => _components = components;

    public RunState Name => RunState.VerifyingEnv;

    public async Task<IState?> RunAsync(RunContext ctx)
    {
        var url = ctx.Config!.Url;
        ctx.OnStatus($"[{Name}] Checking {url}...");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        try
        {
            // SENDS A SIMPLE GET REQUEST TO THE CONFIGURED URL TO ENSURE THE ENVIRONMENT IS REACHABLE.
            using var response = await http.GetAsync(url);
            ctx.OnStatus($"[{Name}] Reachable ({(int)response.StatusCode}).");
        }
        catch (Exception ex)
        {
            throw new Exception($"Environment not reachable: {url} — {Unwrap(ex)}");
        }

        return new AuthState(_components);
    }

    private static string Unwrap(Exception ex) =>
        ex.InnerException is not null ? ex.InnerException.Message : ex.Message;
}
