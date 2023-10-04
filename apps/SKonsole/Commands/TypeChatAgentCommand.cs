using System.CommandLine;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Skills.Core;
using Microsoft.SemanticKernel.Skills.Web;
using Microsoft.SemanticKernel.Skills.Web.Bing;
using Microsoft.TypeChat;
using Microsoft.TypeChat.Schema;
using Microsoft.TypeChat.Dialog;
using SKonsole.Utils;
using Spectre.Console;
using System.Text.Json.Serialization;

namespace SKonsole.Commands;

public class TypeChatAgentCommand : Command
{
    public TypeChatAgentCommand(ConfigurationProvider config, ILogger? logger = null) : base("typechatagent", "skonsole typechat agent command")
    {
        if (logger is null)
        {
            using var loggerFactory = Logging.GetFactory();
            this._logger = loggerFactory.CreateLogger<TypeChatAgentCommand>();
        }
        else
        {
            this._logger = logger;
        }

        var optionSet = new Option<string?>("optionset", "The optionset to use for planning.");
        this.Add(optionSet);
        this.SetHandler(async (optionSetValue) => await Run(CancellationToken.None, this._logger, "", optionSetValue ?? string.Empty), optionSet);

    }

    private static async Task Run(CancellationToken token, ILogger logger, string message = "", string optionSet = "")
    {
        IKernel kernel = LoadOptionSet(optionSet);

        var stepKernel = KernelProvider.Instance.Get();
        var functions = stepKernel.ImportSkill(new TypeChatAgentPlugin(kernel as Kernel), "typechat");

        await RunChat(stepKernel, null, functions["RespondTo"]).ConfigureAwait(false);
    }


    private static string SKonsoleSkillPath()
    {
        const string PARENT = "Skills";
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
            throw new Exception("Skills directory not found. The app needs the skills from the library to work.");
        }

