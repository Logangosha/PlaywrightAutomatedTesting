using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

/// <summary>
/// Shared primitive for running xUnit tests as a subprocess.
///
/// Both components that need to run tests use this:
///   - Actions — runs the selected test actions the user asked for.
///   - Auth (auto mode) — runs the designated login test.
///
/// It spawns "dotnet test --no-build" with a trait filter and a set of environment
/// variables, streams per-test progress, and parses the TRX results file. Keeping
/// this in one place means the spawn/parse logic isn't duplicated across components.
/// </summary>
public static class TestProcess
{
    // XML namespace for xUnit's TRX (Test Results XML) output.
    private static readonly XNamespace Trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    /// <summary>
    /// Runs `dotnet test` with the given filter and environment, then returns parsed results.
    /// </summary>
    /// <param name="filter">Trait selector (e.g. "Category=Smoke"), or null/"all" to run everything.</param>
    /// <param name="resultsDir">Where the TRX and log files are written (unique per caller so they don't collide).</param>
    /// <param name="env">Environment variables the test process needs (BASE_URL, credentials, etc.).</param>
    /// <param name="onProgress">Optional callback per test result line (e.g. "Passed SomeTest [1.5s]").</param>
    public static async Task<(List<TestResult> Tests, string LogPath)> RunAsync(string? filter, string resultsDir, IReadOnlyDictionary<string, string> env, Action<string>? onProgress = null)
    {
        Directory.CreateDirectory(resultsDir);

        var trxPath = Path.Combine(resultsDir, "tests.trx");
        var logPath = Path.Combine(resultsDir, "log.txt");

        // Build the "dotnet test" command line.
        // --no-build: the binaries were already built; rebuilding would race the running exe.
        // --logger trx: machine-readable results (what we parse below).
        // --logger console: human-readable output for the log + live progress.
        var arguments =
            $"test --no-build --results-directory \"{resultsDir}\" " +
            $"--logger \"trx;LogFileName=tests.trx\" --logger \"console;verbosity=normal\"";

        // Add the trait filter unless the caller wants every test.
        if (!string.IsNullOrWhiteSpace(filter) && !filter.Equals("all", StringComparison.OrdinalIgnoreCase))
            arguments += $" --filter \"{filter}\"";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = Paths.ProjectRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        // The test process is separate from this one, so config reaches the tests via
        // environment variables (read by TestSettings/TestBase — never from files).
        foreach (var kv in env)
            process.StartInfo.Environment[kv.Key] = kv.Value;

        // Capture output for both the log file and live progress streaming.
        var log = new List<string>();

        void HandleLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            // Output/ErrorDataReceived fire on thread-pool threads — lock before appending.
            lock (log)
                log.Add(line);

            // xUnit's console logger prints per-test lines like "  Passed SomeTest [1.5s]".
            // Forward those to the caller so the UI can show progress in real time.
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("Passed ") || trimmed.StartsWith("Failed ") || trimmed.StartsWith("Skipped "))
                onProgress?.Invoke(trimmed);
        }

        process.OutputDataReceived += (_, e) => HandleLine(e.Data);
        process.ErrorDataReceived += (_, e) => HandleLine(e.Data is null ? null : "[stderr] " + e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        // Persist the full output so a mysterious failure can be diagnosed later.
        File.WriteAllLines(logPath, log);

        // No TRX file means one of two things:
        if (!File.Exists(trxPath))
        {
            // (1) The filter matched zero tests — a user selector mistake, not a crash.
            //     Return an empty result set and let the caller decide what that means.
            if (log.Any(l => l.Contains("No test matches", StringComparison.OrdinalIgnoreCase)))
                return (new List<TestResult>(), logPath);

            // (2) The process died before writing results — the runner genuinely faulted.
            throw new Exception($"Test run produced no results file. See log: {logPath}");
        }

        return (ParseTrx(trxPath), logPath);
    }

    /// <summary>
    /// Parses the TRX file into TestResults. The relevant structure is:
    ///   &lt;UnitTestResult testName="..." outcome="Passed|Failed|NotExecuted" duration="..."&gt;
    ///     &lt;Output&gt;&lt;ErrorInfo&gt;&lt;Message&gt;...&lt;/Message&gt;
    /// </summary>
    private static List<TestResult> ParseTrx(string trxPath)
    {
        var doc = XDocument.Load(trxPath);

        return doc.Descendants(Trx + "UnitTestResult")
            .Select(r => new TestResult
            {
                Name = (string?)r.Attribute("testName") ?? "Unknown",
                Outcome = (string?)r.Attribute("outcome") ?? "Unknown",
                Duration = TimeSpan.TryParse((string?)r.Attribute("duration"), out var d) ? d : TimeSpan.Zero,
                Error = r.Descendants(Trx + "Message").FirstOrDefault()?.Value
            })
            .OrderBy(t => t.Name)
            .ToList();
    }
}
