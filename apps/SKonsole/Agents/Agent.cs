using Microsoft.SemanticKernel.AI.ChatCompletion;

namespace SKonsole.Agents;

/// <summary>
/// An abstract class for AI agents.
/// An agent can communicate with other agents and perform actions.
/// Agents can define which actions they perform in the Receive methods.
/// </summary>
public abstract class Agent
{
    private string _name;

    public Agent(string name)
    {
        this._name = name;
    }

    public string Name
    {
        get { return this._name; }
    }

    public abstract void Send(object message, Agent recipient, bool? requestReply = null);

    public abstract Task SendAsync(object message, Agent recipient, bool? requestReply = null, bool? silent = false);

    public abstract void Receive(object message, Agent sender, bool? requestReply = null);

    public abstract Task ReceiveAsync(object message, Agent sender, bool? requestReply = null, bool? silent = false);

    public abstract void Reset();

    public abstract object? GenerateReply(
        ChatHistory? messages = null,
        Agent? sender = null,
        List<Delegate>? exclude = null,
        Dictionary<string, object>? kwargs = null);

    public abstract Task<object?> GenerateReplyAsync(
        ChatHistory? messages = null,
        Agent? sender = null,
        List<Delegate>? exclude = null,
        Dictionary<string, object>? kwargs = null);
}
