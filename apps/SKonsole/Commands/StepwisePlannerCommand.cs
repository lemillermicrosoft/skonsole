using System.CommandLine;
using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Skills.Core;
using Microsoft.SemanticKernel.Skills.Web;
using Microsoft.SemanticKernel.Skills.Web.Bing;
using Microsoft.SemanticKernel.Skills.OpenAPI.Extensions;
using SKonsole.Utils;
using Spectre.Console;
using System.Reflection;
using SKonsole.Skills;
using System.Text.Json;

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

    private static string AIPluginsSkillsPath()
    {
        const string PARENT = "AIPlugins";
        static bool SearchPath(string pathToFind, out string result, int maxAttempts = 10)
        {
            var currDir = Path.GetFullPath(Assembly.GetExecutingAssembly().Location);
            bool found;
            do
            {
                result = Path.Join(currDir, pathToFind);
                found = Directory.Exists(result);
                currDir = Path.GetFullPath(Path.Combine(currDir, ".."));
            } while (maxAttempts-- > 0 && !found);

            return found;
        }

        if (!SearchPath(PARENT, out string path))
        {
            throw new InvalidOperationException("AIPlugins directory not found. The app needs the skills from the library to work.");
        }

        return path;
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

        var LoadSequentialPlanner = () =>
        {
            var planner = new SequentialPlanner(kernel);

            kernel.RegisterCustomFunction(SKFunction.FromNativeFunction((string goal) =>
            {
                var plan = planner.CreatePlanAsync(goal).ConfigureAwait(false).GetAwaiter().GetResult();
                return plan.InvokeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }, "PlanAndExecute", "Run", "Creates a multi-step plan and executes it using all available functions. The plan is created from the goal which should contain relevant parameters."));
        };
        var planJson = "";
        var LoadMeetingSummary = () =>
        {
            var summary = kernel.ImportSkill(new CustomConversationSummarySkill(kernel, 4000), "summary");
            var file = kernel.ImportSkill(new FileIOSkill(), "file");

            // Generate a summary of the meeting transcript 'C:\Users\lemiller\Downloads\SK Product Review_2023-09-07.vtt'
            var plan = new Plan("Meeting Summary - Generate a complete meeting summary for a given transcript file.");

            var fileStep = new Plan(file["Read"]);
            fileStep.Parameters["path"] = "$INPUT";
            fileStep.Outputs.Add("FILE_CONTENTS");

            var summarizeStep = new Plan(summary["SummarizeConversation"]);
            summarizeStep.Parameters["input"] = "$FILE_CONTENTS";
            summarizeStep.Outputs.Add("SUMMARY");

            var actionItemStep = new Plan(summary["GetConversationActionItems"]);
            actionItemStep.Parameters["input"] = "$FILE_CONTENTS";
            actionItemStep.Outputs.Add("ACTION_ITEMS");

            plan.AddSteps(fileStep, summarizeStep, actionItemStep);
            plan.Outputs.Add("SUMMARY");
            plan.Outputs.Add("ACTION_ITEMS");

            plan.Name = "GenerateMeetingSummary";

            kernel.ImportPlan(plan);
            planJson = plan.ToJson();
            Console.WriteLine(plan.ToJson(true));
            Console.WriteLine("::");
            Console.WriteLine(JsonSerializer.Serialize(plan.Describe()));
        };

        var LoadWellKnown = () =>
        {
            // get https://www.wellknown.ai/api/plugins which is {"plugins": [{ai-plugin.json}, ...]}
            // foreach plugin in plugins
            //   get plugin.ai-plugin.json
            //   import plugin.ai-plugin.json

            /// Do the thinking thing but loading
            ///
            AnsiConsole.Progress()
                .AutoClear(true)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new SpinnerColumn(),
                })
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Loading Well Known plugins...[/]", autoStart: true).IsIndeterminate();

                    // var result = await kernel.RunAsync(contextVariables, chatFunction);
                    _ = await kernel.ImportAIPluginsAsync(new Uri("https://www.wellknown.ai/api/plugins"), new OpenApiSkillExecutionParameters(enableDynamicOperationPayload: true)).ConfigureAwait(false);

                    task.StopTask();
                }).ConfigureAwait(false).GetAwaiter().GetResult();

        };

        var LoadKayak = () =>
        {
            // TODO -- This needs AUTH
            var path = AIPluginsSkillsPath();
            _ = kernel
                .ImportAIPluginAsync("kayak", Path.Combine(path, "Kayak", "ai-plugin.json"), new OpenApiSkillExecutionParameters(enableDynamicOperationPayload: false))
                .ConfigureAwait(false).GetAwaiter().GetResult();
        };

        var LoadZillow = () =>
        {
            var path = AIPluginsSkillsPath();
            _ = kernel
                .ImportAIPluginAsync("zillow", Path.Combine(path, "Zillow", "ai-plugin.json"), new OpenApiSkillExecutionParameters(enableDynamicOperationPayload: false))
                .ConfigureAwait(false).GetAwaiter().GetResult();
        };

        if (optionSet.Contains("bing"))
        {
            var bingConnector = new BingConnector(Configuration.ConfigVar("BING_API_KEY"));
            var bing = new WebSearchEngineSkill(bingConnector);
            var search = kernel.ImportSkill(bing, "bing");
        }

        if (optionSet.Contains("plan"))
        {
            // LoadSequentialPlanner();
            LoadMeetingSummary();
        }

        if (optionSet.Contains("++"))
        {
            kernel.ImportSkill(new TimeSkill(), "time");
            kernel.ImportSkill(new ConversationSummarySkill(kernel), "summary");
            kernel.ImportSkill(new FileIOSkill(), "file");
            kernel.ImportSkill(new TextSkill(), "text");

            // https://turingbotdev.blob.core.windows.net/chatgpt-plugins/kayak/api.yaml



            _ = kernel.ImportAIPluginAsync("Klarna", new Uri("https://www.klarna.com/.well-known/ai-plugin.json"), new OpenApiSkillExecutionParameters(enableDynamicOperationPayload: true)).ConfigureAwait(false).GetAwaiter().GetResult();
            // LoadKayak();
            // LoadZillow();
            // LoadWellKnown();
        }
        else
        {
            if (optionSet.Contains("wellknown"))
            {
                LoadWellKnown();
            }

            if (optionSet.Contains("klarna"))
            {
                _ = kernel.ImportAIPluginAsync("Klarna", new Uri("https://www.klarna.com/.well-known/ai-plugin.json"), new OpenApiSkillExecutionParameters(enableDynamicOperationPayload: true)).ConfigureAwait(false).GetAwaiter().GetResult();
            }

            if (optionSet.Contains("zillow"))
            {
                LoadZillow();
            }

            if (optionSet.Contains("kayak"))
            {
                LoadKayak();
            }

            if (optionSet.Contains("time"))
            {
                kernel.ImportSkill(new TimeSkill(), "time");
            }

            if (optionSet.Contains("summary"))
            {
                kernel.ImportSkill(new ConversationSummarySkill(kernel), "summary");
            }

            if (optionSet.Contains("file"))
            {
                kernel.ImportSkill(new FileIOSkill(), "file");
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

            // this._kernel.RegisterCustomFunction(SKFunction.FromNativeFunction((string questionForUser) =>
            // {
            //     // Ask the user a question
            //     // Console.WriteLine(questionForUser);

            //     // Read the response
            //     // var response = Console.ReadLine();

            //     // Return the response
            //     return "42";
            // }, "UserInput", "GetAnswerForQuestion", "Gets the answer for a question from the user directly."));
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
            // var plan2 = planner.CreatePlan($"{history}\n---\nGiven the conversation history, respond to the most recent message.");// When taking steps involving reading files or long string content, call 'PlanAndExecute.Run' instead of the function directly.");
            // var result = await plan2.InvokeAsync();

            if (this._kernel.Skills.TryGetFunction("Plan", "GenerateMeetingSummary", out var plan))
            {
                return await plan.InvokeAsync(@"C:\Users\lemiller\Downloads\SK Product Review_2023-09-07.vtt");
            }


            return this._kernel.CreateNewContext();
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
