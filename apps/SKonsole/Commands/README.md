# Enabling Chat Scenarios

Table of contents:


*...Intro...*

### Simple Chat

> \> skonsole stepwise

Two pieces enable this scenario. First, an SKFunction that leverages the StepwisePlanner to generate a response. Second, a simple loop to call the function.

Let's look at the code to generate this scenario.

1. Create a `RespondTo` function.
2. Pass message and history to `RespondTo`.
3. Loop

RespondTo:

```csharp
[SKFunction, Description("Respond to a message.")]
public async Task<FunctionResult> RespondTo(string message, string history, CancellationToken cancellationToken = default)
{
    var planner = new StepwisePlanner(this._kernel);

    var plan = planner.CreatePlan($"{history}\n---\nGiven the conversation history, respond to the most recent message.");

    return await this._kernel.RunAsync(plan, cancellationToken: cancellationToken);
}
```

Call RespondTo:

```csharp
var result = await kernel.RunAsync(contextVariables, functions["RespondTo"]);
```

This will start a simple chat session with the configured LLM.

```
── AI ───────────────────────────────────────

Hello!

── User ─────────────────────────────────────

Hey there! Got any good jokes?

── AI ───────────────────────────────────────

Sure, here's a classic one for you: Why did the chicken cross the road? To get to the other side!
```