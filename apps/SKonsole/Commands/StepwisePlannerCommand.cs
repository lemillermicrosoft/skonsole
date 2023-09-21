using System.CommandLine;
using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using SKonsole.Utils;
using Spectre.Console;

namespace SKonsole.Commands;

public class StepwisePlannerCommand : Command
{
    public StepwisePlannerCommand(ConfigurationProvider config, ILogger? logger = null) : base("stepwise", "skonsole stepwise planning command")
    {
        if (logger is null)
        {
            using var loggerFactory = Logging.GetFactory();
            this._logger = loggerFactory.CreateLogger<StepwisePlannerCommand>();
        }
        else
        {
            this._logger = logger;
        }

        var optionSet = new Option<string?>("optionset", "The optionset to use for planning.");
        this.Add(optionSet);
        this.SetHandler(async (optionSetValue) => await RunCreatePlan(CancellationToken.None, this._logger, "", optionSetValue ?? string.Empty), optionSet);
    }

    private static async Task RunCreatePlan(CancellationToken token, ILogger logger, string message = "", string optionSet = "")
    {
        IKernel kernel = LoadOptionSet(optionSet);

        var stepKernel = KernelProvider.Instance.Get();
        var functions = stepKernel.ImportSkill(new StepwiseSkill(kernel), "stepwise");

        await RunChat(stepKernel, null, functions["RespondTo"]).ConfigureAwait(false);
    }

    private static IKernel LoadOptionSet(string optionSet)
    {
        var kernel = KernelProvider.Instance.Get();

        if (optionSet.Contains("bing"))
        {
            var bingConnector = new BingConnector(Configuration.ConfigVar("BING_API_KEY"));
            var bing = new WebSearchEnginePlugin(bingConnector);
            var search = kernel.ImportSkill(bing, "bing");
        }

        if (optionSet.Contains("++"))
        {
            kernel.ImportSkill(new TimePlugin(), "time");
            kernel.ImportSkill(new ConversationSummaryPlugin(kernel), "summary");
            kernel.ImportSkill(new FileIOPlugin(), "file");
        }
        else
        {
            if (optionSet.Contains("time"))
            {
                kernel.ImportSkill(new TimePlugin(), "time");
            }

            if (optionSet.Contains("summary"))
            {
                kernel.ImportSkill(new ConversationSummaryPlugin(kernel), "summary");
            }

            if (optionSet.Contains("file"))
            {
                kernel.ImportSkill(new FileIOPlugin(), "file");
            }
        }

        return kernel;
    }

    public class StepwiseSkill
    {
        private readonly IKernel _kernel;
        public StepwiseSkill(IKernel kernel)
        {
            this._kernel = kernel;
        }

        [SKFunction, Description("Respond to a message.")]
        public async Task<SKContext> RespondTo(string message, string history)
        {
            var planner = new StepwisePlanner(this._kernel);

            // Option 1 - Respond to just the message
            // var plan = planner.CreatePlan(message);
            // var messageResult =  await plan.InvokeAsync();

            // Option 2 - Respond to the history
            // var plan = planner.CreatePlan(history);
            // var result = await plan.InvokeAsync();

            // Option 3 - Respond to the history with prompt
            var plan2 = planner.CreatePlan($"{history}\n---\nGiven the conversation history, respond to the most recent message.");
            var result = await this._kernel.RunAsync(plan2);

            return result;
        }
    }

    private static async Task RunChat(IKernel kernel, ILogger? logger, ISKFunction chatFunction)
    {
        AnsiConsole.MarkupLine("[grey]Press Enter twice to send a message.[/]");
        AnsiConsole.MarkupLine("[grey]Enter 'exit' to exit.[/]");
        var contextVariables = new ContextVariables();

        var history = string.Empty;
        contextVariables.Set("history", history);

        var botMessage = kernel.CreateNewContext();
        botMessage.Variables.Update("Hello!");
        //var botMessage = await kernel.RunAsync(contextVariables, chatFunction);

        var userMessage = string.Empty;

        void HorizontalRule(string title, string style = "white bold")
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule($"[{style}]{title}[/]").RuleStyle("grey").LeftJustified());
            AnsiConsole.WriteLine();
        }

        while (userMessage != "exit")
        {
            if (botMessage.Variables.TryGetValue("skillCount", out string? skillCount) && skillCount != "0 ()")
            {
                HorizontalRule($"AI - {skillCount}", "green bold");
            }
            else
            {
                HorizontalRule("AI", "green bold");
            }

            AnsiConsole.Foreground = ConsoleColor.Green;
            AnsiConsole.WriteLine(botMessage.ToString());
            AnsiConsole.ResetColors();

            HorizontalRule("User");
            userMessage = ReadMultiLineInput();

            if (userMessage == "exit")
            {
                break;
            }

            history += $"AI: {botMessage}\nHuman: {userMessage} \n";
            contextVariables.Set("history", history);
            contextVariables.Set("message", userMessage);

            botMessage = await AnsiConsole.Progress()
                .AutoClear(true)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new SpinnerColumn(),
                })
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Thinking...[/]", autoStart: true).IsIndeterminate();

                    var result = await kernel.RunAsync(contextVariables, chatFunction);

                    task.StopTask();
                    return result;
                });
        }
    }

    private static string ReadMultiLineInput()
    {
        var input = new StringBuilder();
        var line = string.Empty;

        while ((line = Console.ReadLine()) != string.Empty)
        {
            input.AppendLine(line);
        }

        return input.ToString().Trim();
    }

    private readonly ILogger _logger;
}
