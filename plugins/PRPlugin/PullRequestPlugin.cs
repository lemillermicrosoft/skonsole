// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Reflection;
using CondensePluginLib;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Orchestration;
using PRPlugin.Utils;

namespace PRPlugin;

public static class FunctionEx
{
    public static async Task<SKContext> RollingChunkProcess(this ISKFunction func, List<string> chunkedInput, SKContext context)
    {
        context.Variables.Set("previousresults", string.Empty);
        foreach (var chunk in chunkedInput)
        {
            context.Variables.Update(chunk);
            var result = await context.Runner.RunAsync(func, context.Variables);

            context.Variables.Set("previousresults", result.GetValue<string>());
        }

        return context;
    }

    public static async Task<SKContext> CondenseChunkProcess(this ISKFunction func, CondensePlugin condensePlugin, List<string> chunkedInput, string prompt, SKContext context, string resultTag, CancellationToken cancellationToken = default)
    {
        var results = new List<string>();
        foreach (var chunk in chunkedInput)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Variables.Update(chunk);
            var result = await context.Runner.RunAsync(func, context.Variables, cancellationToken);

            results.Add(result.GetValue<string>());
        }

        if (chunkedInput.Count <= 1)
        {
            context.Variables.Update(context.Result);
            return context;
        }

        // update memory with serialized list of results
        context.Variables.Set("prompt", prompt);
        return await condensePlugin.Condense(context, string.Join($"\n ====={resultTag}=====\n", results) + $"\n ====={resultTag}=====\n", cancellationToken: cancellationToken);
    }

    public static async Task<SKContext> AggregateChunkProcess(this ISKFunction func, List<string> chunkedInput, SKContext context)
    {
        var results = new List<string>();
        foreach (var chunk in chunkedInput)
        {
            context.Variables.Update(chunk);
            var result = await context.Runner.RunAsync(func, context.Variables);

            results.Add(result.GetValue<string>());
        }

        context.Variables.Update(string.Join("\n", results));
        return context;
    }
}

public class PullRequestPlugin
{
    public const string SEMANTIC_FUNCTION_PATH = "PRPlugin";
    private const int CHUNK_SIZE = 8000; // Eventually this should come from the kernel

    private readonly CondensePlugin _condensePlugin;

    private readonly IKernel _kernel;
    private readonly ILogger _logger;

    public PullRequestPlugin(IKernel kernel)
    {
        try
        {
            // Load semantic plugin defined with prompt templates
            var folder = PRPluginsPath();
            var PRPlugin = kernel.ImportSemanticFunctionsFromDirectory(folder, SEMANTIC_FUNCTION_PATH);
            this._condensePlugin = new CondensePlugin(kernel);

            this._kernel = new KernelBuilder()
                .WithAIService<ITextCompletion>(null, new RedirectTextCompletion(), true)
                .Build();
            this._kernel.ImportSemanticFunctionsFromDirectory(folder, SEMANTIC_FUNCTION_PATH);

            this._logger = kernel.LoggerFactory.CreateLogger(this.GetType());
        }
        catch (Exception e)
        {
            throw new Exception("Failed to load plugin.", e);
        }
    }

    [SKFunction, Description("Generate feedback for a pull request based on a git diff or git show file output.")]
    public async Task<SKContext> GeneratePullRequestFeedback(
        [Description("Output of a `git diff` or `git show` command.")]
        string input,
        SKContext context)
    {
        this._logger.LogTrace("GeneratePullRequestFeedback called");

        var prFeedbackGenerator = context.Functions.GetFunction(SEMANTIC_FUNCTION_PATH, "PullRequestFeedbackGenerator");
        var chunkedInput = CommitChunker.ChunkCommitInfo(input, CHUNK_SIZE);
        return await prFeedbackGenerator.AggregateChunkProcess(chunkedInput, context);
    }

    [SKFunction, Description("Generate a commit message based on a git diff file output.")]
    public async Task<SKContext> GenerateCommitMessage(
        [Description("Output of a `git diff` command.")]
        string input,
        SKContext context,
        CancellationToken cancellationToken = default)
    {
        this._logger.LogTrace("GenerateCommitMessage called");

        var commitGenerator = context.Functions.GetFunction(SEMANTIC_FUNCTION_PATH, "CommitMessageGenerator");

        var commitGeneratorCapture = this._kernel.Functions.GetFunction(SEMANTIC_FUNCTION_PATH, "CommitMessageGenerator");
        var prompt = (await this._kernel.RunAsync(commitGeneratorCapture, cancellationToken: cancellationToken)).GetValue<string>();

        var chunkedInput = CommitChunker.ChunkCommitInfo(input, CHUNK_SIZE, cancellationToken);
        return await commitGenerator.CondenseChunkProcess(this._condensePlugin, chunkedInput, prompt, context, "CommitMessageResult", cancellationToken);
    }

    [SKFunction, Description("Generate a pull request description based on a git diff or git show file output using a rolling query mechanism.")]
    public async Task<SKContext> GeneratePR_Rolling(
        [Description("Output of a `git diff` or `git show` command.")]
        string input,
        SKContext context,
        CancellationToken cancellationToken = default)
    {
        var prGenerator_Rolling = context.Functions.GetFunction(SEMANTIC_FUNCTION_PATH, "PullRequestDescriptionGenerator_Rolling");
        var chunkedInput = CommitChunker.ChunkCommitInfo(input, CHUNK_SIZE, cancellationToken);
        return await prGenerator_Rolling.RollingChunkProcess(chunkedInput, context);
    }

    [SKFunction, Description("Generate a pull request description based on a git diff or git show file output using a reduce mechanism.")]
    public async Task<SKContext> GeneratePR(
        [Description("Output of a `git diff` or `git show` command.")]
        string input,
        SKContext context,
        CancellationToken cancellationToken = default)
    {
        var prGenerator = context.Functions.GetFunction(SEMANTIC_FUNCTION_PATH, "PullRequestDescriptionGenerator");

        var prGeneratorCapture = this._kernel.Functions.GetFunction(SEMANTIC_FUNCTION_PATH, "PullRequestDescriptionGenerator");
        var contextVariablesWithoutInput = context.Variables.Clone();
        contextVariablesWithoutInput.Set("input", "");
        var prompt = (await this._kernel.RunAsync(prGeneratorCapture, contextVariablesWithoutInput, cancellationToken: cancellationToken)).GetValue<string>();

        var chunkedInput = CommitChunker.ChunkCommitInfo(input, CHUNK_SIZE, cancellationToken);
        return await prGenerator.CondenseChunkProcess(this._condensePlugin, chunkedInput, prompt, context, "PullRequestDescriptionResult", cancellationToken);
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

public class RedirectTextCompletion : ITextCompletion
{
    Task<IReadOnlyList<ITextResult>> ITextCompletion.GetCompletionsAsync(string text, AIRequestSettings requestSettings, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ITextResult>>(new List<ITextResult> { new RedirectTextCompletionResult(text) });
    }

    IAsyncEnumerable<ITextStreamingResult> ITextCompletion.GetStreamingCompletionsAsync(string text, AIRequestSettings requestSettings, CancellationToken cancellationToken)
    {
        throw new NotImplementedException(); // TODO
    }
}

internal sealed class RedirectTextCompletionResult : ITextResult
{
    private readonly string _completion;

    public RedirectTextCompletionResult(string completion)
    {
        this._completion = completion;
    }

    public ModelResult ModelResult => new(this._completion);

    public Task<string> GetCompletionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this._completion);
    }
}
