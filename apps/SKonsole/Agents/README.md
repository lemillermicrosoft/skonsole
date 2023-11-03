# AutoGen Agents
The code here is inspired by the https://github.com/microsof/AutoGen project.


```cs
var assistantKernel = KernelProvider.Instance.Get();
var stepwiseKernel = StepwisePlannerCommand.LoadOptionSet("bing++");
var userKernel = KernelProvider.Instance.Get();

// Depends on the userProxy being able to *do* something.
var assistant = new SKonsole.Agents.AssistantAgent(
    name: "assistant",
    kernel: assistantKernel);

var stepwiseAssistant = new SKonsole.Agents.StepwiseAgent(
    name: "stepwise_assistant",
    kernel: stepwiseKernel);

var userProxy = new SKonsole.Agents.UserProxyAgent(
    name: "user_proxy",
    kernel: userKernel,
    humanInputMode: "ALWAYS",// "NEVER",
    maxConsecutiveAutoReply: 10,
    isTerminationMsg: (msg) => msg.TrimEnd().EndsWith("TERMINATE"),
    codeExecutionConfig: new Dictionary<string, object>
    {
        { "work_dir", "coding" },
        { "use_docker", false } // set to True or image name like "python:3" to use docker
    });

await userProxy.StartChatAsync(stepwiseAssistant, context: new Dictionary<string, object>() { { "message", "what's the current Microsoft stock price?" } });
```


At present, `skonsole agent` will execute the agent setup with a pre-configured question. An example output follows:

```
--------------------------------------------------------------------------------
user_proxy (to stepwise_assistant):
what's the current Microsoft stock price?

--------------------------------------------------------------------------------
Thought:  I need to search the web for the current Microsoft stock price.
Action:  bing.Search({"query":"current Microsoft stock price"}).
Observation:  ["Find the latest Microsoft Corporation (MSFT) stock quote, history, news and other vital information to help you with your stock trading and
Thought:  The observation contains various sources, but I need to find the exact stock price from the results.
Action:  bing.Search({"query":"Microsoft stock price site:nasdaq.com"}).
Observation:  ["Microsoft Corporation Common Stock (MSFT) Stock Quotes - Nasdaq offers stock quotes \u0026 market activity data for US and global markets.","MSFT
Thought:  I still couldn't find the exact stock price in the observation. I'll try searching again with a more specific query.
Action:  bing.Search({"query":"Microsoft stock price MSFT site:finance.yahoo.com"}).
Observation:  ["Find the latest Microsoft Corporation (MSFT) stock quote, history, news and other vital information to help you with your stock trading and
Thought:  I still couldn't find the exact stock price in the observation. I'll try one more time with a different query.
Action:  bing.Search({"query":"Microsoft stock price MSFT site:marketwatch.com"}).
Observation:  ["0.47%. $129.84B. MSFT | Complete Microsoft Corp. stock news by MarketWatch. View real-time stock prices and stock quotes for a full financial
Final Answer:  The current Microsoft stock price is approximately $348.32.

stepwise_assistant (to user_proxy):
The current Microsoft stock price is approximately $348.32.
--------------------------------------------------------------------------------
```