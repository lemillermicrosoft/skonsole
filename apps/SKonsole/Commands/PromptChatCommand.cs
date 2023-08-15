using System.CommandLine;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.SkillDefinition;
using SKonsole.Utils;

namespace SKonsole.Commands;

public class PromptChatCommand : Command
{
    public PromptChatCommand(ConfigurationProvider config, ILogger? logger = null) : base("chat", "Chat with the prompt bot")
    {
        if (logger is null)
        {
            using var loggerFactory = Logging.GetFactory();
            this._logger = loggerFactory.CreateLogger<PromptChatCommand>();
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

        const string skPrompt = @"
    You are a prompt generation robot. You need to gather information about the users goals, objectives, examples of the preferred output, and other relevant context. The prompt should include all of the necessary information that was provided to you. Ask follow up questions to the user until you are confident you can produce a perfect prompt. Your return should be formatted clearly and optimized for GPT models. Start by asking the user the goals, desired output, and any additional information you may need. Prefix messages with 'AI: '.

    {{$history}}
    AI:
    ";

        var promptConfig = new PromptTemplateConfig
        {
            Completion =
            {
                MaxTokens = 2000,
                Temperature = 0.7,
                TopP = 0.5,
                StopSequences = new List<string> { "Human:", "AI:" },
            }
        };
        var promptTemplate = new PromptTemplate(skPrompt, promptConfig, kernel);
        var functionConfig = new SemanticFunctionConfig(promptConfig, promptTemplate);
        var chatFunction = kernel.RegisterSemanticFunction("PromptBot", "Chat", functionConfig);
        await RunChat(kernel, logger, chatFunction);
    }

    private static async Task RunChat(IKernel kernel, ILogger? logger, ISKFunction chatFunction)
    {
        var contextVariables = new ContextVariables();

        var history = "";
        contextVariables.Set("history", history);

        var botMessage = await kernel.RunAsync(contextVariables, chatFunction);
        var userMessage = string.Empty;

        while (userMessage != "exit")
        {
            var botMessageFormatted = "\nAI: " + botMessage.ToString() + "\n";
            (logger ?? kernel.Logger).LogInformation("{botMessage}", botMessageFormatted);
            (logger ?? kernel.Logger).LogInformation(">>>");

            userMessage = ReadMutiLineInput();
            if (userMessage == "exit")
            {
                break;
            }

            history += $"{botMessageFormatted}Human: {userMessage}\nAI:";
            contextVariables.Set("history", history);

            botMessage = await kernel.RunAsync(contextVariables, chatFunction);
        }
    }

    private static string ReadMutiLineInput()
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
