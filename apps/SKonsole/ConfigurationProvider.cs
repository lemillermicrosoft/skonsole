using Microsoft.Extensions.Configuration;
using System.Text.Json;

public class ConfigurationProvider
{
    const string _file = ".skonsole";

    private readonly string _path;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, string?> _config = new();

    public ConfigurationProvider()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _path = Path.Combine(userProfile, _file);

        if (File.Exists(_path))
        {
            _config = FromJson<Dictionary<string, string?>>(File.ReadAllText(_path)) ?? new();
        }

        _configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddInMemoryCollection(_config)
            .Build();
    }

    public async Task SaveConfig(string key, string value)
    {
        _config[key] = value;

        await File.WriteAllTextAsync(_path, ToJson(_config));
    }

    public string? Get(string key)
    {
        return _configuration[key];
    }

    private static string ToJson<T>(T obj)
    {
        return JsonSerializer.Serialize(obj);
    }

    private static T? FromJson<T>(string json)
    {
        if (json == null)
            return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }
}