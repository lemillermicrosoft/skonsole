using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;
using Spectre.Console;

namespace SKonsole.Agents;
public class DefaultDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly Func<TValue> _defaultValueProvider;

    public DefaultDictionary(Func<TValue> defaultValueProvider)
    {
        this._defaultValueProvider = defaultValueProvider ?? throw new ArgumentNullException(nameof(defaultValueProvider));
    }

    public new TValue this[TKey key]
    {
        get
        {
            if (!this.ContainsKey(key))
            {
                this[key] = this._defaultValueProvider();
            }
            return base[key];
        }
        set
        {
            base[key] = value;
        }
    }
}

public class ConversableAgent : Agent
{
    public const int MAX_CONSECUTIVE_AUTO_REPLY = 100;  // maximum number of consecutive auto replies (subject to future change)
    public static readonly Dictionary<string, object> DEFAULT_CONFIG = new()
    {
        { "model", "gpt-35-turbo" }  // Assuming DEFAULT_MODEL is a constant or variable with the default model value.
    };

    // private Dictionary<string, List<Dictionary<string, object>>> _oaiMessages;
    protected DefaultDictionary<string, ChatHistory> _chatHistories;
    private string _systemMessage;
    private Func<string, bool> _isTerminationMsg;
    private DefaultDictionary<string, int> _consecutiveAutoReplyCounter;
    private DefaultDictionary<string, int> _maxConsecutiveAutoReplyDict;
    private Dictionary<string, object> _codeExecutionConfig;
    private string _humanInputMode;
    private Dictionary<string, object> _defaultAutoReply;
    private List<ReplyFuncConfig> _replyFuncList;

    private DefaultDictionary<Agent, bool> _replyAtReceive;

    private IKernel? _kernel;

    public ConversableAgent(
        string name,
        string systemMessage = "You are a helpful AI Assistant.",
        Func<string, bool>? isTerminationMsg = null,
        int? maxConsecutiveAutoReply = null,
        string humanInputMode = "TERMINATE",
        Dictionary<string, object>? codeExecutionConfig = null,
        IKernel? kernel = null,
        // function_map: Optional[Dict[str, Callable]] = None,  // This is how you advertise functions you can execute/handle
        object? defaultAutoReply = null)
        : base(name)
    {
        this._systemMessage = systemMessage;

        this._chatHistories = new DefaultDictionary<string, ChatHistory>(() =>
        {
            var history = new ChatHistory();
            history.AddSystemMessage(systemMessage);
            return history;
        });

        this._replyAtReceive = new DefaultDictionary<Agent, bool>(() => false);

        this._kernel = kernel;

        this._isTerminationMsg = isTerminationMsg ?? (msg => msg == "TERMINATE");
        this._codeExecutionConfig = codeExecutionConfig ?? new Dictionary<string, object>();
        this._humanInputMode = humanInputMode;
        this._defaultAutoReply = defaultAutoReply as Dictionary<string, object> ?? new Dictionary<string, object>();
        this._replyFuncList = new List<ReplyFuncConfig>();

        if (maxConsecutiveAutoReply.HasValue)
        {
            this._maxConsecutiveAutoReplyDict = new DefaultDictionary<string, int>(() => maxConsecutiveAutoReply.Value) { { name, maxConsecutiveAutoReply.Value } };
        }
        else
        {
            this._maxConsecutiveAutoReplyDict = new DefaultDictionary<string, int>(() => MAX_CONSECUTIVE_AUTO_REPLY) { { name, MAX_CONSECUTIVE_AUTO_REPLY } };
        }

        this._consecutiveAutoReplyCounter = new DefaultDictionary<string, int>(() => 0) { { name, 0 } };

        this.RegisterReply(new List<object?> { typeof(Agent), null }, this.GenerateCompletionAsync);
        // this.RegisterReply(new List<object?> { typeof(Agent), null }, this.GenerateCodeExecutionReplyAsync);
        // this.RegisterReply(new List<object?> { typeof(Agent), null }, this.GenerateFunctionCallReplyAsync);
        this.RegisterReply(new List<object?> { typeof(Agent), null }, this.CheckTerminationAndHumanReply);
    }

