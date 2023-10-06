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
        var userKernel = KernelProvider.Instance.Get();
        // var functions = stepKernel.ImportFunctions(new StepwiseSkill(kernel), "stepwise");

        // await RunChat(stepKernel, null, functions["RespondTo"]).ConfigureAwait(false);

        // # create an AssistantAgent named "assistant"
        //         assistant = autogen.AssistantAgent(
        //             name = "assistant",
        //             llm_config ={
        //             "seed": 42,  # seed for caching and reproducibility
        //         "config_list": config_list,  # a list of OpenAI API configurations
        //         "temperature": 0,  # temperature for sampling
        //     },  # configuration for autogen's enhanced inference API which is compatible with OpenAI API
        // )
        // # create a UserProxyAgent instance named "user_proxy"
        // user_proxy = autogen.UserProxyAgent(
        //     name = "user_proxy",
        //     human_input_mode = "NEVER",
        //     max_consecutive_auto_reply = 10,
        //     is_termination_msg = lambda x: x.get("content", "").rstrip().endswith("TERMINATE"),
        //     code_execution_config ={
        //             "work_dir": "coding",
        //         "use_docker": False,  # set to True or image name like "python:3" to use docker
        //     },
        // )
        // # the assistant receives a message from the user_proxy, which contains the task description
        // user_proxy.initiate_chat(
        //     assistant,
        //     message = """What date is today? Compare the year-to-date gain for META and TESLA.""",
        // )

        var assistant = new SKonsole.Agents.AssistantAgent(
            name: "assistant",
            kernel: assistantKernel);

        var userProxy = new SKonsole.Agents.UserProxyAgent(
            name: "user_proxy",
            kernel: userKernel,
            humanInputMode: "NEVER",
            maxConsecutiveAutoReply: 10,
            isTerminationMsg: (msg) => msg.TrimEnd().EndsWith("TERMINATE"),
            codeExecutionConfig: new Dictionary<string, object>
            {
                { "work_dir", "coding" },
                { "use_docker", false } // set to True or image name like "python:3" to use docker
            });

        await userProxy.StartChatAsync(assistant, context: new Dictionary<string, object>() { { "message", "What date is today? Compare the year-to-date gain for META and TESLA." } });
    }

    private readonly ILogger _logger;
}
