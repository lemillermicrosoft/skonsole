
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Planners;

namespace SKonsole.Agents;
public class StepwiseAgent : ConversableAgent
{
    public StepwiseAgent(
        string name,
        string systemMessage = "You are a helpful AI Assistant.", // not used
        Func<string, bool>? isTerminationMsg = null,
        int? maxConsecutiveAutoReply = null,
        string humanInputMode = "NEVER",
        Dictionary<string, object>? codeExecutionConfig = null,
        IKernel? kernel = null,
        object? defaultAutoReply = null)
        : base(name, systemMessage, isTerminationMsg, maxConsecutiveAutoReply, humanInputMode, codeExecutionConfig, kernel, defaultAutoReply)
    {
        this._planner = new StepwisePlanner(kernel);
        this._kernel = kernel;
    }

    private StepwisePlanner _planner;
    private IKernel _kernel;

    public override async Task<object?> GenerateReplyAsync(ChatHistory? messages = null, Agent? sender = null,
    List<Delegate>? exclude = null, Dictionary<string, object>? kwargs = null)
    {
        if (messages == null && sender == null)
        {
            string errorMsg = "Either messages or sender must be provided.";
            // Logger.Error(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        if (messages == null && sender is not null)
        {
            messages = this._chatHistories[sender.Name];
        }

        var history = string.Join("\n", messages!.Where(m => m.Role != AuthorRole.System).Select(m => $"{m.Role}: {m.Content}"));
        var plan = this._planner.CreatePlan($"{history}\n---\nGiven the conversation history, respond to the most recent message. Reply \"TERMINATE\" in the end when everything is done.");
        var result = await this._kernel.RunAsync(plan);

        var functionResult = result?.FunctionResults?.FirstOrDefault();
        if (functionResult == null)
        {
            return null;
        }

        return functionResult.GetValue<string>();
    }
}