    private async Task<(bool final, object? reply)> GenerateCompletionAsync(ChatHistory messages, Agent sender, object config)
    {
        if (this._kernel == null)
        {
            return (false, null);
        }

        if (messages == null)
        {
            messages = this._chatHistories[sender.Name];
        }

        var service = this._kernel.GetService<IChatCompletion>();
        if (service == null)
        {
            return (false, null);
        }

        // TODO: Handle token limit exceeded error - Implement error handling here

        var result = await service.GenerateMessageAsync(messages);

        // TODO: handle function calls
        // var extractedTextOrFunctionCall = oai.ChatCompletion.ExtractTextOrFunctionCall(response);

        return (true, result);
    }

    protected virtual string GetHumanInput(string prompt)
    {
        return new TextPrompt<string>(prompt).AllowEmpty().Show(AnsiConsole.Console);
    }

    private Task<(bool final, object? reply)> CheckTerminationAndHumanReply(ChatHistory messages, Agent sender, object config)
    {
        (bool, string? reply) func()
        {
            if (config == null)
            {
                config = this;
            }

            if (messages == null)
            {
                messages = this._chatHistories[sender.Name];
            }

            var message = messages[messages.Count - 1];
            var reply = "";
            var noHumanInputMsg = "";

            if (this._humanInputMode == "ALWAYS")
            {
                reply = this.GetHumanInput($"Provide feedback to {sender.Name}. Press enter to skip and use auto-reply, or type 'exit' to end the conversation: ");
                noHumanInputMsg = !string.IsNullOrEmpty(reply) ? "" : "NO HUMAN INPUT RECEIVED.";

                // If the human input is empty, and the message is a termination message, then we will terminate the conversation
                reply = !string.IsNullOrEmpty(reply) || !this._isTerminationMsg(message.Content) ? reply : "exit";
            }
            else
            {
                if (this._consecutiveAutoReplyCounter[sender.Name] >= this._maxConsecutiveAutoReplyDict[sender.Name])
                {
                    if (this._humanInputMode == "NEVER")
                    {
                        reply = "exit";
                    }
                    else
                    {
                        // this._humanInputMode == "TERMINATE":
                        var terminate = this._isTerminationMsg(message.Content);
                        reply = this.GetHumanInput(
                            terminate
                                ? $"Please give feedback to {sender.Name}. Press enter or type 'exit' to stop the conversation: "
                                : $"Please give feedback to {sender.Name}. Press enter to skip and use auto-reply, or type 'exit' to stop the conversation: "
                        );
                        noHumanInputMsg = !string.IsNullOrEmpty(reply) ? "" : "NO HUMAN INPUT RECEIVED.";

                        // If the human input is empty, and the message is a termination message, then we will terminate the conversation
                        reply = !string.IsNullOrEmpty(reply) || terminate ? reply : "exit";
                    }
                }
                else if (this._isTerminationMsg(message.Content))
                {
                    if (this._humanInputMode == "NEVER")
                    {
                        reply = "exit";
                    }
                    else
                    {
                        // this._humanInputMode == "TERMINATE":
                        reply = this.GetHumanInput(
                            $"Please give feedback to {sender.Name}. Press enter or type 'exit' to stop the conversation: "
                        );
                        noHumanInputMsg = !string.IsNullOrEmpty(reply) ? "" : "NO HUMAN INPUT RECEIVED.";

                        // If the human input is empty, and the message is a termination message, then we will terminate the conversation
                        reply = !string.IsNullOrEmpty(reply) ? reply : "exit";
                    }
                }
            }

            // Print the noHumanInputMsg
            if (!string.IsNullOrEmpty(noHumanInputMsg))
            {
                Console.WriteLine($"\n>>>>>>>> {noHumanInputMsg}");
            }

            // Stop the conversation
            if (reply == "exit")
            {
                // Reset the consecutiveAutoReplyCounter
                this._consecutiveAutoReplyCounter[sender.Name] = 0;
                return (true, null);
            }

            // Send the human reply
            if (!string.IsNullOrEmpty(reply) || this._maxConsecutiveAutoReplyDict[sender.Name] == 0)
            {
                // Reset the consecutiveAutoReplyCounter
                this._consecutiveAutoReplyCounter[sender.Name] = 0;
                return (true, reply);
            }

            // Increment the consecutiveAutoReplyCounter
            this._consecutiveAutoReplyCounter[sender.Name]++;
            if (this._humanInputMode != "NEVER")
            {
                Console.WriteLine("\n>>>>>>>> USING AUTO REPLY...");
            }

            return (false, null);
        }

        return Task.FromResult<(bool final, object? reply)>(func());
    }

