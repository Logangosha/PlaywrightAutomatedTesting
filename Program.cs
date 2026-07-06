if (!Console.IsOutputRedirected)
{
    try { Console.Clear(); } catch { /* no console buffer available */ }
}

// EXITS WITH USAGE ERROR IF NO CONFIG FILE PATH PROVIDED.
if (args.Length < 1)
{
    Console.WriteLine("Usage: dotnet run -- <path-to-config.json>");
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
