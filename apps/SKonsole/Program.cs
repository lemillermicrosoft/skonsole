using System.CommandLine;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CodeRewriteSkillLib;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Reliability;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Skills.Web;
using Microsoft.SemanticKernel.Skills.Web.Bing;
using SKonsole.Skills;

Console.OutputEncoding = Encoding.Unicode;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddFilter("Microsoft", LogLevel.Warning)
        .AddFilter("System", LogLevel.Warning)
        .AddConsole();
});

// Get an instance of ILogger
var logger = loggerFactory.CreateLogger<Program>();

var _kernel = Kernel.Builder.WithLogger(logger).Build();

// _kernel.Log.LogTrace("KernelSingleton.Instance: adding OpenAI backends");
// _kernel.Config.AddOpenAICompletionBackend("text-davinci-003", "text-davinci-003", EnvVar("OPENAI_API_KEY"));

_kernel.Log.LogTrace("KernelSingleton.Instance: adding Azure OpenAI backends");
_kernel.Config.AddAzureTextCompletionService(EnvVar("AZURE_OPENAI_DEPLOYMENT_NAME"), EnvVar("AZURE_OPENAI_API_ENDPOINT"), EnvVar("AZURE_OPENAI_API_KEY"), EnvVar("AZURE_OPENAI_DEPLOYMENT_LABEL"));

_kernel.Config.SetDefaultHttpRetryConfig(new HttpRetryConfig
{
    MaxRetryCount = 3,
    MinRetryDelay = TimeSpan.FromSeconds(8),
    UseExponentialBackoff = true,
});

var rootCommand = new RootCommand();
var commitCommand = new Command("commit", "Commit subcommand");
var designCommand = new Command("designdoc", "Design document subcommand");
var motivationCommand = new Command("motivation", "Motivation and context subcommand");
//RunGenerateDescription
var descriptionCommand = new Command("description", "Description subcommand");
var prCommand = new Command("pr", "Pull Request feedback subcommand");
var prFeedbackCommand = new Command("feedback", "Pull Request feedback subcommand");
var prDescriptionCommand = new Command("description", "Pull Request description subcommand");
var plannerCommand = new Command("createplan", "Planner subcommand");
var promptChatCommand = new Command("promptchat", "Prompt chat subcommand");
var generalChatCommand = new Command("chat", "General chat subcommand");
var runCodeGenCommand = new Command("codegen", "Code generation subcommand");
var runCodeRewriteCommand = new Command("coderewrite", "Code rewrite subcommand");
var pathArgument = new Argument<string>
    ("path", "An argument that is parsed as a string.");
runCodeGenCommand.Add(pathArgument);
runCodeRewriteCommand.Add(pathArgument);
var messageArgument = new Argument<string>
    ("message", "An argument that is parsed as a string.");
plannerCommand.Add(messageArgument);

rootCommand.SetHandler(async () => await RunCommitMessage(_kernel));
commitCommand.SetHandler(async () => await RunCommitMessage(_kernel));
prCommand.SetHandler(async () => await RunPullRequestDescription(_kernel));
designCommand.SetHandler(async () => await RunGenerateDesignDoc(_kernel));
//RunGenerateMotivationAndContext
motivationCommand.SetHandler(async () => await RunGenerateMotivationAndContext(_kernel));
//RunGenerateDescription
descriptionCommand.SetHandler(async () => await RunGenerateDescription(_kernel));

prFeedbackCommand.SetHandler(async () => await RunPullRequestFeedback(_kernel));
prDescriptionCommand.SetHandler(async () => await RunPullRequestDescription(_kernel));
plannerCommand.SetHandler(async (messageArgumentValue) => await RunCreatePlan(_kernel, messageArgumentValue), messageArgument);
promptChatCommand.SetHandler(async () => await RunPromptChat(_kernel));
generalChatCommand.SetHandler(async () => await RunGeneralChat(_kernel));

runCodeGenCommand.SetHandler(async (pathArgumentValue) => await RunCodeGen(_kernel, pathArgumentValue), pathArgument);
runCodeRewriteCommand.SetHandler(async (pathArgumentValue) => await RunCodeRewrite(_kernel, pathArgumentValue), pathArgument);

