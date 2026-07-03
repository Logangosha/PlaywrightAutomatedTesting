using System.Text.Json;

public static class TestRunContext
{
    private static Dictionary<string, string>? _current;

    public static Dictionary<string, string> Current =>
        _current ??= Load();

    private static Dictionary<string, string> Load()
    {
        var path = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..","..","..",
                "src","utilities","config","runtime",
                "testRunContext.json"));

        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
               ?? throw new Exception("Invalid testRunContext.json");
    }
}

public class ContextModel
{
    public string Env { get; set; } = "";
    public string Auth { get; set; } = "";
    public string Tests { get; set; } = "";
    public string Headless { get; set; } = "";
    public string TestRunTimestamp { get; set; } = "";
}