
using Microsoft.SemanticKernel;

namespace SKonsole.Agents;

public class UserProxyAgent : ConversableAgent
{
    public UserProxyAgent(string name, string systemMessage = "You are a helpful AI Assistant.", Func<string, bool>? isTerminationMsg = null, int? maxConsecutiveAutoReply = null, string humanInputMode = "TERMINATE", Dictionary<string, object>? codeExecutionConfig = null, IKernel? kernel = null, object? defaultAutoReply = null) : base(name, systemMessage, isTerminationMsg, maxConsecutiveAutoReply, humanInputMode, codeExecutionConfig, kernel, defaultAutoReply)
    {
    }
}