    public void StartChat(
    ConversableAgent recipient,
    bool clearHistory = true,
    bool silent = false,
    Dictionary<string, object>? context = null)
    {
        this._PrepareChat(recipient, clearHistory);
        this.Send(this.GenerateInitMessage(context), recipient, silent);
    }

    public async Task StartChatAsync(
        ConversableAgent recipient,
        bool clearHistory = true,
        bool silent = false,
        Dictionary<string, object>? context = null)
    {
        this._PrepareChat(recipient, clearHistory);
        await this.SendAsync(this.GenerateInitMessage(context), recipient, silent: silent);
    }

    public object GenerateInitMessage(Dictionary<string, object> context)
    {
        if (context.ContainsKey("message"))
        {
            return context["message"];
        }

        throw new ArgumentException("The 'message' key must be provided in the context when GenerateInitMessage is not overridden.");
    }

    private void _PrepareChat(ConversableAgent recipient, bool clearHistory)
    {
        if (clearHistory)
        {
            this._chatHistories[recipient.Name].Clear();
        }

        this._consecutiveAutoReplyCounter[recipient.Name] = 0;
    }

    public void RegisterReply(
        object trigger,
        Func<ChatHistory, Agent, object, object?> replyFunc,
        int position = 0,
        object? config = null,
        Func<object, object>? resetConfig = null)
    {
        var replyFunction = new ReplyFuncConfig
        {
            Trigger = trigger,
            ReplyFunc = replyFunc,
            Position = position,
            Config = config,
            ResetConfig = resetConfig
        };

        if (position < 0)
        {
            this._replyFuncList.Add(replyFunction);
        }
        else
        {
            this._replyFuncList.Insert(position, replyFunction);
        }
    }

    public string SystemMessage
    {
        get { return this._chatHistories[this.Name][0].Content; }
    }

    public void UpdateSystemMessage(string systemMessage)
    {
        this._chatHistories[this.Name][0].Content = systemMessage;
    }

    public void UpdateMaxConsecutiveAutoReply(int value, Agent? sender = null)
    {
        if (sender == null)
        {
            foreach (var key in this._maxConsecutiveAutoReplyDict.Keys.ToList())
            {
                this._maxConsecutiveAutoReplyDict[key] = value;
            }
        }
        else
        {
            this._maxConsecutiveAutoReplyDict[sender.Name] = value;
        }
    }

    public int MaxConsecutiveAutoReply(Agent? sender = null)
    {
        return sender == null ? this._maxConsecutiveAutoReplyDict[this.Name] : this._maxConsecutiveAutoReplyDict[sender.Name];
    }

    public override void Send(object message, Agent recipient, bool? requestReply = null)
    {
        throw new NotImplementedException();
    }

    public override Task SendAsync(object message, Agent recipient, bool? requestReply = null, bool? silent = false)
    {
        // When the agent composes and sends the message, the role of the message is "assistant"
        // unless it's "function".
        var valid = this._AppendOaiMessage(message, "assistant", recipient);

        if (valid)
        {
            return recipient.ReceiveAsync(message, this, requestReply, false);
        }

        throw new ArgumentException("Message can't be converted into a valid ChatCompletion message. Either content or function_call must be provided.");
    }

