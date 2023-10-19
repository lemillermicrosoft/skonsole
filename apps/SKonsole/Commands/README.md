# Enabling Chat Scenarios
- [Enabling Chat Scenarios](#enabling-chat-scenarios)
  - [Simple Chat](#simple-chat)
  - [Grounded Chat](#grounded-chat)
  - [Dynamic Chat](#dynamic-chat)


## Simple Chat

> \> skonsole stepwise

The simple chat scenario is enabled by a custom plugin. This plugin provides a simple SKFunction that leverages the `StepwisePlanner` to generate a response for a message. A simple loop to call the function completes the scenario.

Implement the SKFunction:
```csharp
[SKFunction, Description("Respond to a message.")]
public async Task<SKContext> RespondTo(string message, string history, CancellationToken cancellationToken = default)
{
    var planner = new StepwisePlanner(this._kernel);

    var plan = planner.CreatePlan($"{history}\n---\nGiven the conversation history, respond to the most recent message.");

    return await this._kernel.RunAsync(plan, cancellationToken: cancellationToken);
}
```

Execute the function in a loop to start a chat session.
```csharp
var result = await kernel.RunAsync(contextVariables, functions["RespondTo"]);
```

This will start a simple chat session with the configured LLM.

```txt
── AI ──────────────────────────────────────────────────────────────────────────────

Hello!

── User ────────────────────────────────────────────────────────────────────────────

Hey there! Got any good jokes?

── AI ──────────────────────────────────────────────────────────────────────────────

Sure, here's a classic one for you: Why did the chicken cross the road? To get to the other side!
```

## Grounded Chat

> \> skonsole stepwise optionset bing

*`Bing` plugin is specified to enable grounding when responding*

Like above, this uses the same `RespondTo` function and chat loop. The difference is that the Bing plugin is specified to enable grounding.

```csharp
private static IKernel LoadOptionSet(string optionSet)
{
   var kernel = KernelProvider.Instance.Get();

    if (optionSet.Contains("bing"))
    {
        var bingConnector = new BingConnector(Configuration.ConfigVar("BING_API_KEY"));
        var bing = new WebSearchEnginePlugin(bingConnector);
        var search = kernel.ImportFunctions(bing, "bing");
    }

    /// Other option sets here

    return kernel;
}
```

Loading the option set is done before the chat loop is started.
```csharp
var kernel  = LoadOptionSet(optionSet);

var stepKernel = KernelProvider.Instance.Get();
var functions = stepKernel.ImportFunctions(new StepwisePlugin(kernel), "stepwise");

await RunChat(stepKernel, validPlugins, functions["RespondTo"]).ConfigureAwait(false);
```

This will start a grounded chat session with the configured LLM.
```txt
── AI ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
Hello!
── User ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
Show me the happy news headlines for the day in the voice of Hulk Hogan.
── AI - 1 (bing.Search(1)) ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
Brother! Here are some happy news headlines for you today, Hulkamaniacs:

1. "Artist Creates Iconic Landscapes From Food – Then Donates to Hungry People (LOOK)" - Whatcha gonna do when hunger runs wild on you?
2. "Burger King worker went viral for never missing a day of work in 27 years" - That's the power of Hulkamania, brother!
3. "Community Rallies To Replace Stolen Custom Bike For Teen With Autism" - Let me tell you something, Mean Gene, that's what I call teamwork!
4. "Lost Michigan toddler is found asleep in woods using a family dog as a pillow" - Just like the Hulkster, they never give up, dude!

Stay positive and keep the Hulkamania running wild, brother!
```

## Dynamic Chat

> \> skonsole stepwise optionset bing,git,time

*`Git` plugin here imports a dynamic plan object that can smartly handed complex asks and reasoning of large results*

// TODO - Add some code snippets about importing the Plan object

```txt
── AI ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

Hello!

── User ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

Generate a summary of the diff of all the *.csproj files and tell me the arguments you use to determine this along with the summary of the file contents.

── AI - 1 (Plan.GenerateDynamicGitDiffResult(1)) ──────────────────────────────────────────────────────────────────────────────────────────────────────────────

The arguments used to determine the diff of all the *.csproj files are: `-- *.csproj`. The summary of the file contents is as follows:

1. SKonsole.csproj: A new console app project file was added with various package references, project references, and properties such as OutputType, PackageId, Version, Authors, Owners, PackageIcon, Company,
PackageDescription, RepositoryUrl, RepositoryType, TargetFramework, ImplicitUsings, Nullable, PackageTags, PackageReadmeFile, PackAsTool, ToolCommandName, PackageLicenseExpression, and Description.

2. CondensePlugin.csproj: A new plugin project file was added with package references to Microsoft.SemanticKernel and Microsoft.ML.Tokenizers, and properties such as TargetFramework, ImplicitUsings, LangVersion, and
RootNamespace. It also includes embedded resources like dict.txt, encoder.json, and vocab.bpe.

3. PRPlugin.csproj: A new plugin project file was added with a package reference to Microsoft.SemanticKernel, properties such as TargetFramework, ImplicitUsings, and LangVersion, and a project reference to
CondensePlugin.csproj.
```

> \> skonsole stepwise optionset bing,file,time

*`file` plugin here adds ability to read and write files to the file system*

```txt
── AI ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
Hello!
── User ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
Find recent headlines about Semantic Kernel
── AI - 1 (bing.Search(1)) ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

Here are some recent headlines about Semantic Kernel:

1. "Matthew Bolanos: Semantic Kernel SDK v1.0.0-beta1 release"
2. "Fall 2023 release plan for Semantic Kernel: focus on open source, reliability, and AI innovations"
3. "Semantic Kernel: encapsulating design patterns from the latest AI research"
4. "Microsoft's announcement: making Semantic Kernel AI provider agnostic"
5. "Semantic Kernel SDK v1.0.0-beta1: breaking changes and API lockdown"
6. "Microsoft's open source Semantic Kernel: improving large language models for app development"
7. "Microsoft's new v1.0.0 Beta1 for the .NET Semantic Kernel SDK: reducing OpenAI tie-ins"
8. "From 'Skills' to 'Plugins': standardization in Semantic Kernel"
9. "Semantic Kernel: a new AI SDK for adding large language capabilities to your app"

These headlines provide an overview of the recent developments and updates related to Semantic Kernel.

── User ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

Search for more details about these headlines and write a summary to a file in the current directory. include the current date in the filename.

── AI - 10 (bing.Search(8), time.Date(1), file.Write(1)) ──────────────────────────────────────────────────────────────────────────────────────────────────────

Here is the summary of the recent headlines about Semantic Kernel:

Semantic Kernel Headlines Summary:

1. Semantic Kernel SDK v1.0.0-beta1 has been released, introducing new features and improvements. The SDK now supports multiple AI models and has refactored planners and memory configuration.

2. The Fall 2023 release plan for Semantic Kernel focuses on open source, reliability, and AI innovations. The SDK aims to encapsulate design patterns from the latest AI research.

3. Microsoft announced that Semantic Kernel will become AI provider agnostic, allowing developers to use various AI services like OpenAI, Azure OpenAI, and Hugging Face.

4. The term 'Skills' has been replaced by 'Plugins' in Semantic Kernel to standardize the naming convention and align with the OpenAI plugin specification.

5. Semantic Kernel v1.0.0-beta1 reduces OpenAI tie-ins, making the SDK service-agnostic and providing developers with more flexibility in choosing AI services.

I have saved this summary to a file in your current directory with the filename "Semantic_Kernel_Headlines_Summary_October_19_2023.txt".
```