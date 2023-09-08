using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Orchestration;
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

        var targetBranchOption = new Option<string>(
                            new string[] { "--targetBranch", "-t" },
                            () => { return "origin/main"; },
                            "The target branch for the pull request.");

        this.AddOption(targetBranchOption);

        var outputJsonOption = new Option<bool>(
                            new string[] { "--outputJson", "-j" },
                            () => { return false; },
                            "Output the result as json. Suppresses logging.");

        this.AddOption(outputJsonOption);

        this.Add(this.GeneratePRFeedbackCommand(targetBranchOption));
        this.Add(this.GeneratePRDescriptionCommand(targetBranchOption, outputJsonOption));
        this.SetHandler(async context => await RunPullRequestDescription(context.GetCancellationToken(), this._logger, this.TryGetValueFromOption(context, targetBranchOption) ?? "origin/main"));
    }

    private T? TryGetValueFromOption<T>(InvocationContext context, Option<T> option)
    {
        return context.ParseResult.GetValueForOption(option);
    }
    private Command GeneratePRFeedbackCommand(Option<string> targetBranchOption)
    {
        var prFeedbackCommand = new Command("feedback", "Pull Request feedback subcommand");
        prFeedbackCommand.AddOption(targetBranchOption);
        prFeedbackCommand.SetHandler(async context => await RunPullRequestFeedback(context.GetCancellationToken(), this._logger, this.TryGetValueFromOption(context, targetBranchOption) ?? "origin/main"));
        return prFeedbackCommand;
    }

    private Command GeneratePRDescriptionCommand(Option<string> targetBranchOption, Option<bool> outputJsonOption)
    {
        var prDescriptionCommand = new Command("description", "Pull Request description subcommand");
        prDescriptionCommand.AddOption(targetBranchOption);
        prDescriptionCommand.AddOption(outputJsonOption);
        prDescriptionCommand.SetHandler(async context => await RunPullRequestDescription(context.GetCancellationToken(), this._logger, this.TryGetValueFromOption(context, targetBranchOption) ?? "origin/main", this.TryGetValueFromOption(context, outputJsonOption)));
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

    private static async Task RunPullRequestDescription(CancellationToken token, ILogger logger, string targetBranch = "origin/main", bool outputJson = false)
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

        var contextVariables = new ContextVariables(output);
        contextVariables.Set("outputJson", outputJson.ToString().ToLowerInvariant());
        var kernelResponse = await kernel.RunAsync(output, token, pullRequestSkill["GeneratePR"]);
        logger.LogInformation("{result}", kernelResponse.Result);
    }

    private readonly ILogger _logger;
}
