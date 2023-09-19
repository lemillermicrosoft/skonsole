using System.CommandLine;
using SKonsole.Utils;
using Spectre.Console;

namespace SKonsole.Commands;

public class ConfigCommand : Command
{
    private readonly ConfigurationProvider _config;

    public ConfigCommand(ConfigurationProvider config) : base("config", "skonsole configuration")
    {
        this._config = config;
        this.Add(this.ConfigGetCommand());
        this.Add(this.ConfigSetCommand());

        this.SetHandler(async context => await RunConfigAsync(context.GetCancellationToken()));
    }

    private Command ConfigGetCommand()
    {
        var keyArgument = new Argument<string>("key", "configuration key");

        var getCommand = new Command("get", "get configuration value");

        getCommand.AddArgument(keyArgument);

        getCommand.SetHandler((key) =>
        {
            var value = this._config.Get(key);
            if (value == null)
            {
                AnsiConsole.MarkupLine($"[red]Configuration key '{key}' not found.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]{key}[/]: {value}");
            }
        }, keyArgument);
        return getCommand;
    }

    private Command ConfigSetCommand()
    {
        var keyArgument = new Argument<string>("key", "configuration key");
        var valueArgument = new Argument<string>("value", "configuration value");

        var setCommand = new Command("set", "set configuration value");
        setCommand.AddArgument(keyArgument);
        setCommand.AddArgument(valueArgument);

        setCommand.SetHandler(this._config.SaveConfig, keyArgument, valueArgument);
        return setCommand;
    }

    private static async Task RunConfigAsync(CancellationToken token)
    {
        var mainMenuKeys = new[] { "LLM" };
        await ConfigOrExitAsync(mainMenuKeys,
        "Select config:",
        async (configKey) =>
        {
            if (configKey == "LLM")
            {
                await LLMConfigAsync(token);
            }
        }, token);
    }

    private static async Task ConfigOrExitAsync(string[] menuKeys, string title, Func<string, Task> ConfigTask, CancellationToken token)
    {
        const string Exit = nameof(Exit);

        var keysWithExit = menuKeys.Append(Exit).ToArray();
        while (!token.IsCancellationRequested)
        {
            var selectItem = await new SelectionPrompt<string>()
                    .Title(title)
                    .AddChoices(keysWithExit)
                    .ShowAsync(AnsiConsole.Console, token);

            if (selectItem == Exit)
            {
                return;
            }

            await ConfigTask(selectItem);
        }
    }

    private static async Task RunKeyValueConfigAsync(string[] keys, CancellationToken cancellationToken)
    {
        var config = ConfigurationProvider.Instance;
        await ConfigOrExitAsync(keys,
        "Select config:",
        async (configKey) =>
        {
            var hasSecret = configKey.Contains("KEY", StringComparison.OrdinalIgnoreCase);
            var currentValue = config.Get(configKey);
            var value = await new TextPrompt<string>($"Set value for [green]{configKey}[/]")
                                .DefaultValue(currentValue ?? string.Empty)
                                .IsSecret(hasSecret)
                                .Validate((value) =>
                                {
                                    if (string.IsNullOrWhiteSpace(value))
                                    {
                                        return ValidationResult.Error("[red]Value cannot be empty[/]");
                                    }
                                    return ValidationResult.Success();
                                })
                                .AllowEmpty()
                                .ShowAsync(AnsiConsole.Console, cancellationToken);

            if (!string.IsNullOrWhiteSpace(value))
            {
                await config.SaveConfig(configKey, value.Trim());
            }
        }, cancellationToken);
    }

    private static async Task LLMConfigAsync(CancellationToken cancellationToken)
    {
        await ConfigOrExitAsync(new[]
            {
                ConfigConstants.AzureOpenAI,
                ConfigConstants.OpenAI
            },
        "Select LLM:",
        async (LLM) =>
        {
            var config = ConfigurationProvider.Instance;

            switch (LLM)
            {
                case ConfigConstants.AzureOpenAI:
                {
                    var keys = new[]
                    {
                    ConfigConstants.AZURE_OPENAI_CHAT_DEPLOYMENT_NAME,
                    ConfigConstants.AZURE_OPENAI_API_ENDPOINT,
                    ConfigConstants.AZURE_OPENAI_API_KEY
                    };
                    await RunKeyValueConfigAsync(keys, cancellationToken);
                    await config.SaveConfig(ConfigConstants.LLM_PROVIDER, ConfigConstants.AzureOpenAI);
                    break;
                }
                case ConfigConstants.OpenAI:
                {
                    var keys = new[]
                    {
                    ConfigConstants.OPENAI_CHAT_MODEL_ID,
                    ConfigConstants.OPENAI_API_KEY
                    };
                    await RunKeyValueConfigAsync(keys, cancellationToken);
                    await config.SaveConfig(ConfigConstants.LLM_PROVIDER, ConfigConstants.OpenAI);
                    break;
                }
            }
        }, cancellationToken);
    }
}
