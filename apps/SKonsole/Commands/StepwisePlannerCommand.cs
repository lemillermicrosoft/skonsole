using System.CommandLine;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planners;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using SKonsole.Plugins;
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
            this._logger = loggerFactory.CreateLogger(this.GetType());
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
        (IKernel kernel, var validPlugins) = LoadOptionSet(optionSet);

        var stepKernel = KernelProvider.Instance.Get();
        var functions = stepKernel.ImportFunctions(new StepwisePlugin(kernel), "stepwise");

        await RunChat(stepKernel, validPlugins, null, functions["RespondTo"]).ConfigureAwait(false);
    }

    private static (IKernel, List<string>) LoadOptionSet(string optionSet)
    {
        var kernel = KernelProvider.Instance.Get();
        List<string> validPlugins = new();

        if (optionSet.Contains("bing"))
        {
            var bingConnector = new BingConnector(Configuration.ConfigVar("BING_API_KEY"));
            var bing = new WebSearchEnginePlugin(bingConnector);
            var search = kernel.ImportFunctions(bing, "bing");
            validPlugins.Add("bing");
        }

        if (optionSet.Contains("++"))
        {
            kernel.ImportFunctions(new TimePlugin(), "time");
            validPlugins.Add("time");
            kernel.ImportFunctions(new ConversationSummaryPlugin(kernel), "summary");
            validPlugins.Add("summary");
            kernel.ImportFunctions(new SuperFileIOPlugin(), "file");
            validPlugins.Add("file");
        }
        else
        {
            if (optionSet.Contains("time"))
            {
                kernel.ImportFunctions(new TimePlugin(), "time");
                validPlugins.Add("time");
            }

            if (optionSet.Contains("summary"))
            {
                kernel.ImportFunctions(new ConversationSummaryPlugin(kernel), "summary");
                validPlugins.Add("summary");
            }

            if (optionSet.Contains("file"))
            {
                kernel.ImportFunctions(new SuperFileIOPlugin(), "file");
                validPlugins.Add("file");
            }

            if (optionSet.Contains("git"))
            {
                var gitPlugin = kernel.ImportFunctions(new GitPlugin(kernel), "git");

                Plan gitProcessPlan = new("Execute a 'git diff' command and execute semantic reasoning over the output.");
                gitProcessPlan.Name = "GenerateDynamicGitDiffResult";

                // It'd be nice if I could even just use this to set default values. An option -- doesn't work right now.
                // gitProcessPlan.Parameters.Set("filter", "-- . \":!*.md\" \":!*skprompt.txt\" \":!*encoder.json\" \":!*vocab.bpe\" \":!*dict.txt\"");
                // gitProcessPlan.Parameters.Set("target", "HEAD");
                // gitProcessPlan.Parameters.Set("source", "4b825dc642cb6eb9a060e54bf8d69288fbee4904");
                // gitProcessPlan.Parameters.Set("instructions", "");
                gitProcessPlan.Parameters.Set("filter", "");
                gitProcessPlan.Parameters.Set("target", "");
                gitProcessPlan.Parameters.Set("source", "");
                gitProcessPlan.Parameters.Set("instructions", "");

                gitProcessPlan.AddSteps(gitPlugin["GitDiffDynamic"]);
                gitProcessPlan.Steps[0].Outputs.Add("gitDiffResult");

                gitProcessPlan.AddSteps(gitPlugin["GenerateDynamic"]);

                // This is the only way to connect to parameters. It's not great.
                // Since they are optional, if nothing is supplied `Plan` will not replace the parameter. Should file an issue.
                gitProcessPlan.Steps[0].Parameters.Set("filter", "$filter");
                gitProcessPlan.Steps[0].Parameters.Set("target", "$target");
                gitProcessPlan.Steps[0].Parameters.Set("source", "$source");
                gitProcessPlan.Steps[1].Parameters.Set("fullDiff", "$gitDiffResult");
                gitProcessPlan.Steps[1].Parameters.Set("instructions", "$instructions");

                var p = gitProcessPlan.Describe();
                kernel.ImportPlan(gitProcessPlan);
                validPlugins.Add("Plan");
            }
        }

        return (kernel, validPlugins);
    }

    public class StepwisePlugin
    {
        private readonly IKernel _kernel;
        public StepwisePlugin(IKernel kernel)
        {
            this._kernel = kernel;
        }

        [SKFunction, Description("Respond to a message.")]
        public async Task<SKContext?> RespondTo(string message, string history, string validPlugins, CancellationToken cancellationToken = default)
        {
            var config = new StepwisePlannerConfig();
            var plugins = JsonSerializer.Deserialize<List<string>>(validPlugins);

            config.GetAvailableFunctionsAsync = (plannerConfig, filter, token) =>
            {
                var functions = this._kernel.Functions;
                var functionViews = functions.GetFunctionViews();

                var filteredViews = functionViews
                    .Where(f => plugins is null || plugins.Contains(f.PluginName))
                    .OrderBy(f => $"{f.PluginName}.{f.Name}");

                return Task.FromResult(filteredViews);
            };
            var planner = new StepwisePlanner(this._kernel, config);

            // Option 1 - Respond to just the message
            // var plan = planner.CreatePlan(message);
            // var messageResult =  await plan.InvokeAsync();

            // Option 2 - Respond to the history
            // var plan = planner.CreatePlan(history);
            // var result = await plan.InvokeAsync();

            // Option 3 - Respond to the history with prompt
            var plan = planner.CreatePlan($"{history}\n---\nGiven the conversation history, respond to the most recent message.");
            var result = await this._kernel.RunAsync(plan, cancellationToken: cancellationToken);

            // Extract metadata and result string into new SKContext -- Is there a better way?
            var functionResult = result?.FunctionResults?.FirstOrDefault();
            if (functionResult == null)
            {
                return null;
            }

            var context = this._kernel.CreateNewContext();
            context.Variables.Update(functionResult.GetValue<string>());
            foreach (var key in functionResult.Metadata.Keys)
            {
                context.Variables.Set(key, functionResult.Metadata[key]?.ToString());
            }

            return context;
        }
    }

    private static async Task RunChat(IKernel kernel, IList<string> validPlugins, ILogger? logger, ISKFunction chatFunction, CancellationToken cancellationToken = default)
    {
        AnsiConsole.MarkupLine("[grey]Press Enter twice to send a message.[/]");
        AnsiConsole.MarkupLine("[grey]Enter 'exit' to exit.[/]");
        var contextVariables = new ContextVariables();

        var history = string.Empty;
        contextVariables.Set("history", history);
        contextVariables.Set("validPlugins", JsonSerializer.Serialize(validPlugins));

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
