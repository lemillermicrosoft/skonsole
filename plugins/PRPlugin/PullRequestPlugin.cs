// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Reflection;
using CondensePluginLib;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.TextGeneration;
using PRPlugin.Utils;
using System.IO;

namespace PRPlugin;

public static class FunctionEx
{
    public static async Task<KernelArguments> RollingChunkProcess(this KernelFunction func, Kernel kernel, List<string> chunkedInput, KernelArguments context)
    {
        context["previousresults"] = string.Empty;
        foreach (var chunk in chunkedInput)
        {
            context[KernelArguments.InputParameterName] = chunk;
            var result = await func.InvokeAsync(kernel, context);

            context["previousresults"] = result.GetValue<string>();
        }

        return context;
    }

    public static async Task<KernelArguments> CondenseChunkProcess(this KernelFunction func, Kernel kernel, CondensePlugin condensePlugin, List<string> chunkedInput, string prompt, KernelArguments context, string resultTag, CancellationToken cancellationToken = default)
    {
        var results = new List<string>();
        foreach (var chunk in chunkedInput)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context[KernelArguments.InputParameterName] = chunk;
            var result = await func.InvokeAsync(kernel, context, cancellationToken);

            results.Add(result.GetValue<string>());
        }

        if (chunkedInput.Count <= 1)
        {
            context[KernelArguments.InputParameterName] = results.First();
            return context;
        }

        // update memory with serialized list of results
        context["prompt"] = prompt;
        return await condensePlugin.Condense(context, kernel, string.Join($"\n ====={resultTag}=====\n", results) + $"\n ====={resultTag}=====\n", cancellationToken: cancellationToken);
    }

    public static async Task<KernelArguments> AggregateChunkProcess(this KernelFunction func, Kernel kernel, List<string> chunkedInput, KernelArguments context)
    {
        var results = new List<string>();
        foreach (var chunk in chunkedInput)
        {
            context[KernelArguments.InputParameterName] = chunk;
            var result = await func.InvokeAsync(kernel, context);

            results.Add(result.GetValue<string>());
        }

        context[KernelArguments.InputParameterName] = string.Join("\n", results);
        return context;
    }
}

public class PullRequestPlugin
{
    public const string SEMANTIC_FUNCTION_PATH = "PRPlugin";
    private const int CHUNK_SIZE = 8000; // Eventually this should come from the kernel

    private readonly CondensePlugin _condensePlugin;

    private readonly Kernel _kernel;
    private readonly ILogger _logger;

    public PullRequestPlugin(Kernel kernel)
    {
        try
        {
            // Load semantic plugin defined with prompt templates
            var folder = PRPluginsPath();
            var PRPlugin = kernel.ImportPluginFromPromptDirectory(Path.Combine(folder, SEMANTIC_FUNCTION_PATH));
            this._condensePlugin = new CondensePlugin(kernel);

            this._kernel = new KernelBuilder()
                .WithServices((serviceCollection) =>
                {
                    serviceCollection.AddKeyedSingleton<ITextGenerationService>(null, new RedirectTextCompletion());
                })
                .Build();
            this._kernel.ImportPluginFromPromptDirectory(Path.Combine(folder, SEMANTIC_FUNCTION_PATH));

            this._logger = kernel.LoggerFactory.CreateLogger(this.GetType());
        }
        catch (Exception e)
        {
            throw new Exception("Failed to load plugin.", e);
        }
    }

    [KernelFunction, Description("Generate feedback for a pull request based on a git diff or git show file output.")]
    public async Task<KernelArguments> GeneratePullRequestFeedback(
        Kernel kernel,
        [Description("Output of a `git diff` or `git show` command.")]
        string input,
        KernelArguments context)
    {
        this._logger.LogTrace("GeneratePullRequestFeedback called");

        var prFeedbackGenerator = kernel.Plugins[SEMANTIC_FUNCTION_PATH]["PullRequestFeedbackGenerator"];
        var chunkedInput = CommitChunker.ChunkCommitInfo(input, CHUNK_SIZE);
        return await prFeedbackGenerator.AggregateChunkProcess(kernel, chunkedInput, context);
    }

