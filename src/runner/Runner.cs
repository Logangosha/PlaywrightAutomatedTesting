// A COMPONENTS RECORD HOLDS ALL THE DEPENDENCIES THAT THE RUNNER NEEDS TO FUNCTION. 
public record Components(
    Func<string, IConfig> LoadConfig,
    IAuth Auth,
    IActions Actions,
    IResults Results);

public class Runner : IRunner
{
    private readonly Components _components;
    private readonly Action<string> _onStatus;

    public Runner(Func<string, IConfig> loadConfig, IAuth auth, IActions actions, IResults results, Action<string>? onStatus = null)
    {
        _components = new Components(loadConfig, auth, actions, results);
        _onStatus = onStatus ?? (_ => { });
    }

    // STATE MACHINE: STARTS AT READINGCONFIG, EACH STATE RETURNS THE NEXT STATE (OR NULL TO END).
    // LOOP EXITS WHEN A STATE RETURNS NULL (SUCCESS) OR AN EXCEPTION BREAKS THE CHAIN (FAULTED).
    public async Task<RunResult> RunAsync(string configPath)
    {
        // THE RUN CONTEXT IS THE SINGLE SOURCE OF TRUTH FOR "WHERE ARE WE" AT ANY INSTANT, INCLUDING ON A CRASH.
        var ctx = new RunContext(configPath, _onStatus);

        // FIRST STATE: LOAD AND VALIDATE THE CONFIG FILE. RETURNS NEXT STATE OR NULL IF PARSING FAILS.
        IState? state = new ReadingConfigState(_components);

        try
        {
            while (state is not null)
            {
                ctx.Current = state.Name;
                state = await state.RunAsync(ctx);
            }

            ctx.Current = RunState.Completed;
        }
        catch (Exception ex)
        {
            var faultedDuring = ctx.Current;
            ctx.Current = RunState.Faulted;
            ctx.Result = RunResult.From(ctx, RunState.Faulted, faultedDuring, ex.Message);
            _components.Results.Report(ctx.Result);
        }

        return ctx.Result!;
    }
}
