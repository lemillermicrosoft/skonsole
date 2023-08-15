using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SKonsole.Commands;

public class CommitCommand : Command
{
    public CommitCommand(ConfigurationProvider config) : base("commit", "skonsole commit message command")
    {
        this.Add(this.GenerateCommitMessageCommand());
        this.SetHandler(async context => await RunCommitMessage(context.GetCancellationToken()));
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
                    await RunCommitMessage(CancellationToken.None, commitArgumentValue);
                }, commitArgument);
        return createCommand;
    }

    private static async Task RunCommitMessage(CancellationToken token, string commitHash = "")
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
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                }
            };
            process.Start();
            output = process.StandardOutput.ReadToEnd();
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
            output = process.StandardOutput.ReadToEnd();
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
                output = retryProcess.StandardOutput.ReadToEnd();
            }
        }

        var pullRequestSkill = kernel.ImportSkill(new PRSkill.PullRequestSkill(kernel));
        var kernelResponse = await kernel.RunAsync(output, pullRequestSkill["GenerateCommitMessage"]);

        kernel.Logger.LogInformation("Commit Message:\n{result}", kernelResponse.Result);
    }
}
