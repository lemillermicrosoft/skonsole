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

        var outputFormatOption = new Option<string>(
                            new string[] { "--outputFormat", "-o" },
                            () => { return ""; },
                            "Output the result in a specified format. Supported formats are json, markdown, and text. Omit to output in plain text.");
        this.AddOption(outputFormatOption);

        var outputFileOption = new Option<string>(
                            new string[] { "--outputFile", "-f" },
                            () => { return ""; },
                            "Output the result to the specified file.");
        this.AddOption(outputFileOption);

        var diffInputFileOption = new Option<string>(
                            new string[] { "--diffInputFile", "-d" },
                            () => { return ""; },
                            "Use the specified file as the diff. Can be a file or URL.");
        this.AddOption(diffInputFileOption);

        this.Add(this.GeneratePRFeedbackCommand(targetBranchOption));
        this.Add(this.GeneratePRDescriptionCommand(targetBranchOption, outputFormatOption, outputFileOption, diffInputFileOption));

        this.SetHandler(async context => await RunPullRequestDescription(
            context.GetCancellationToken(),
            this._logger,
            this.TryGetValueFromOption(context, targetBranchOption) ?? "origin/main",
            this.TryGetValueFromOption(context, outputFormatOption) ?? "",
            this.TryGetValueFromOption(context, outputFileOption) ?? "",
            this.TryGetValueFromOption(context, diffInputFileOption) ?? ""));
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

    private Command GeneratePRDescriptionCommand(Option<string> targetBranchOption, Option<string> outputFormatOption, Option<string> outputFileOption, Option<string> diffInputFileOption)
    {
        var prDescriptionCommand = new Command("description", "Pull Request description subcommand");
        prDescriptionCommand.AddOption(targetBranchOption);
        prDescriptionCommand.AddOption(outputFormatOption);
        prDescriptionCommand.AddOption(outputFileOption);
        prDescriptionCommand.AddOption(diffInputFileOption);
        prDescriptionCommand.SetHandler(async context => await RunPullRequestDescription(
            context.GetCancellationToken(),
            this._logger,
            this.TryGetValueFromOption(context, targetBranchOption) ?? "origin/main",
            this.TryGetValueFromOption(context, outputFormatOption) ?? "",
            this.TryGetValueFromOption(context, outputFileOption) ?? "",
            this.TryGetValueFromOption(context, diffInputFileOption) ?? ""));
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

    private static async Task RunPullRequestDescription(CancellationToken token, ILogger logger, string targetBranch = "origin/main", string outputFormat = "", string outputFile = "", string diffInputFile = "")
    {
        var kernel = KernelProvider.Instance.Get();

        var output = await FetchDiff(targetBranch, diffInputFile);

        var pullRequestSkill = kernel.ImportSkill(new PRSkill.PullRequestSkill(kernel));

        var contextVariables = new ContextVariables(output);
        contextVariables.Set("outputFormatInstructions", PRSkill.Utils.FormatInstructionsProvider.GetOutputFormatInstructions(outputFormat));

        var kernelResponse = await kernel.RunAsync(contextVariables, token, pullRequestSkill["GeneratePR"]);
        logger.LogInformation("Pull Request Description:\n{result}", kernelResponse.Result);

        if (!string.IsNullOrEmpty(outputFile))
        {
            var directory = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            System.IO.File.WriteAllText(outputFile, kernelResponse.Result);
        }
    }

    private static async Task<string> FetchDiff(string targetBranch, string diffInputFile)
    {
        if (string.IsNullOrEmpty(diffInputFile))
        {
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

            return process.StandardOutput.ReadToEnd();
        }
        else if (diffInputFile.StartsWith("http"))
        {
            using var client = new HttpClient();
            return await client.GetStringAsync(diffInputFile);
        }
        else
        {
            return await System.IO.File.ReadAllTextAsync(diffInputFile);
        }
    }

    private readonly ILogger _logger;
}
