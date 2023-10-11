using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SKonsole.Utils;
using Spectre.Console;
using TextCopy;

namespace SKonsole.Commands;

public class CommitCommand : Command
{
    public CommitCommand(ConfigurationProvider config, ILogger? logger = null) : base("commit", "skonsole commit message command")
    {
        if (logger is null)
        {
            using var loggerFactory = Logging.GetFactory();
            this._logger = loggerFactory.CreateLogger<CommitCommand>();
        }
        else
        {
            this._logger = logger;
        }

        this.Add(this.GenerateCommitMessageCommand());
        this.SetHandler(async context => await RunCommitMessage(context.GetCancellationToken(), this._logger));
    }

    private Command GenerateCommitMessageCommand()
    {
        var commitArgument = new Argument<string>
            ("commitHash", () =>
            {
                return string.Empty;
            }, "commit hash argument that is parsed as a string.");
        var createCommand = new Command("create", "create commit message");
        createCommand.AddArgument(commitArgument);
        createCommand.SetHandler(async (commitArgumentValue) =>
                {
                    await RunCommitMessage(CancellationToken.None, this._logger, commitArgumentValue);
                }, commitArgument);
        return createCommand;
    }

    private static async Task RunCommitMessage(CancellationToken token, ILogger logger, string commitHash = "")
    {
        var kernel = KernelProvider.Instance.Get();

        string output = string.Empty;
        if (!string.IsNullOrEmpty(commitHash))
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"show {commitHash}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                }
            };
            process.Start();
            output = await process.StandardOutput.ReadToEndAsync();
        }
        else
        {
            using var process = new Process
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
            output = await process.StandardOutput.ReadToEndAsync();
            if (string.IsNullOrEmpty(output))
            {
                using var retryProcess = new Process
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
                retryProcess.Start();
                output = await retryProcess.StandardOutput.ReadToEndAsync();
            }
        }

        var pullRequestPlugin = kernel.ImportFunctions(new PRPlugin.PullRequestPlugin(kernel));

        static void HorizontalRule(string title, string style = "white bold")
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule($"[{style}]{title}[/]").RuleStyle("grey").LeftJustified());
            AnsiConsole.WriteLine();
        }

        HorizontalRule("Commit Message");
        var botMessage = await AnsiConsole.Progress()
            .AutoClear(true)
            .Columns(new ProgressColumn[]
            {
                            new TaskDescriptionColumn(),
                            new SpinnerColumn(),
            })
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Thinking...[/]", autoStart: true).IsIndeterminate();
                var kernelResponse = await kernel.RunAsync(output, token, pullRequestPlugin["GenerateCommitMessage"]);
                task.StopTask();

                var result = kernelResponse.GetValue<string>() ?? string.Empty;
                await ClipboardService.SetTextAsync(result);
                return result;
            });

        AnsiConsole.WriteLine(botMessage);
        HorizontalRule(string.Empty);
    }

    private readonly ILogger _logger;
}
