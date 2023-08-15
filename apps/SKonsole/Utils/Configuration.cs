namespace SKonsole.Utils;

internal static class Configuration
{
    internal static string ConfigVar(string name)
    {
        var provider = ConfigurationProvider.Instance;
        var value = provider.Get(name);
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentNullException($"Configuration var not set: {name}.Please run `skonsole config` to set it.");
        }

        return value;
    }
}
