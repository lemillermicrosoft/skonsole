using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Kernel;
using Reliability;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddFilter("Microsoft", LogLevel.Warning)
        .AddFilter("System", LogLevel.Warning)
        .AddConsole();
});

// Get an instance of ILogger
var logger = loggerFactory.CreateLogger<Program>();

var _kernel = SemanticKernel.Build(logger);

// _kernel.Log.Log(LogLevel.Warning, "KernelSingleton.Instance: adding OpenAI backends");
// _kernel.Config.AddOpenAICompletionBackend("text-davinci-003", "text-davinci-003", EnvVar("OPENAI_API_KEY"));

_kernel.Log.Log(LogLevel.Warning, "KernelSingleton.Instance: adding Azure OpenAI backends");
_kernel.Config.AddAzureOpenAICompletionBackend(EnvVar("AZURE_OPENAI_DEPLOYMENT_LABEL"), EnvVar("AZURE_OPENAI_DEPLOYMENT_NAME"), EnvVar("AZURE_OPENAI_API_ENDPOINT"), EnvVar("AZURE_OPENAI_API_KEY"));

_kernel.Config.SetRetryMechanism(new PollyRetryMechanism());

await RunCommitMessage(_kernel);
// await RunPullRequestFeedback(_kernel);

static async Task RunPullRequestFeedback(ISemanticKernel kernel)
{
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "show main..HEAD",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        }
    };
    process.Start();

    string output = process.StandardOutput.ReadToEnd();

    var pullRequestSkill = new PRSkill.PullRequestSkill(kernel);
    var kernelResponse = await kernel.RunAsync(output, pullRequestSkill.GeneratePullRequestFeedback);
    Console.WriteLine(kernelResponse.ToString());
}

static async Task RunCommitMessage(ISemanticKernel kernel)
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

    var pullRequestSkill = new PRSkill.PullRequestSkill(kernel);
    var kernelResponse = await kernel.RunAsync(output, pullRequestSkill.GenerateCommitMessage);
    Console.WriteLine(kernelResponse.ToString());
}

static string EnvVar(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrEmpty(value)) throw new Exception($"Env var not set: {name}");
    return value;
}