        return path;
    }

    private static IKernel LoadOptionSet(string optionSet)
    {
        var kernel = KernelProvider.Instance.Get();

        // You must at minimum add a RenderResponse function to the kernel for formulating a response.
        var path = SKonsoleSkillPath();
        _ = kernel.ImportSemanticSkillFromDirectory(path, "GenerateResponse");

        // this is the programTranslator.PluginSchema -- why are the inputs missing? probably because i didn't set them.
        // "interface IPluginApi {\n  // Generate a response for a task with given context.\n  GenerateResponse__ForTaskAndContext(): string;\n}\n"


        if (optionSet.Contains("bing"))
        {
            var bingConnector = new BingConnector(Configuration.ConfigVar("BING_API_KEY"));
            var bing = new WebSearchEngineSkill(bingConnector);
            var search = kernel.ImportSkill(bing, "bing");
        }

        if (optionSet.Contains("++"))
        {
            kernel.ImportSkill(new TimeSkill(), "time");
            kernel.ImportSkill(new ConversationSummarySkill(kernel), "summary");
            kernel.ImportSkill(new FileIOSkill(), "file");
        }
        else
        {
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


    public class TypeChatAgentPlugin
    {
        public class ChatResponse
        {
            [Comment("The response from the agent.")]
            public string ResponseMessage { get; set; } = string.Empty;

            [JsonIgnore]
            public bool HasMessage => (!string.IsNullOrEmpty(this.ResponseMessage));
        }

        private AgentWithHistory<Microsoft.TypeChat.Program> _agent;
        private readonly PluginProgramTranslator _programTranslator;
        private readonly IKernel _kernel;


        public TypeChatAgentPlugin(Kernel kernel)
        {
            this._kernel = kernel;

            // Create an agent with history
            _programTranslator = new PluginProgramTranslator(_kernel, Configuration.ConfigVar(ConfigConstants.AZURE_OPENAI_CHAT_DEPLOYMENT_NAME));
            _programTranslator.Translator.MaxRepairAttempts = 2;
            _agent = new AgentWithHistory<Microsoft.TypeChat.Program>(_programTranslator.Translator);
            // Instruct the agent on how it should act
            this.GiveAgentInstructions();
            // We only capture the questions that the model asked us into history
            this._agent.CreateMessageForHistory = (r) =>
            {
                return null;
                // var p = new ProgramInterpreter();
                // var result = p.RunAsync(r, _programTranslator.Api.InvokeAsync).GetAwaiter().GetResult();
                // return Message.FromAssistant(result);
            };
            // Enforce additional constraints validation, as provided by System.ComponentModel.DataAnnotations namespace
            _agent.Translator.ConstraintsValidator = new ConstraintsValidator<Microsoft.TypeChat.Program>();
            //
            // Set up some parameters
            //
            _agent.MaxRequestPromptLength = 2048; // Limit # of characters, to avoid hitting token limits
            _agent.Translator.MaxRepairAttempts = 2;
        }

        [SKFunction, Description("Respond to a message.")]
        public async Task<SKContext?> RespondTo(string message, string history, CancellationToken cancelToken)
        {
            var context = this._kernel.CreateNewContext();

            var response = await this._agent.GetResponseAsync(history, cancelToken);
            var p = new ProgramInterpreter();
            var result = await p.RunAsync(response, _programTranslator.Api.InvokeAsync).ConfigureAwait(false);

            context.Variables.Update(result);

            return context;
        }


        // async Task<string> RunProgram(Microsoft.TypeChat.Program program)
        // {
        //     if (!program.IsComplete)
        //     {
        //         return string.Empty;
        //     }

        //     // https://github.com/microsoft/typechat.net/issues/162
        //     try
        //     {
        //         string result = await _interpreter.RunAsync(program, _programTranslator.Api.InvokeAsync);
        //         // taking the program when its succesful and add it to my kernel or memory
        //         return result;
        //     }
        //     catch (Exception ex)
        //     {

        //         var json = Json.Stringify(program.Source);
        //         return ex.Message;
        //     }
        // }

        void GiveAgentInstructions()
        {
            Prompt instructions = _agent.Instructions;
            // Supply current date and time, and how to use it
            // instructions += PromptLibrary.Now();

            // health bot
            // instructions += "Help me enter my health data step by step.\n" +
            //        "Ask specific questions to gather required and optional fields I have not already provided" +
            //        "Stop asking if I don't know the answer\n" +
            //        "Automatically fix my spelling mistakes\n" +
            //        "My health data may be complex: always record and return ALL of it.\n" +
            //        "Always return a response:\n" +
            //        "- If you don't understand what I say, ask a question.\n" +
            //        "- At least respond with an OK message.";

            // attempt 1 at chat bot
            // instructions += "Given the conversation history, respond to the most recent message.\n---\n";
            // THIS DOESNT WORK :'(

            // attempt 2 at chat bot
            // instead of a health bot, this is just a general purpose chatbot with dynamic plugins
            instructions += "Given the conversation history, respond to the most recent Human message.\n---\n" +
                "If you don't understand what I say, ask a question.\n" +
                "At least respond with an OK message.";
        }

        // public Task ProcessCommandAsync(string cmd, IList<string> args)
        // {
        //     switch (cmd.ToLower())
        //     {
        //         default:
        //             Console.WriteLine($"Unhandled command {cmd}");
        //             break;

        //         case "history":
        //             foreach (var message in _agent.History.All())
        //             {
        //                 Console.WriteLine($"{message.Source}: {message.GetText()}");
        //             }
        //             break;

        //         case "clear":
        //             _agent.History.Clear();
        //             Console.Clear();
        //             PrintHelp();
        //             break;
        //     }
        //     return Task.CompletedTask;
        // }
    }

    /// <summary>
    /// Program translator that translates user requests into programs that call APIs defined by Microsoft Semantic Kernel Plugins
    /// </summary>
    public class PluginProgramTranslator
    {
        IKernel _kernel;
        ProgramTranslator _translator;
        PluginApi _pluginApi;
        SchemaText _pluginSchema;

        /// <summary>
        /// Create a new translator that will produce programs that can call all skills and
        /// plugins registered with the given semantic kernel
        /// </summary>
        /// <param name="kernel"></param>
        /// <param name="model"></param>
        public PluginProgramTranslator(IKernel kernel, ModelInfo model)
        {
            _kernel = kernel;
            _pluginApi = new PluginApi(_kernel);
            _pluginSchema = _pluginApi.TypeInfo.ExportSchema(_pluginApi.TypeName);
            _translator = new ProgramTranslator(
                _kernel.LanguageModel(model),
                new ProgramValidator(new PluginProgramValidator(_pluginApi.TypeInfo)),
                _pluginSchema
            );
        }
        /// <summary>
        /// Translator being used
        /// </summary>
        public ProgramTranslator Translator => _translator;
        /// <summary>
        /// Kernel this translator is working with
        /// </summary>
        public IKernel Kernel => _kernel;
        /// <summary>
        /// The "API" formed by the various plugins registered with the kernel
        /// </summary>
        public PluginApi Api => _pluginApi;
        public SchemaText Schema => _pluginSchema;

        public Task<Microsoft.TypeChat.Program> TranslateAsync(string input, CancellationToken cancelToken)
        {
            return _translator.TranslateAsync(input, cancelToken);
        }
    }

    /// <summary>
    /// Validates programs produced by PluginProgramTranslator.
    /// Ensures that function calls are to existing plugins with matching parameters
    /// </summary>
    public class PluginProgramValidator : ProgramVisitor, IProgramValidator
    {
        PluginApiTypeInfo _typeInfo;

        public PluginProgramValidator(PluginApiTypeInfo typeInfo)
        {
            _typeInfo = typeInfo;
        }

        public Result<Microsoft.TypeChat.Program> ValidateProgram(Microsoft.TypeChat.Program program)
        {
            try
            {
                Visit(program);
                return program;
            }
            catch (Exception ex)
            {
                return Result<Microsoft.TypeChat.Program>.Error(program, ex.Message);
            }
        }

        protected override void VisitFunction(FunctionCall functionCall)
        {
            try
            {
                // Verify function exists
                var name = PluginFunctionName.Parse(functionCall.Name);
                var typeInfo = _typeInfo[name];
                // Verify that parameter counts etc match
                ValidateArgCounts(functionCall, typeInfo, functionCall.Args);
                // Continue visiting to handle any nested function calls
                base.VisitFunction(functionCall);
                return;
            }
            catch (ProgramException)
            {
                throw;
            }
            catch { }
            ProgramException.ThrowFunctionNotFound(functionCall.Name);
        }

        void ValidateArgCounts(FunctionCall call, FunctionView typeInfo, Expression[] args)
        {
            int expectedCount = (typeInfo.Parameters != null) ? typeInfo.Parameters.Count : 0;
            int actualCount = (args != null) ? args.Length : 0;
            if (actualCount != expectedCount)
            {
                // TODO this is the bug with bing sometimes
                ProgramException.ThrowArgCountMismatch(call, expectedCount, actualCount);
            }
        }
    }

    private static async Task RunChat(IKernel kernel, ILogger? logger, ISKFunction chatFunction)
    {
        AnsiConsole.MarkupLine("[grey]Press Enter twice to send a message.[/]");
        AnsiConsole.MarkupLine("[grey]Enter 'exit' to exit.[/]");
        var contextVariables = new ContextVariables();

        var history = string.Empty;
        contextVariables.Set("history", history);

        // KernelResult botMessage = KernelResult.FromFunctionResults("➕➖✖️➗🟰", new List<FunctionResult>());
        var botMessage = kernel.CreateNewContext();
        botMessage.Variables.Update("Hello!");

        var userMessage = string.Empty;

        static void HorizontalRule(string title, string style = "white bold")
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
