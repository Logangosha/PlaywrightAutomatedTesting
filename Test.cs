using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// ======================================================
// STATE
// ======================================================
string env = "dev";
string auth = "manual";
string tests = "all";
string headless = "false";

string stage = "INIT";
string statusMessage = "Starting...";

var allLogs = new List<string>();
string? finalSummaryLine = null;

// Spinner state
string[] spinnerFrames = [".", "..", "..."];
int spinnerIndex = 0;
CancellationTokenSource? spinnerCts = null;

// ======================================================
// CLI PARSE
// ======================================================
for (int i = 0; i < args.Length; i++)
{
    switch (args[i].ToLower())
    {
        case "--env":
        case "-env":
            env = args[++i].ToLower();
            break;

        case "--auth":
        case "-auth":
            auth = args[++i].ToLower();
            break;

        case "--tests":
        case "-tests":
            tests = args[++i];
            break;

        case "--headless":
        case "-headless":
            headless = args.Length > i + 1 && !args[i + 1].StartsWith("--")
                ? args[++i]
                : "true";
            break;
    }
}

// ======================================================
// TRAIT VALIDATION
// ======================================================
bool IsValidFilter(string input)
{
    if (string.IsNullOrWhiteSpace(input) || input.Equals("all", StringComparison.OrdinalIgnoreCase))
        return true;

    var allowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Category",
        "Module"
    };

    var parts = input.Split('&', StringSplitOptions.RemoveEmptyEntries);

    foreach (var part in parts)
    {
        var kv = part.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);

        if (kv.Length != 2)
            return false;

        var key = kv[0].Trim();
        var value = kv[1].Trim();

        if (!allowedKeys.Contains(key))
            return false;

        if (string.IsNullOrWhiteSpace(value))
            return false;
    }

    return true;
}

if (!IsValidFilter(tests))
{
    Console.WriteLine("Invalid test filter. Use format: \"Category=Smoke&Module=Auth");
    Environment.Exit(1);
}

// ======================================================
// UI
// ======================================================
void Render(string spinner = "")
{
    Console.Clear();

    Console.WriteLine("===================================");
    Console.WriteLine(" Playwright Test Runner");
    Console.WriteLine("===================================");
    Console.WriteLine($" Environment : {env}");
    Console.WriteLine($" Auth Mode   : {auth}");
    Console.WriteLine($" Headless    : {headless}");
    Console.WriteLine($" Tests Filter: {tests}");
    Console.WriteLine("===================================");
    Console.WriteLine($" Stage       : {stage}");
    Console.WriteLine($" Status      : {statusMessage} {spinner}");
    Console.WriteLine("===================================");

    if (stage == "AUTH")
    {
        Console.WriteLine("\n(Press ENTER to continue after manual login)");
    }

    Console.WriteLine();
}

void StartSpinner()
{
    StopSpinner();

    spinnerCts = new CancellationTokenSource();

    _ = Task.Run(async () =>
    {
        while (!spinnerCts.Token.IsCancellationRequested)
        {
            Render(spinnerFrames[spinnerIndex]);

            spinnerIndex++;
            if (spinnerIndex >= spinnerFrames.Length)
                spinnerIndex = 0;

            try
            {
                await Task.Delay(200, spinnerCts.Token);
            }
            catch
            {
                break;
            }
        }
    });
}

void StopSpinner()
{
    if (spinnerCts != null)
    {
        spinnerCts.Cancel();
        spinnerCts.Dispose();
        spinnerCts = null;
    }

    Render();
}

// ======================================================
// STEP 1: CONTEXT
// ======================================================
stage = "CONTEXT";
statusMessage = "Writing runtime context...";
Render();

var contextPath = Path.Combine(
    AppContext.BaseDirectory,
    "..",
    "..",
    "..",
    "src",
    "utilities",
    "config",
    "runtime",
    "testRunContext.json");

Directory.CreateDirectory(Path.GetDirectoryName(contextPath)!);

File.WriteAllText(
    contextPath,
    JsonSerializer.Serialize(
        new { env, auth, tests, headless },
        new JsonSerializerOptions { WriteIndented = true }));

// ======================================================
// STEP 2: AUTH
// ======================================================
stage = "AUTH";
statusMessage = "Authenticating";
StartSpinner();

await AuthenticationProvider.AuthenticateAsync(auth);

StopSpinner();

// ======================================================
// STEP 3: TEST EXECUTION
// ======================================================
stage = "TESTS";
statusMessage = "Running tests";
StartSpinner();

var arguments = "test";

if (!string.IsNullOrWhiteSpace(tests) &&
    !tests.Equals("all", StringComparison.OrdinalIgnoreCase))
{
    arguments += $" --filter \"{tests}\"";
}

// ======================================================
// PROCESS
// ======================================================
var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = arguments,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
    }
};

void HandleLine(string line)
{
    if (string.IsNullOrWhiteSpace(line))
        return;

    allLogs.Add(line);

    if (line.StartsWith("Passed!") || line.StartsWith("Failed!"))
    {
        finalSummaryLine = line;
    }
}

process.OutputDataReceived += (_, e) => HandleLine(e.Data ?? "");

process.ErrorDataReceived += (_, e) =>
{
    if (!string.IsNullOrWhiteSpace(e.Data))
    {
        HandleLine("[ERROR] " + e.Data);
    }
};

process.Start();
process.BeginOutputReadLine();
process.BeginErrorReadLine();

await process.WaitForExitAsync();

StopSpinner();

// ======================================================
// WRITE LOG
// ======================================================
var logPath = Path.Combine(
    AppContext.BaseDirectory,
    "test-run-log.txt");

File.WriteAllText(
    logPath,
    string.Join(Environment.NewLine, allLogs));

// ======================================================
// FINAL SUMMARY
// ======================================================
Console.Clear();

Console.WriteLine("===================================");
Console.WriteLine(" TEST SUMMARY");
Console.WriteLine("===================================");
Console.WriteLine($"Environment : {env}");
Console.WriteLine($"Auth Mode   : {auth}");
Console.WriteLine($"Headless    : {headless}");
Console.WriteLine($"Test Filter : {tests}");
Console.WriteLine("===================================");

if (!string.IsNullOrWhiteSpace(finalSummaryLine))
{
    var match = Regex.Match(
        finalSummaryLine,
        @"^(Passed!|Failed!).*Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+),\s*Duration:\s*([^-]+)");

    if (match.Success)
    {
        Console.WriteLine($"Result   : {match.Groups[1].Value.Replace("!", "").ToUpper()}");
        Console.WriteLine($"Total    : {match.Groups[5].Value}");
        Console.WriteLine($"Passed   : {match.Groups[3].Value}");
        Console.WriteLine($"Failed   : {match.Groups[2].Value}");
        Console.WriteLine($"Skipped  : {match.Groups[4].Value}");
        Console.WriteLine($"Duration : {match.Groups[6].Value}");
    }
    else
    {
        Console.WriteLine(finalSummaryLine);
    }
}
else
{
    Console.WriteLine("Unable to locate xUnit summary.");
}

Console.WriteLine("===================================");
Console.WriteLine($"Full log saved to: {logPath}");
Console.WriteLine("===================================");

// Preserve the actual dotnet test exit code
Environment.Exit(process.ExitCode);