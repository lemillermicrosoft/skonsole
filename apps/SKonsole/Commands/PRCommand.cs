using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SKonsole.Utils;

namespace SKonsole.Commands;

public class PRCommand : Command
{
    public PRCommand(ConfigurationProvider config, ILogger? logger = null) : base("pr", "skonsole pull request command")
    {
        if (logger is null)
        {
            using var loggerFactory = Logging.GetFactory();
            this._logger = loggerFactory.CreateLogger<PRCommand>();
        }
        else
        {
            this._logger = logger;
        }

        this.Add(this.GeneratePRFeedbackCommand());
        this.Add(this.GeneratePRDescriptionCommand());
        this.SetHandler(async context => await RunPullRequestDescription(context.GetCancellationToken(), this._logger));
    }

    private Command GeneratePRFeedbackCommand()
    {
        var prFeedbackCommand = new Command("feedback", "Pull Request feedback subcommand");
        prFeedbackCommand.SetHandler(async () => await RunPullRequestFeedback(CancellationToken.None, this._logger));
        return prFeedbackCommand;
    }

    private Command GeneratePRDescriptionCommand()
    {
        var prDescriptionCommand = new Command("description", "Pull Request description subcommand");
        prDescriptionCommand.SetHandler(async () => await RunPullRequestDescription(CancellationToken.None, this._logger));
        return prDescriptionCommand;
    }

    private static async Task RunPullRequestFeedback(CancellationToken token, ILogger logger, string targetBranch = "origin/main")
    {
        var kernel = KernelProvider.Instance.Get();

        using var process = new Process
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

        var kernelResponse = await kernel.RunAsync(output, token, pullRequestSkill["GeneratePullRequestFeedback"]);

        logger.LogInformation("Pull Request Feedback:\n{result}", kernelResponse.Result);
    }

    private static async Task RunPullRequestDescription(CancellationToken token, ILogger logger, string targetBranch = "origin/main")
    {
        var kernel = KernelProvider.Instance.Get();

        using var process = new Process
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

        var kernelResponse = await kernel.RunAsync(output, token, pullRequestSkill["GeneratePR"]);
        logger.LogInformation("Pull Request Description:\n{result}", kernelResponse.Result);
    }

    private readonly ILogger _logger;
}
