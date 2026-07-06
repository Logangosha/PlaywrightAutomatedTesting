using System.Text.Json;
public class AuthConfig
{
    // NONE = NO AUTH
    // MANUAL = USER LOGINS MANUALLY (NO AUTO-LOGIN TEST)
    // AUTO = RUNS AN AUTO-LOGIN TEST WITH THE PROVIDED CREDENTIALS
    public string Mode { get; set; } = "none";
    public string? Username { get; set; }
    public string? Password { get; set; }
}
public class Config : IConfig
{
    public string Site { get; set; } = string.Empty;
    public string Env { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public AuthConfig Auth { get; set; } = new();
    public string Actions { get; set; } = "all";
    public bool Headless { get; set; } = true;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static Config Load(string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
            throw new Exception($"Config file not found: {fullPath}");

        Config? config;
        try
        {
            config = JsonSerializer.Deserialize<Config>(File.ReadAllText(fullPath), JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new Exception($"Config file is not valid JSON: {fullPath} — {ex.Message}");
        }

        if (config is null)
            throw new Exception($"Config file is empty: {fullPath}");

        config.Validate(fullPath);
        return config;
    }

    private void Validate(string path)
    {
        // VALIDATE TRAITS (SITE AND ENV) TO ENSURE THEY ARE NOT NULL OR EMPTY AND DO NOT CONTAIN INVALID CHARACTERS.
        ValidateTraitValue("site", Site, path);
        ValidateTraitValue("env", Env, path);

        // VALIDATE URL TO ENSURE IT IS A WELL-FORMED HTTP(S) URL.
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new Exception($"'url' must be an http(s) URL. Got '{Url}' in {path}");

        // VALIDATE AUTH MODE AND CREDENTIALS.
        var mode = Auth.Mode.ToLowerInvariant();
        if (mode is not ("none" or "manual" or "auto"))
            throw new Exception($"'auth.mode' must be none, manual, or auto. Got '{Auth.Mode}' in {path}");

        if (mode == "auto" &&
            (string.IsNullOrWhiteSpace(Auth.Username) || string.IsNullOrWhiteSpace(Auth.Password)))
            throw new Exception($"auth.mode is 'auto' but 'auth.username' or 'auth.password' is missing in {path}");

        if (!IsValidActionsSlice(Actions))
            throw new Exception(
                $"'actions' must be \"all\" or Category/Module filters like \"Category=Smoke&Module=HomePage\", " +
                $"and may OR alternatives with | (e.g. \"Module=HomePage|Module=MemberManagement\") " +
                $"(Site/Env/Kind are composed automatically from site+env). Got '{Actions}' in {path}");
    }

    // VALIDATES THAT THE TRAIT VALUE IS NOT NULL OR EMPTY AND DOES NOT CONTAIN INVALID CHARACTERS.
    private static void ValidateTraitValue(string key, string value, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new Exception($"'{key}' is required (it selects the tests via the {Capitalize(key)} trait) in {path}");

        // CANNOT CONTAIN &, |, =, !, PARENTHESES, OR SPACES BECAUSE THESE ARE USED IN THE TRAIT FILTER SYNTAX.
        if (value.IndexOfAny(new[] { '&', '|', '=', '!', '(', ')', ' ' }) >= 0)
            throw new Exception($"'{key}' must not contain &, |, =, !, parentheses, or spaces. Got '{value}' in {path}");
    }

    // VALIDATES THAT THE ACTIONS SLICE IS EITHER "ALL" OR A COMMA-SEPARATED LIST OF CATEGORY/MODULE FILTERS.
    private static bool IsValidActionsSlice(string input)
    {
        // ALLOW "ALL" OR EMPTY TO RUN ALL TESTS.
        if (string.IsNullOrWhiteSpace(input) || input.Equals("all", StringComparison.OrdinalIgnoreCase))
            return true;

        // SET OF ALLOWED KEYS FOR FILTERING TESTS. ONLY "Category" AND "Module" ARE ALLOWED.
        var allowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Category", "Module" };

        // SPLIT THE INPUT BY & OR | AND VALIDATE EACH PART.
        foreach (var part in input.Split(new[] { '&', '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            // SPLIT EACH PART BY = AND ENSURE IT HAS EXACTLY TWO PARTS: KEY AND VALUE.
            var kv = part.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);

            // ENSUME THAT EACH PART HAS EXACTLY TWO PARTS: KEY AND VALUE.
            if (kv.Length != 2)
                return false;

            // ENSURE THAT THE KEY IS ALLOWED AND THE VALUE IS NOT EMPTY OR WHITESPACE.
            if (!allowedKeys.Contains(kv[0].Trim()) || string.IsNullOrWhiteSpace(kv[1].Trim()))
                return false;
        }

        return true;
    }

    private static string Capitalize(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
