using System.Xml.Linq;

namespace RunnerUI.Services;

/// <summary>
/// Reads past runs off disk (the runner's results/ and traces/ folders) so the UI can
/// browse them without the user digging through the file system, and delete old ones.
/// A run is identified by its timestamp folder (e.g. 2026-07-08_07-59-31).
/// </summary>
public class RunHistory
{
    private static readonly XNamespace Trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    /// <summary>Past runs, newest first.</summary>
    public IReadOnlyList<RunRecord> List()
    {
        var root = Paths.ResultsRoot;
        if (!Directory.Exists(root))
            return Array.Empty<RunRecord>();

        var runs = new List<RunRecord>();
        foreach (var dir in Directory.GetDirectories(root))
        {
            var ts = Path.GetFileName(dir);
            var log = Path.Combine(dir, "log.txt");
            var trx = Path.Combine(dir, "tests.trx");
            var tracesDir = Path.Combine(Paths.TracesRoot, ts);

            var (total, passed, failed) = ParseTrxCounts(trx);

            runs.Add(new RunRecord(
                Timestamp: ts,
                When: ParseWhen(ts),
                LogPath: File.Exists(log) ? log : null,
                TracesDir: Directory.Exists(tracesDir) ? tracesDir : null,
                Total: total,
                Passed: passed,
                Failed: failed));
        }

        // Timestamp format (yyyy-MM-dd_HH-mm-ss) sorts chronologically as text.
        return runs.OrderByDescending(r => r.Timestamp, StringComparer.Ordinal).ToList();
    }

    /// <summary>Deletes a run's results and traces folders.</summary>
    public void Delete(string timestamp)
    {
        SafeDeleteDir(Path.Combine(Paths.ResultsRoot, timestamp));
        SafeDeleteDir(Path.Combine(Paths.TracesRoot, timestamp));
    }

    /// <summary>Deletes every run.</summary>
    public void Clear()
    {
        foreach (var r in List())
            Delete(r.Timestamp);
    }

    private static (int Total, int Passed, int Failed) ParseTrxCounts(string trxPath)
    {
        if (!File.Exists(trxPath))
            return (0, 0, 0);

        try
        {
            var results = XDocument.Load(trxPath).Descendants(Trx + "UnitTestResult").ToList();
            return (
                results.Count,
                results.Count(r => (string?)r.Attribute("outcome") == "Passed"),
                results.Count(r => (string?)r.Attribute("outcome") == "Failed"));
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    private static DateTime? ParseWhen(string timestamp) =>
        DateTime.TryParseExact(timestamp, "yyyy-MM-dd_HH-mm-ss", null,
            System.Globalization.DateTimeStyles.None, out var dt) ? dt : null;

    private static void SafeDeleteDir(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch { /* best-effort */ }
    }
}

/// <summary>One past run: its timestamp, artifact paths, and result counts.</summary>
public record RunRecord(
    string Timestamp,
    DateTime? When,
    string? LogPath,
    string? TracesDir,
    int Total,
    int Passed,
    int Failed);
