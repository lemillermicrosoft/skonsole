using System.CommandLine;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Skills.Web;
using Microsoft.SemanticKernel.Skills.Web.Bing;
using SKonsole;
using SKonsole.Commands;
using SKonsole.Skills;
using SKonsole.Utils;

Console.OutputEncoding = Encoding.Unicode;
using var loggerFactory = Logging.GetFactory();// Get an instance of ILogger
var _logger = loggerFactory.CreateLogger<Program>();
var _kernel = KernelProvider.Instance.Get();
_kernel.Logger.LogTrace("KernelSingleton.Instance: adding Azure OpenAI backends");


var rootCommand = new RootCommand();
var prCommand = new Command("pr", "Pull Request feedback subcommand");
var prFeedbackCommand = new Command("feedback", "Pull Request feedback subcommand");
var prDescriptionCommand = new Command("description", "Pull Request description subcommand");
var plannerCommand = new Command("createPlan", "Planner subcommand");
var promptChatCommand = new Command("promptChat", "Prompt chat subcommand");


var messageArgument = new Argument<string>
    ("message", "An argument that is parsed as a string.");

plannerCommand.Add(messageArgument);

var targetBranchOption = new Option<string>(
       new string[] { "--targetBranch", "-t" },
          () => { return "origin/main"; },
             "The target branch for the pull request.");
prCommand.AddOption(targetBranchOption);
prDescriptionCommand.AddOption(targetBranchOption);
prFeedbackCommand.AddOption(targetBranchOption);

prCommand.SetHandler(async (targetBranch) => await RunPullRequestDescription(_kernel, _logger, targetBranch), targetBranchOption);
prFeedbackCommand.SetHandler(async (targetBranch) => await RunPullRequestFeedback(_kernel, _logger, targetBranch), targetBranchOption);
prDescriptionCommand.SetHandler(async (targetBranch) => await RunPullRequestDescription(_kernel, _logger, targetBranch), targetBranchOption);
plannerCommand.SetHandler(async (messageArgumentValue) => await RunCreatePlan(_kernel, _logger, messageArgumentValue), messageArgument);
promptChatCommand.SetHandler(async () => await RunPromptChat(_kernel, _logger));

prCommand.Add(prFeedbackCommand);
prCommand.Add(prDescriptionCommand);

rootCommand.Add(new ConfigCommand(ConfigurationProvider.Instance));
rootCommand.Add(new CommitCommand(ConfigurationProvider.Instance));
rootCommand.Add(prCommand);
rootCommand.Add(plannerCommand);
rootCommand.Add(promptChatCommand);

return await rootCommand.InvokeAsync(args);

static async Task RunPullRequestDescription(IKernel kernel, ILogger? logger, string targetBranch = "origin/main")
{
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"show --ignore-space-change {targetBranch}..HEAD",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        }
    };
    process.Start();

    string output = process.StandardOutput.ReadToEnd();
    var pullRequestSkill = kernel.ImportSkill(new PRSkill.PullRequestSkill(kernel));

    var kernelResponse = await kernel.RunAsync(output, pullRequestSkill["GeneratePR"]);
    (logger ?? kernel.Logger).LogInformation("Pull Request Description:\n{result}", kernelResponse.Result);
}

static async Task RunPullRequestFeedback(IKernel kernel, ILogger? logger, string targetBranch = "origin/main")
{
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"show --ignore-space-change {targetBranch}..HEAD",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        }
    };
    process.Start();

    string output = process.StandardOutput.ReadToEnd();

    var pullRequestSkill = kernel.ImportSkill(new PRSkill.PullRequestSkill(kernel));

    var kernelResponse = await kernel.RunAsync(output, pullRequestSkill["GeneratePullRequestFeedback"]);

    (logger ?? kernel.Logger).LogInformation("Pull Request Feedback:\n{result}", kernelResponse.Result);
}

static async Task RunCreatePlan(IKernel kernel, ILogger? logger, string message)
{
    // Eventually, Kernel will be smarter about what skills it uses for an ask.
    // kernel.ImportSkill(new EmailSkill(), "email");
    // kernel.ImportSkill(new GitSkill(), "git");
    // kernel.ImportSkill(new SearchUrlSkill(), "url");
    // kernel.ImportSkill(new HttpSkill(), "http");
    // kernel.ImportSkill(new PRSkill.PullRequestSkill(kernel), "PullRequest");

    kernel.ImportSkill(new WriterSkill(kernel), "writer");
    var bingConnector = new BingConnector(Configuration.ConfigVar("BING_API_KEY"));
    var bing = new WebSearchEngineSkill(bingConnector);
    var search = kernel.ImportSkill(bing, "bing");

    // var planner = new ActionPlanner();
    var planner = new SequentialPlanner(kernel);
    var plan = await planner.CreatePlanAsync(message);

    await plan.InvokeAsync();
}

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
