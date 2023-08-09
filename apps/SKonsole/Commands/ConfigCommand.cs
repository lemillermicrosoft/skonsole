using Newtonsoft.Json.Linq;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKonsole.Commands
{
    public class ConfigCommand : Command
    {
        private readonly ConfigurationProvider _config;

        public ConfigCommand(ConfigurationProvider config) : base("config", "skonsole configuration")
        {
            this._config = config;
            Add(ConfigGetCommand());
            Add(ConfigSetCommand());

            this.SetHandler(async context => await RunConfigAsync(context.GetCancellationToken()));
        }

        Command ConfigGetCommand()
        {
            var keyArgument = new Argument<string>("key", "configuration key");

            var getCommand = new Command("get", "get configuration value");

            getCommand.AddArgument(keyArgument);

            getCommand.SetHandler((key) =>
            {
                var value = _config.Get(key);
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


        Command ConfigSetCommand()
        {
            var keyArgument = new Argument<string>("key", "configuration key");
            var valueArgument = new Argument<string>("value", "configuration value");

            var setCommand = new Command("set", "set configuration value");
            setCommand.AddArgument(keyArgument);
            setCommand.AddArgument(valueArgument);

            setCommand.SetHandler(_config.SaveConfig, keyArgument, valueArgument);
            return setCommand;
        }

        static async Task RunConfigAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var keys = new[]
                {
                    "AZURE_OPENAI_CHAT_DEPLOYMENT_NAME",
                    "AZURE_OPENAI_API_ENDPOINT",
                    "AZURE_OPENAI_API_KEY"
                };

                var config = new ConfigurationProvider();
                var configKey = await new SelectionPrompt<string>()
                        .Title("Select key to config:")
                        .AddChoices(keys)
                        .ShowAsync(AnsiConsole.Console, token);

                var currentValue = config.Get(configKey);

                var value = await new TextPrompt<string>($"Set value for [green]{configKey}[/]")
                    .DefaultValue(currentValue ?? string.Empty)
                    .HideDefaultValue()
                    .Validate((value) =>
                    {
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            return ValidationResult.Error("[red]Value cannot be empty[/]");
                        }
                        return ValidationResult.Success();
                    })
                    .AllowEmpty()
                    .ShowAsync(AnsiConsole.Console, token);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    await config.SaveConfig(configKey, value);
                }
            }
        }
    }

}
