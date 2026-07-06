public static class TestSettings
{
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("BASE_URL")
        ?? throw new Exception(
            "BASE_URL is not set. Run tests through the runner (dotnet run -- <config.json>) " +
            "or set BASE_URL manually for a direct `dotnet test`.");

    public static bool Headless =>
        (Environment.GetEnvironmentVariable("HEADLESS") ?? "true")
            .Equals("true", StringComparison.OrdinalIgnoreCase);

    public static string? StorageStatePath
    {
        get
        {
            var path = Environment.GetEnvironmentVariable("STORAGE_STATE");
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
        }
    }

    public static string? SaveStorageStatePath
    {
        get
        {
            var path = Environment.GetEnvironmentVariable("SAVE_STORAGE_STATE");
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
    }

    public static string LoginUsername =>
        Environment.GetEnvironmentVariable("LOGIN_USERNAME")
        ?? throw new Exception("LOGIN_USERNAME is not set. A login test must be run by Auth (auth.mode = auto).");

    public static string LoginPassword =>
        Environment.GetEnvironmentVariable("LOGIN_PASSWORD")
        ?? throw new Exception("LOGIN_PASSWORD is not set. A login test must be run by Auth (auth.mode = auto).");

    public static string RunTimestamp { get; } =
        Environment.GetEnvironmentVariable("RUN_TIMESTAMP")
        ?? DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
}