    [KernelFunction, Description("Generate a commit message based on a git diff file output.")]
    public async Task<KernelArguments> GenerateCommitMessage(
        Kernel kernel,
        [Description("Output of a `git diff` command.")]
        string input,
        KernelArguments context,
        CancellationToken cancellationToken = default)
    {
        this._logger.LogTrace("GenerateCommitMessage called");

        var commitGenerator = kernel.Plugins[SEMANTIC_FUNCTION_PATH]["CommitMessageGenerator"];

        var commitGeneratorCapture = this._kernel.Plugins[SEMANTIC_FUNCTION_PATH]["CommitMessageGenerator"];
        var prompt = (await this._kernel.InvokeAsync(commitGeneratorCapture, cancellationToken: cancellationToken)).GetValue<string>();

        var chunkedInput = CommitChunker.ChunkCommitInfo(input, CHUNK_SIZE, cancellationToken);
        return await commitGenerator.CondenseChunkProcess(kernel, this._condensePlugin, chunkedInput, prompt, context, "CommitMessageResult", cancellationToken);
    }

    [KernelFunction, Description("Generate a pull request description based on a git diff or git show file output using a rolling query mechanism.")]
    public async Task<KernelArguments> GeneratePR_Rolling(
        Kernel kernel,
        [Description("Output of a `git diff` or `git show` command.")]
        string input,
        KernelArguments context,
        CancellationToken cancellationToken = default)
    {
        var prGenerator_Rolling = kernel.Plugins[SEMANTIC_FUNCTION_PATH]["PullRequestDescriptionGenerator_Rolling"];
        var chunkedInput = CommitChunker.ChunkCommitInfo(input, CHUNK_SIZE, cancellationToken);
        return await prGenerator_Rolling.RollingChunkProcess(kernel, chunkedInput, context);
    }

    [KernelFunction, Description("Generate a pull request description based on a git diff or git show file output using a reduce mechanism.")]
    public async Task<KernelArguments> GeneratePR(
        Kernel kernel,
        KernelArguments context,
        [Description("Output of a `git diff` or `git show` command.")]
        string input,
        CancellationToken cancellationToken = default)
    {
        var prGenerator = kernel.Plugins[SEMANTIC_FUNCTION_PATH]["PullRequestDescriptionGenerator"];

        var prGeneratorCapture = this._kernel.Plugins[SEMANTIC_FUNCTION_PATH]["PullRequestDescriptionGenerator"];
        var contextVariablesWithoutInput = new KernelArguments(context);
        contextVariablesWithoutInput["input"] = "";
        var prompt = (await this._kernel.InvokeAsync(prGeneratorCapture, contextVariablesWithoutInput, cancellationToken: cancellationToken)).GetValue<string>();

        var chunkedInput = CommitChunker.ChunkCommitInfo(input, CHUNK_SIZE, cancellationToken);
        return await prGenerator.CondenseChunkProcess(kernel, this._condensePlugin, chunkedInput, prompt, context, "PullRequestDescriptionResult", cancellationToken);
    }

    #region MISC
    private static string PRPluginsPath()
    {
        const string PARENT = "SemanticFunctions";
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
            throw new Exception("Plugins directory not found. The app needs the plugins from the library to work.");
        }

        return path;
    }
    #endregion MISC
}

public class RedirectTextCompletion : ITextGenerationService
{
    Task<IReadOnlyList<TextContent>> ITextGenerationService.GetTextContentsAsync(string prompt, PromptExecutionSettings executionSettings, Kernel kernel, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<TextContent>>(new List<TextContent> { new TextContent(prompt) });
    }

    IAsyncEnumerable<StreamingTextContent> ITextGenerationService.GetStreamingTextContentsAsync(string prompt, PromptExecutionSettings executionSettings, Kernel kernel, CancellationToken cancellationToken)
    {
        throw new NotImplementedException(); // TODO
    }

    public IReadOnlyDictionary<string, object> Attributes => new Dictionary<string, object>();
}
