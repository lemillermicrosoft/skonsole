using System.CommandLine;
using System.CommandLine.Builder;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Reliability;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Skills.Web;
using Microsoft.SemanticKernel.Skills.Web.Bing;
using SKonsole.Commands;
using SKonsole.Skills;

Console.OutputEncoding = Encoding.Unicode;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddFilter("Microsoft", LogLevel.Error)
        .AddFilter("System", LogLevel.Error)
        .AddFilter("Program", LogLevel.Information)
        .AddConsole();
});

// Get an instance of ILogger
var _logger = loggerFactory.CreateLogger<Program>();
var kernelLogger = loggerFactory.CreateLogger<Kernel>();

var _kernel = Kernel.Builder
.Configure((config) =>
{
    config.SetDefaultHttpRetryConfig(new HttpRetryConfig
    {
        MaxRetryCount = 3,
        MinRetryDelay = TimeSpan.FromSeconds(8),
        UseExponentialBackoff = true,
    });
})
.WithAzureChatCompletionService(
    ConfigVar("AZURE_OPENAI_CHAT_DEPLOYMENT_NAME"),
    ConfigVar("AZURE_OPENAI_API_ENDPOINT"),
    ConfigVar("AZURE_OPENAI_API_KEY"))
.WithLogger(kernelLogger)
.Build();

_kernel.Logger.LogTrace("KernelSingleton.Instance: adding Azure OpenAI backends");


var rootCommand = new RootCommand();
var commitCommand = new Command("commit", "Commit subcommand");
var prCommand = new Command("pr", "Pull Request feedback subcommand");
var prFeedbackCommand = new Command("feedback", "Pull Request feedback subcommand");
var prDescriptionCommand = new Command("description", "Pull Request description subcommand");
var plannerCommand = new Command("createPlan", "Planner subcommand");
var promptChatCommand = new Command("promptChat", "Prompt chat subcommand");


var messageArgument = new Argument<string>
    ("message", "An argument that is parsed as a string.");

plannerCommand.Add(messageArgument);

var commitArgument = new Argument<string>
    ("commitHash", () => { return string.Empty; }, "An argument that is parsed as a string.");
rootCommand.Add(commitArgument);
commitCommand.Add(commitArgument);

rootCommand.SetHandler(async (commitArgumentValue) => await RunCommitMessage(_kernel, _logger, commitArgumentValue), commitArgument);
commitCommand.SetHandler(async (commitArgumentValue) => await RunCommitMessage(_kernel, _logger, commitArgumentValue), commitArgument);
prCommand.SetHandler(async () => await RunPullRequestDescription(_kernel, _logger));
prFeedbackCommand.SetHandler(async () => await RunPullRequestFeedback(_kernel, _logger));
prDescriptionCommand.SetHandler(async () => await RunPullRequestDescription(_kernel, _logger));
plannerCommand.SetHandler(async (messageArgumentValue) => await RunCreatePlan(_kernel, _logger, messageArgumentValue), messageArgument);
promptChatCommand.SetHandler(async () => await RunPromptChat(_kernel, _logger));

prCommand.Add(prFeedbackCommand);
prCommand.Add(prDescriptionCommand);

rootCommand.Add(new ConfigCommand(ConfigurationProvider.Instance));
rootCommand.Add(commitCommand);
rootCommand.Add(prCommand);
rootCommand.Add(plannerCommand);
rootCommand.Add(promptChatCommand);

return await rootCommand.InvokeAsync(args);

static async Task RunCommitMessage(IKernel kernel, ILogger? logger, string commitHash = "")
{
    string output = string.Empty;
    if (!string.IsNullOrEmpty(commitHash))
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"show {commitHash}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            }
        };
        process.Start();

        output = process.StandardOutput.ReadToEnd();
    }
    else
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "diff --staged",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            }
        };
        process.Start();

        output = process.StandardOutput.ReadToEnd();

        if (string.IsNullOrEmpty(output))
        {
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "diff HEAD~1",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                }
            };
            process.Start();

            output = process.StandardOutput.ReadToEnd();
        }
    }

    var pullRequestSkill = kernel.ImportSkill(new PRSkill.PullRequestSkill(kernel));

    var kernelResponse = await kernel.RunAsync(output, pullRequestSkill["GenerateCommitMessage"]);

    (logger ?? kernel.Logger).LogInformation("Commit Message:\n{result}", kernelResponse.Result);
}

static async Task RunPullRequestDescription(IKernel kernel, ILogger? logger)
{
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "show --ignore-space-change origin/main..HEAD",
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

static async Task RunPullRequestFeedback(IKernel kernel, ILogger? logger)
{
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "show --ignore-space-change origin/main..HEAD",
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

    var bingConnector = new BingConnector(ConfigVar("BING_API_KEY"));
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

static string ConfigVar(string name)
{
    var provider = ConfigurationProvider.Instance;
    var value = provider.Get(name);
    if (string.IsNullOrEmpty(value)) throw new Exception($"Configuration var not set: {name}.Please run `skonsole config` to set it.");
    return value;
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
        if (userMessage == "exit") break;

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