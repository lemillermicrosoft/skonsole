using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SKonsole;

public class ConfigurationProvider
{
    public static ConfigurationProvider Instance = new();
    private const string _file = ".skonsole";

    private readonly string _path;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, string?> _config = new();

    public ConfigurationProvider()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        this._path = Path.Combine(userProfile, _file);

        if (File.Exists(this._path))
        {
            this._config = FromJson<Dictionary<string, string?>>(File.ReadAllText(this._path)) ?? new();
        }

        this._configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddInMemoryCollection(this._config)
            .Build();
    }

    public async Task SaveConfig(string key, string value)
    {
        this._config[key] = value;

        await File.WriteAllTextAsync(this._path, ToJson(this._config));
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