    private bool _AppendOaiMessage(object message, string role, Agent conversationId)
    {
        var messageDict = _MessageToDict(message);

        // var aiMessage = new SKChatMessage(); // Wish I could use this but I can't
        var aiMessage = new Azure.AI.OpenAI.ChatMessage();

        foreach (var key in new string[] { "content", "function_call", "name", "context" })
        {
            if (messageDict.ContainsKey(key))
            {
                switch (key)
                {
                    case "content":
                        aiMessage.Content = messageDict[key].ToString();
                        break;
                    case "function_call":
                        aiMessage.FunctionCall.Name = messageDict[key].ToString(); // todo what about args?
                        break;
                    case "name":
                        aiMessage.Name = messageDict[key].ToString();
                        break;
                        // case "context":
                        //     aiMessage.AzureExtensionsContext = messageDict[key].ToString();
                        //     break;
                }
            }
        }

        if (string.IsNullOrEmpty(aiMessage.Content))
        {
            if (aiMessage.FunctionCall is not null)
            {
                aiMessage.Content = null;
            }
            else
            {
                return false;
            }
        }

        aiMessage.Role = (messageDict.ContainsKey("role") && messageDict["role"].ToString() == "function") ? "function" : role;
        if (aiMessage.FunctionCall is not null)
        {
            aiMessage.Role = Azure.AI.OpenAI.ChatRole.Assistant;
        }

        this._chatHistories[conversationId.Name].Add(new SKChatMessage(aiMessage));
        return true;
    }

    private static Dictionary<string, object> _MessageToDict(object message)
    {
        if (message is string)
        {
            return new Dictionary<string, object> { { "content", message } };
        }
        else if (message is Dictionary<string, object>)
        {
            return (Dictionary<string, object>)message;
        }
        else
        {
            throw new ArgumentException("Message must be a string or a dictionary.");
        }
    }

    public override void Receive(object message, Agent sender, bool? requestReply = null)
    {
        throw new NotImplementedException();
    }

    public override async Task ReceiveAsync(object message, Agent sender, bool? requestReply = null, bool? silent = false)
    {
        this._ProcessReceivedMessage(message, sender, silent == true);
        if (requestReply == false || (requestReply == null && this._replyAtReceive.TryGetValue(sender, out var val) && !val))
        {
            return;
        }
        object reply = await this.GenerateReplyAsync(/*messages: this._chatHistories[sender.Name],*/ sender: sender);
        if (reply != null)
        {
            await this.SendAsync(reply, recipient: sender, silent: silent);
        }
    }

    private void _ProcessReceivedMessage(object message, Agent sender, bool silent)
    {
        var messageDict = _MessageToDict(message);
        var valid = this._AppendOaiMessage(message, "user", sender);

        if (!valid)
        {
            throw new ArgumentException("Received message can't be converted into a valid ChatCompletion message. Either content or function_call must be provided.");
        }

        if (!silent)
        {
            this.PrintReceivedMessage(messageDict, sender);
        }
    }