prCommand.Add(prFeedbackCommand);
prCommand.Add(prDescriptionCommand);

rootCommand.Add(commitCommand);
rootCommand.Add(prCommand);
rootCommand.Add(plannerCommand);
rootCommand.Add(promptChatCommand);
rootCommand.Add(generalChatCommand);
rootCommand.Add(runCodeGenCommand);
rootCommand.Add(runCodeRewriteCommand);
rootCommand.Add(designCommand);
rootCommand.Add(motivationCommand);
rootCommand.Add(descriptionCommand);

return await rootCommand.InvokeAsync(args);

static async Task RunCommitMessage(IKernel kernel)
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

    string output = process.StandardOutput.ReadToEnd();

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

    var pullRequestSkill = kernel.ImportSkill(new PRSkill.PullRequestSkill(kernel));

    var kernelResponse = await kernel.RunAsync(output, pullRequestSkill["GenerateCommitMessage"]);

    // kernel.Log.LogInformation(kernelResponse.ToString());
    kernel.Log.LogInformation(kernelResponse.ToString());
}

static async Task RunPullRequestDescription(IKernel kernel)
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
    kernel.Log.LogInformation(kernelResponse.ToString());
}

//GenerateDesignDoc
static async Task RunGenerateDesignDoc(IKernel kernel)
{
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "diff --ignore-space-change origin/main..HEAD",
            RedirectStandardOutput = true,
            WorkingDirectory = "D:/repos/semantic-kernel/dotnet/src/SemanticKernel/",
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        }
    };
    process.Start();

    string output = process.StandardOutput.ReadToEnd();
    var pullRequestSkill = kernel.ImportSkill(new PRSkill.PullRequestSkill(kernel));

    var kernelResponse = await kernel.RunAsync(output, pullRequestSkill["GenerateDesignDoc"]);
    kernel.Log.LogInformation(kernelResponse.ToString());
}

//GenerateMotivationAndContext
static async Task RunGenerateMotivationAndContext(IKernel kernel)
{
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "diff --ignore-space-change origin/main..HEAD",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        }
    };
    process.Start();

    string output = process.StandardOutput.ReadToEnd();
    var pullRequestSkill = kernel.ImportSkill(new PRSkill.PullRequestSkill(kernel));

    var kernelResponse = await kernel.RunAsync(output, pullRequestSkill["GenerateMotivationAndContext"]);
    kernel.Log.LogInformation(kernelResponse.ToString());
}
//GenerateDescription
static async Task RunGenerateDescription(IKernel kernel)
{
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "diff --ignore-space-change origin/main..HEAD",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        }
    };
    process.Start();

    string output = process.StandardOutput.ReadToEnd();
    var pullRequestSkill = kernel.ImportSkill(new PRSkill.PullRequestSkill(kernel));

    var kernelResponse = await kernel.RunAsync(output, pullRequestSkill["GenerateDescription"]);
    kernel.Log.LogInformation(kernelResponse.ToString());
}

static async Task RunPullRequestFeedback(IKernel kernel)
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

    kernel.Log.LogInformation(kernelResponse.ToString());
}

static async Task RunCodeRewrite(IKernel kernel, string rootPath)
{
    var codeRewriteSkill = kernel.ImportSkill(new CodeRewriteSkill(kernel));

    // loop through files
    var fileList = (await codeRewriteSkill["FindAllFiles"].InvokeAsync(rootPath)).Result;

    var files = JsonSerializer.Deserialize<string[]>(fileList) ?? Array.Empty<string>();

    // rewrite each file
    foreach (var file in files)
    {
        var kernelResponse = await kernel.RunAsync(file, codeRewriteSkill["CSharpToTypescript"]);

        var result = kernelResponse.Result;

        kernel.Log.LogInformation("Read file: " + file);
        // kernel.Log.LogInformation(result);

        // write the file to a relative path
        var relativePath = file.Replace(rootPath, "").Replace(".cs", ".ts");
        // var currentDirectory = Directory.GetCurrentDirectory();
        var currentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var outputPath = Path.Combine(currentDirectory, "SKonsole", "output", relativePath);
        // var outputDirectory = Path.GetDirectoryName(outputPath); // this is wrong
        // Console.Write("Creating directory: " + Directory.GetParent(outputPath)?.FullName);
        Directory.CreateDirectory(Directory.GetParent(outputPath)?.FullName ?? throw new InvalidOperationException());
        kernel.Log.LogInformation("Writing file: " + outputPath);
        File.WriteAllText(outputPath, result);
    }
}

