using System.CommandLine;
using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planners;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using SKonsole.Skills;
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
        var functions = stepKernel.ImportFunctions(new StepwiseSkill(kernel), "stepwise");

        await RunChat(stepKernel, null, functions["RespondTo"]).ConfigureAwait(false);
    }

    private static IKernel LoadOptionSet(string optionSet)
    {
        var kernel = KernelProvider.Instance.Get();

        if (optionSet.Contains("bing"))
        {
            var bingConnector = new BingConnector(Configuration.ConfigVar("BING_API_KEY"));
            var bing = new WebSearchEnginePlugin(bingConnector);
            var search = kernel.ImportFunctions(bing, "bing");
        }

        if (optionSet.Contains("++"))
        {
            kernel.ImportFunctions(new TimePlugin(), "time");
            kernel.ImportFunctions(new ConversationSummaryPlugin(kernel), "summary");
            kernel.ImportFunctions(new SuperFileIOPlugin(), "file");
        }
        else
        {
            if (optionSet.Contains("time"))
            {
                kernel.ImportFunctions(new TimePlugin(), "time");
            }

            if (optionSet.Contains("summary"))
            {
                kernel.ImportFunctions(new ConversationSummaryPlugin(kernel), "summary");
            }

            if (optionSet.Contains("file"))
            {
                kernel.ImportFunctions(new SuperFileIOPlugin(), "file");
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
        public async Task<SKContext?> RespondTo(string message, string history)
        {
            var planner = new StepwisePlanner(this._kernel);

            // Option 1 - Respond to just the message
            // var plan = planner.CreatePlan(message);
            // var messageResult =  await plan.InvokeAsync();

            // Option 2 - Respond to the history
            // var plan = planner.CreatePlan(history);
            // var result = await plan.InvokeAsync();

            // Option 3 - Respond to the history with prompt
            var plan = planner.CreatePlan($"{history}\n---\nGiven the conversation history, respond to the most recent message.");
            var result = await this._kernel.RunAsync(plan);

            // Extract metadata and result string into new SKContext -- Is there a better way?
            var functionResult = result?.FunctionResults?.FirstOrDefault();
            if (functionResult == null)
            {
                return null;
            }

            var context = new SKContext(this._kernel);
            context.Variables.Update(functionResult.GetValue<string>());
            foreach (var key in functionResult.Metadata.Keys)
            {
                context.Variables.Set(key, functionResult.Metadata[key]?.ToString());
            }

            return context;
        }
    }

    private static async Task RunChat(IKernel kernel, ILogger? logger, ISKFunction chatFunction)
    {
        AnsiConsole.MarkupLine("[grey]Press Enter twice to send a message.[/]");
        AnsiConsole.MarkupLine("[grey]Enter 'exit' to exit.[/]");
        var contextVariables = new ContextVariables();

        var history = string.Empty;
        contextVariables.Set("history", history);

        KernelResult botMessage = KernelResult.FromFunctionResults("Hello!", new List<FunctionResult>());

        var userMessage = string.Empty;

        static void HorizontalRule(string title, string style = "white bold")
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule($"[{style}]{title}[/]").RuleStyle("grey").LeftJustified());
            AnsiConsole.WriteLine();
        }

        while (userMessage != "exit")
        {
            var functionResult = botMessage.FunctionResults.FirstOrDefault();
            if (functionResult is not null && functionResult.TryGetMetadataValue("functionCount", out string? functionCount) && functionCount != "0 ()")
            {
                HorizontalRule($"AI - {functionCount}", "green bold");
            }
            else
            {
                HorizontalRule("AI", "green bold");
            }

            AnsiConsole.Foreground = ConsoleColor.Green;
            var message = botMessage.GetValue<string>() ?? string.Empty;
            if (message.Contains("Result not found"))
            {
                if (functionResult is not null && functionResult.TryGetMetadataValue("stepsTaken", out string? stepsTaken))
                {
                    message += $"\n{stepsTaken}";
                }
            }
            AnsiConsole.WriteLine(message);
            AnsiConsole.ResetColors();

            HorizontalRule("User");
            userMessage = ReadMultiLineInput();

            if (userMessage == "exit")
            {
                break;
            }

            history += $"AI: {botMessage.GetValue<string>()}\nHuman: {userMessage} \n";
            contextVariables.Set("history", history);
            contextVariables.Set("message", userMessage);

            var kernelResult = await AnsiConsole.Progress()
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
            botMessage = kernelResult ?? botMessage;
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
