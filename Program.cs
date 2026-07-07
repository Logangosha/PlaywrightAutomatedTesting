if (!Console.IsOutputRedirected)
{
    try { Console.Clear(); } catch { /* no console buffer available */ }
}

// DISCOVERY MODE: list the available tests (the "menu") without running anything.
// This is the same IDiscovery a UI will call; here we just print it to the console.
if (args.Length >= 1 && (args[0] == "discover" || args[0] == "--list"))
{
    PrintDiscovered(new Discovery().Discover());
    return 0;
}

// EXITS WITH USAGE ERROR IF NO CONFIG FILE PATH PROVIDED.
if (args.Length < 1)
{
    Console.WriteLine("Usage: dotnet run -- <path-to-config.json>");
    Console.WriteLine("       dotnet run -- discover        (list available tests)");
    Console.WriteLine("Example: dotnet run -- configs/example.json");
    return 2;
}

// CREATES RUNNER WITH THE PROVIDED CONFIG, AUTH, ACTIONS, RESULTS, AND STATUS CALLBACK.
var runner = new Runner(
    loadConfig: Config.Load,
    auth: new Auth(waitForUser: message =>
    {
        Console.WriteLine();
        Console.WriteLine(message);
        Console.WriteLine("Press ENTER here once you are logged in...");
        Console.ReadLine();
        return Task.CompletedTask;
    }),
    actions: new Actions(),
    results: new Results(),
    onStatus: line => Console.WriteLine(line));

// EXECUTES THE RUNNER WITH THE PROVIDED CONFIG FILE PATH.
var result = await runner.RunAsync(args[0]);

// RETURNS EXIT CODE: 
// 0 IF ALL TESTS PASSED
// 1 IF TESTS FAILED 
// 2 IF THE RUNNER ITSELF FAULTED.
return result.FinalState == RunState.Faulted ? 2
     : result.FailedCount > 0 ? 1
     : 0;

// PRINTS THE DISCOVERED TESTS AS A Site -> Env -> Module TREE. (Only for human viewing, not machine-readable.)
static void PrintDiscovered(IReadOnlyList<DiscoveredTest> tests)
{
    Console.WriteLine($"Available tests ({tests.Count}):");
    Console.WriteLine();
    var rows = tests.SelectMany(t => t.Envs.DefaultIfEmpty("(no env)").Select(env => (test: t, env)));

    foreach (var site in rows.GroupBy(r => r.test.Site).OrderBy(g => g.Key))
    {
        Console.WriteLine(site.Key);
        foreach (var env in site.GroupBy(r => r.env).OrderBy(g => g.Key))
        {
            Console.WriteLine($"  {env.Key}");

            foreach (var r in env.Where(r => r.test.Kind == "Auth").OrderBy(r => r.test.Method))
                Console.WriteLine($"    [Auth]  {r.test.Method}");

            foreach (var module in env.Where(r => r.test.Kind != "Auth")
                                      .GroupBy(r => r.test.Module ?? "(no module)")
                                      .OrderBy(g => g.Key))
            {
                Console.WriteLine($"    {module.Key}");
                foreach (var r in module.OrderBy(r => r.test.Method))
                {
                    var cat = r.test.Category is null ? "" : $"  ({r.test.Category})";
                    Console.WriteLine($"      {r.test.Method}{cat}");
                }
            }
        }
    }
}