static async Task RunCodeGen(IKernel kernel, string rootPath)
{
    // Given a root path, iterate over files and generate code.
    var codeGenSkill = kernel.ImportSkill(new CodeGenSkill(kernel));

    // loop through files
    var files = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories);
    foreach (var file in files)
    {
        var kernelResponse = await kernel.RunAsync(file, codeGenSkill["CodeGen"]);

        var result = kernelResponse.Result.Replace("[END TYPESCRIPT CODE]", "");

        kernel.Log.LogInformation("Read file: " + file);
        kernel.Log.LogInformation(result);

        // write the file to a relative path
        var relativePath = file.Replace(rootPath, "").Replace(".cs", ".ts");
        var currentDirectory = Directory.GetCurrentDirectory();
        var outputPath = Path.Combine(currentDirectory, "output", relativePath);
        // var outputDirectory = Path.GetDirectoryName(outputPath); // this is wrong
        Console.Write("Creating directory: output");
        var createdDir = Directory.CreateDirectory("output");
        kernel.Log.LogInformation("Directory created: " + createdDir.FullName);
        kernel.Log.LogInformation("Writing file: " + outputPath);
        File.WriteAllText(outputPath, result);
    }
}

static async Task RunCreatePlan(IKernel kernel, string message)
{
    // Eventually, Kernel will be smarter about what skills it uses for an ask.
    // kernel.ImportSkill(new EmailSkill(), "email");
    // kernel.ImportSkill(new GitSkill(), "git");
    // kernel.ImportSkill(new SearchUrlSkill(), "url");
    // kernel.ImportSkill(new HttpSkill(), "http");
    // kernel.ImportSkill(new PRSkill.PullRequestSkill(kernel), "PullRequest");

    kernel.ImportSkill(new WriterSkill(kernel), "writer");

    using var bingConnector = new BingConnector(EnvVar("BING_API_KEY"));
    var bing = new WebSearchEngineSkill(bingConnector);
    var search = kernel.ImportSkill(bing, "bing");

    // var planner = new ActionPlanner();
    var planner = new SequentialPlanner(kernel);
    var plan = await planner.CreatePlanAsync(message);

    await plan.InvokeAsync();
}

static async Task RunGeneralChat(IKernel kernel)
{
    const string skPrompt =
        @"You are a GPT model that can chat with users on various topics that interest them. You need to engage the user in a friendly and informative conversation. The prompt should include a greeting, a brief introduction of your capabilities, and a request for the user to choose a topic. You should also acknowledge your limitations and invite feedback from the user. You are able to produce code and advise solutions for software design. Prefix messages with 'AI: '.

{{$history}}
AI:";

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
    var function = kernel.RegisterSemanticFunction("ChatBot", "chat", functionConfig);

    await RunChat(kernel, function);
}

static async Task RunPromptChat(IKernel kernel)
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
    await RunChat(kernel, chatFunction);
}

static string EnvVar(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrEmpty(value)) throw new Exception($"Env var not set: {name}");
    return value;
}

static async Task RunChat(IKernel kernel, ISKFunction chatFunction)
{
    var contextVariables = new ContextVariables();

    var history = "";
    contextVariables.Set("history", history);

    var botMessage = await kernel.RunAsync(contextVariables, chatFunction);
    var userMessage = string.Empty;

    while (userMessage != "exit")
    {
        var botMessageFormatted = "\nAI: " + botMessage.ToString() + "\n";
        kernel.Log.LogInformation(botMessageFormatted);
        Console.Write(">>>");

        userMessage = Console.ReadLine(); // TODO -- How to support multi-line input?
        if (userMessage == "exit") break;

        history += $"{botMessageFormatted}Human: {userMessage}\nAI:";
        contextVariables.Set("history", history);

        botMessage = await kernel.RunAsync(contextVariables, chatFunction);
    }
}