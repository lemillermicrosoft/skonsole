using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SKonsole;

public class ConfigurationProvider
{
    public static ConfigurationProvider Instance = new();
    private const string File = ".skonsole";

    private readonly string _path;
    private IConfiguration _configuration;
    private readonly Dictionary<string, string?> _config = new();

    public ConfigurationProvider()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        this._path = Path.Combine(userProfile, File);

        if (System.IO.File.Exists(this._path))
        {
            this._config = FromJson<Dictionary<string, string?>>(System.IO.File.ReadAllText(this._path)) ?? new();
        }

        this.LoadConfig();
        this.MergeDefaultConfig();
    }

    private void MergeDefaultConfig()
    {
        var defaultConfig = new Dictionary<string, string>()
        {
            { ConfigConstants.OPENAI_CHAT_MODEL_ID , "gpt-3.5-turbo" }
        };

        bool hasChanged = false;

        foreach (var defaultConfigItem in defaultConfig)
        {
            if (!string.IsNullOrWhiteSpace(this._configuration[defaultConfigItem.Key]))
            {
                this._config[defaultConfigItem.Key] = defaultConfigItem.Value;
                hasChanged = true;
            }
        }

        if (hasChanged)
        {
            this.LoadConfig();
        }
    }

    /// <summary>
    /// Load or reload the configuration from the in-memory collection.
    /// </summary>
    [MemberNotNull(nameof(_configuration))]
    private void LoadConfig()
    {
        this._configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddInMemoryCollection(this._config)
            .Build();
    }

    public async Task SaveConfig(string key, string value)
    {
        this._config[key] = value;

        await System.IO.File.WriteAllTextAsync(this._path, ToJson<Dictionary<string, string?>>(this._config));

        this.LoadConfig();
    }

    public string? Get(string key)
    {
        return this._configuration[key];
    }

    private static string ToJson<T>(T obj)
    {
        return JsonSerializer.Serialize(obj);
    }

    private static T? FromJson<T>(string json)
    {
        if (json == null)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or ArgumentNullException)
        {
            return default;
        }
    }
}
