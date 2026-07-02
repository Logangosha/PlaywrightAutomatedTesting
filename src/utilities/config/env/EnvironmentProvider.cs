using System.Text.Json;

public static class EnvironmentProvider
{
    private static EnvironmentConfig? _current;

    public static EnvironmentConfig Current =>
        _current ??= Load();

    private static EnvironmentConfig Load()
    {
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

        var contextJson = File.ReadAllText(contextPath);
        var context = JsonSerializer.Deserialize<Dictionary<string, string>>(contextJson);

        var env = context?["env"] ?? "dev";

        var envPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "src",
            "utilities",
            "config",
            "env",
            "environments.json");

        var envJson = File.ReadAllText(envPath);

        var configs = JsonSerializer.Deserialize<Dictionary<string, EnvironmentConfig>>(envJson)
                      ?? throw new Exception("Invalid environments.json");

        return configs[env];
    }
}