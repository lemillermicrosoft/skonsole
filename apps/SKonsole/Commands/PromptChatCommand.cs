using System.CommandLine;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using SKonsole.Utils;
using Spectre.Console;

namespace SKonsole.Commands;

public class PromptChatCommand : Command
{
    public PromptChatCommand(ConfigurationProvider config, ILogger? logger = null) : base("chat", "Chat with the prompt bot")
    {
        if (logger is null)
        {
            using var loggerFactory = Logging.GetFactory();
            this._logger = loggerFactory.CreateLogger(this.GetType());
        }
        else
        {
            this._logger = logger;
        }

        this.Add(this.GeneratePromptChatCommand());
        this.SetHandler(async context => await RunPromptChat(context.GetCancellationToken(), this._logger));
    }

    private Command GeneratePromptChatCommand()
    {
        var createPlanCommand = new Command("promptChat", "Prompt chat subcommand");
        createPlanCommand.SetHandler(async () => await RunPromptChat(CancellationToken.None, this._logger));
        return createPlanCommand;
    }

    private static async Task RunPromptChat(CancellationToken token, ILogger logger)
    {
        var kernel = KernelProvider.Instance.Get();

        const string SkPrompt = @"
    You are a prompt generation robot. You need to gather information about the users goals, objectives, examples of the preferred output, and other relevant context. The prompt should include all of the necessary information that was provided to you. Ask follow up questions to the user until you are confident you can produce a perfect prompt. Your return should be formatted clearly and optimized for GPT models. Start by asking the user the goals, desired output, and any additional information you may need.

    {{$history}}
    AI:
    ";

        var chatFunction = kernel.CreateFunctionFromPrompt(
            SkPrompt,
            new PromptExecutionSettings()
            {
                ExtensionData = new Dictionary<string, object>()
                {
                    { "Temperature", 0.7 },
                    { "TopP", 0.5 },
                    { "MaxTokens", 2000 },
                    { "StopSequences", new List<string> { "Human:", "AI:" } }
                }
            },
            "Chat");
        await RunChat(kernel, logger, chatFunction);
    }

    private static async Task RunChat(Kernel kernel, ILogger? logger, KernelFunction chatFunction)
    {
        AnsiConsole.MarkupLine("[grey]Press Enter twice to send a message.[/]");
        AnsiConsole.MarkupLine("[grey]Enter 'exit' to exit.[/]");
        var contextVariables = new KernelArguments();

        var history = string.Empty;
        contextVariables["history"] = history;

        var result = await kernel.InvokeAsync(chatFunction, contextVariables);
        var botMessage = result.GetValue<string>();
        var userMessage = string.Empty;

        static void HorizontalRule(string title, string style = "white bold")
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule($"[{style}]{title}[/]").RuleStyle("grey").LeftJustified());
            AnsiConsole.WriteLine();
        }

        while (userMessage != "exit")
        {
            HorizontalRule("AI", "green bold");
            AnsiConsole.Foreground = ConsoleColor.Green;
            AnsiConsole.WriteLine(botMessage?.ToString() ?? "NO MESSAGE FROM BOT");
            AnsiConsole.ResetColors();

            HorizontalRule("User");
            userMessage = ReadMultiLineInput();

            if (userMessage == "exit")
            {
                break;
            }

            history += $"AI: {botMessage}\nHuman: {userMessage} \n";
            contextVariables["history"] = history;

            botMessage = await AnsiConsole.Progress()
                .AutoClear(true)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new SpinnerColumn(),
                })
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Thinking...[/]", autoStart: true).IsIndeterminate();

                    var result = await kernel.InvokeAsync(chatFunction, contextVariables);

                    task.StopTask();
                    return result.GetValue<string>();
                });
        }
    }

    private static string ReadMultiLineInput()
    {
        var input = new StringBuilder();
        var line = string.Empty;

        while ((line = Console.ReadLine()) != string.Empty)
        {
            input.AppendLine(line);
        }

        return input.ToString().Trim();
    }

    private readonly ILogger _logger;
}
