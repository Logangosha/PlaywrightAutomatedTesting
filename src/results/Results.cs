public class Results : IResults
{
    public void Report(RunResult result)
    {
        var verdict = result.FinalState == RunState.Faulted
            ? "FAULTED"
            : result.FailedCount > 0 ? "FAILED" : "PASSED";

        Console.WriteLine();
        Console.WriteLine("===================================");
        Console.WriteLine($" RUN RESULT : {verdict}");
        Console.WriteLine("===================================");
        Console.WriteLine($" Config   : {result.ConfigPath}");

        if (!string.IsNullOrEmpty(result.Url))
        {
            Console.WriteLine($" Site     : {result.Site}");
            Console.WriteLine($" Env      : {result.Env}");
            Console.WriteLine($" Url      : {result.Url}");
            Console.WriteLine($" Auth     : {result.AuthMode}");
            Console.WriteLine($" Actions  : {result.Actions}");
            Console.WriteLine($" Headless : {result.Headless}");
        }

        if (result.FinalState == RunState.Faulted)
        {
            Console.WriteLine("-----------------------------------");
            Console.WriteLine($" Faulted during : {result.FaultedDuring}");
            Console.WriteLine($" Error          : {result.Error}");
            Console.WriteLine("===================================");
            return;
        }

        if (result.Tests.Count > 0)
        {
            Console.WriteLine("-----------------------------------");

            foreach (var test in result.Tests)
            {
                var mark = test.Passed ? "PASS" : test.Failed ? "FAIL" : "SKIP";
                Console.WriteLine($"  {mark}  {test.Name}  ({test.Duration.TotalSeconds:0.0}s)");

                if (test.Failed && !string.IsNullOrWhiteSpace(test.Error))
                    Console.WriteLine($"        -> {FirstLine(test.Error)}");
            }
        }

        Console.WriteLine("-----------------------------------");
        Console.WriteLine(
            $" Total {result.Tests.Count} | Passed {result.PassedCount} | " +
            $"Failed {result.FailedCount} | Skipped {result.SkippedCount} | {result.Duration.TotalSeconds:0.0}s");

        if (result.TracesDir is not null)
            Console.WriteLine($" Traces : {result.TracesDir}");

        if (result.LogPath is not null)
            Console.WriteLine($" Log    : {result.LogPath}");

        Console.WriteLine("===================================");
    }

    private static string FirstLine(string text)
    {
        var index = text.IndexOfAny(new[] { '\r', '\n' });
        return index < 0 ? text : text[..index];
    }
}
