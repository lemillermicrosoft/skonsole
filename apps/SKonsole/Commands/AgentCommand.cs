using System.CommandLine;
using Microsoft.Extensions.Logging;
using SKonsole.Utils;

namespace SKonsole.Commands;

public class AgentCommand : Command
{
    public AgentCommand(ConfigurationProvider config, ILogger? logger = null) : base("agent", "skonsole agent command")
    {
        if (logger is null)
        {
            using var loggerFactory = Logging.GetFactory();
            this._logger = loggerFactory.CreateLogger<AgentCommand>();
        }
        else
        {
            this._logger = logger;
        }

        this.SetHandler(async (optionSetValue) => await Run(CancellationToken.None, this._logger, ""));
    }

    private static async Task Run(CancellationToken token, ILogger logger, string message = "")
    {
        var assistantKernel = KernelProvider.Instance.Get();
        var stepwiseKernel = StepwisePlannerCommand.LoadOptionSet("bing++");
        var userKernel = KernelProvider.Instance.Get();

        // Depends on the userProxy being able to *do* something.
        var assistant = new SKonsole.Agents.AssistantAgent(
            name: "assistant",
            kernel: assistantKernel);

        var stepwiseAssistant = new SKonsole.Agents.StepwiseAgent(
            name: "stepwise_assistant",
            kernel: stepwiseKernel);

        var userProxy = new SKonsole.Agents.UserProxyAgent(
            name: "user_proxy",
            kernel: userKernel,
            humanInputMode: "ALWAYS",// "NEVER",
            maxConsecutiveAutoReply: 10,
            isTerminationMsg: (msg) => msg.TrimEnd().EndsWith("TERMINATE"),
            codeExecutionConfig: new Dictionary<string, object>
            {
                { "work_dir", "coding" },
                { "use_docker", false } // set to True or image name like "python:3" to use docker
            });

        await userProxy.StartChatAsync(stepwiseAssistant, context: new Dictionary<string, object>() { { "message", "what's the current Microsoft stock price?" } });
    }

    private readonly ILogger _logger;
}
