using System.CommandLine;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.SkillDefinition;
using SKonsole;
using SKonsole.Commands;
using SKonsole.Utils;

Console.OutputEncoding = Encoding.Unicode;

using var loggerFactory = Logging.GetFactory();

var _logger = loggerFactory.CreateLogger<Program>();
_logger.LogDebug("Starting SKonsole");
var _kernel = KernelProvider.Instance.Get();

var rootCommand = new RootCommand();
var promptChatCommand = new Command("promptChat", "Prompt chat subcommand");
promptChatCommand.SetHandler(async () => await RunPromptChat(_kernel, _logger));


rootCommand.Add(new ConfigCommand(ConfigurationProvider.Instance));
rootCommand.Add(new CommitCommand(ConfigurationProvider.Instance));
rootCommand.Add(new PRCommand(ConfigurationProvider.Instance));
rootCommand.Add(new PlannerCommand(ConfigurationProvider.Instance));
rootCommand.Add(promptChatCommand);

return await rootCommand.InvokeAsync(args);

static async Task RunPromptChat(IKernel kernel, ILogger? logger)
{
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

static async Task RunChat(IKernel kernel, ILogger? logger, ISKFunction chatFunction)
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

static string ReadMutiLineInput()
{
    var input = new StringBuilder();
    var line = string.Empty;

    while ((line = Console.ReadLine()) != string.Empty)
    {
        input.AppendLine(line);
    }

    return input.ToString().Trim();
}