    private void PrintReceivedMessage(IDictionary<string, object> message, Agent sender)
    {
        AnsiConsole.MarkupLine($"[yellow]{sender.Name} (to {this.Name}):[/]");

        if (message.ContainsKey("role") && message["role"].ToString() == "function")
        {
            string funcName = message.ContainsKey("name") ? message["name"].ToString() : "(No function name found)";
            string funcPrint = $"[green]***** Response from calling function \"{funcName}\" *****[/]";
            AnsiConsole.MarkupLine(funcPrint);
            AnsiConsole.WriteLine(message["content"].ToString());
            AnsiConsole.MarkupLine($"[green]{new string('*', funcPrint.Length)}[/]");
        }
        else
        {
            var content = message.ContainsKey("content") ? message["content"].ToString() : null;
            if (content != null)
            {
                if (message.ContainsKey("context"))
                {
                    AnsiConsole.WriteLine("HIT THIS -- need to use prompt template engine maybe?");
                    // var context = (Dictionary<string, object>)message["context"];
                    // var allowFormatStrTemplate = false;//llmConfig != null && llmConfig.ContainsKey("allow_format_str_template") && (bool)llmConfig["allow_format_str_template"];
                    // content = OAI.ChatCompletion.Instantiate(content, context, allowFormatStrTemplate);
                }

                AnsiConsole.MarkupLineInterpolated($"{content}");
            }

            if (message.ContainsKey("function_call"))
            {
                var functionCall = (Dictionary<string, object>)message["function_call"];
                string funcName = functionCall.ContainsKey("name") ? functionCall["name"].ToString() : "(No function name found)";
                string funcPrint = $"[green]***** Suggested function Call: {funcName} *****[/]";
                AnsiConsole.MarkupLine(funcPrint);
                AnsiConsole.WriteLine("Arguments:");
                AnsiConsole.WriteLine(functionCall.ContainsKey("arguments") ? functionCall["arguments"].ToString() : "(No arguments found)");
                AnsiConsole.MarkupLine($"[green]{new string('*', funcPrint.Length)}[/]");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]--------------------------------------------------------------------------------[/]");
    }

    public override void Reset()
    {
        throw new NotImplementedException();
    }

    public override object GenerateReply(ChatHistory? messages = null, Agent? sender = null,
    List<Delegate>? exclude = null, Dictionary<string, object>? kwargs = null)
    {
        throw new NotImplementedException();
    }

    public override async Task<object?> GenerateReplyAsync(ChatHistory? messages = null, Agent? sender = null,
    List<Delegate>? exclude = null, Dictionary<string, object>? kwargs = null)
    {
        if (messages == null && sender == null)
        {
            string errorMsg = "Either messages or sender must be provided.";
            // Logger.Error(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        if (messages == null)
        {
            messages = this._chatHistories[sender.Name];
        }

        foreach (var replyFuncTuple in this._replyFuncList)
        {
            var replyFunc = replyFuncTuple.ReplyFunc as Delegate;
            if (exclude != null && exclude.Contains(replyFunc))
            {
                continue;
            }

            if (this.MatchTrigger(replyFuncTuple.Trigger, sender))
            {
                var isAsync = replyFunc.Method.ReturnType == typeof(Task<ValueTuple<bool, object>>);
                object final, reply;

                if (isAsync)
                {
                    Task<ValueTuple<bool, object>>? task = (Task<ValueTuple<bool, object>>)replyFuncTuple.ReplyFunc(messages, sender, replyFuncTuple.Config);
                    if (task is not null)
                    {
                        var result = await task.ConfigureAwait(false);

                        final = result.Item1;
                        reply = result.Item2;
                    }
                    else
                    {
                        final = false;
                        reply = null;
                    }
                }
                else
                {
                    final = ((Func<ChatHistory, Agent, object, object>)replyFunc)(
                        messages, sender, replyFuncTuple.Config
                    );
                    reply = final;
                }

                if ((bool)final)
                {
                    return reply;
                }
            }
        }

        return this._defaultAutoReply;
    }

    private bool MatchTrigger(object trigger, Agent sender)
    {
        if (trigger == null)
        {
            return sender == null;
        }
        else if (trigger is string triggerString)
        {
            return triggerString == sender.Name;
        }
        else if (trigger is Type triggerType)
        {
            return triggerType.IsInstanceOfType(sender);
        }
        else if (trigger is Agent triggerAgent)
        {
            return triggerAgent == sender;
        }
        else if (trigger is Delegate triggerDelegate)
        {
            return (bool)triggerDelegate.DynamicInvoke(sender);
        }
        else if (trigger is List<object> triggerList)
        {
            return triggerList.Any(t => this.MatchTrigger(t, sender));
        }
        else
        {
            throw new InvalidOperationException($"Unsupported trigger type: {trigger.GetType()}");
        }
    }

    private sealed class ReplyFuncConfig
    {
        public object Trigger { get; set; }
        public Func<ChatHistory, Agent, object, object?> ReplyFunc { get; set; }
        public int Position { get; set; }
        public object Config { get; set; }
        public Func<object, object> ResetConfig { get; set; }
    }
}
