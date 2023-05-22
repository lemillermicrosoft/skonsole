// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using Microsoft.Extensions.Logging;
using PRSkill.Utils;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using CondenseSkillLib;
using Microsoft.SemanticKernel.AI.TextCompletion;

namespace PRSkill;

public static class FunctionEx
{
    public static async Task<SKContext> RollingChunkProcess(this ISKFunction func, List<string> chunkedInput, SKContext context)
    {
        context.Variables.Set("previousresults", string.Empty);
        foreach (var chunk in chunkedInput)
        {
            context.Variables.Update(chunk);
            context = await func.InvokeAsync(context);

            context.Variables.Set("previousresults", context.Variables.ToString());
        }

        return context;
    }

    public static async Task<SKContext> CondenseChunkProcess(this ISKFunction func, CondenseSkill condenseSkill, List<string> chunkedInput, string prompt, SKContext context)
    {
        var results = new List<string>();
        foreach (var chunk in chunkedInput)
        {
            context.Variables.Update(chunk);
            context = await func.InvokeAsync(context);

            results.Add(context.Variables.ToString());
        }

        if (chunkedInput.Count <= 1)
        {
            context.Variables.Update(context.Variables.ToString());
            return context;
        }

        // update memory with serialized list of results
        context.Variables.Update(string.Join(CondenseSkill.RESULTS_SEPARATOR, results));
        context.Variables.Set("prompt", prompt);
        return await condenseSkill.Condense(context);
    }

    public static async Task<SKContext> AggregateChunkProcess(this ISKFunction func, List<string> chunkedInput, SKContext context)
    {
        var results = new List<string>();
        foreach (var chunk in chunkedInput)
        {
            context.Variables.Update(chunk);
            context = await func.InvokeAsync(context);

            results.Add(context.Variables.ToString());
        }

        context.Variables.Update(string.Join("\n", results));
        return context;
    }
}

public class PullRequestSkill
{
    public const string SEMANTIC_FUNCTION_PATH = "PRSkill";
    private const int CHUNK_SIZE = 8000; // Eventually this should come from the kernel

    private readonly CondenseSkill condenseSkill;

    public PullRequestSkill(IKernel kernel)
    {
        try
        {
            // Load semantic skill defined with prompt templates
            var folder = PRSkillsPath();
            var PRSkill = kernel.ImportSemanticSkillFromDirectory(folder, SEMANTIC_FUNCTION_PATH);
            this.condenseSkill = new CondenseSkill(kernel);
        }
        catch (Exception e)
        {
            throw new Exception("Failed to load skill.", e);
        }
    }

    [SKFunction(description: "Generate feedback for a pull request based on a git diff or git show file output.")]
    [SKFunctionContextParameter(Name = "Input", Description = "Output of a `git diff` or `git show` command.")]
    public async Task<SKContext> GeneratePullRequestFeedback(SKContext context)
    {
        try
        {
            context.Log.LogTrace("GeneratePullRequestFeedback called");

            var prFeedbackGenerator = context.Func(SEMANTIC_FUNCTION_PATH, "PullRequestFeedbackGenerator");
            var chunkedInput = CommitChunker.ChunkCommitInfo(context.Variables.Input, CHUNK_SIZE);
            return await prFeedbackGenerator.AggregateChunkProcess(chunkedInput, context);
        }
        catch (Exception e)
        {
            return context.Fail(e.Message, e);
        }
    }

    [SKFunction(description: "Generate a commit message based on a git diff file output.")]
    [SKFunctionContextParameter(Name = "Input", Description = "Output of a `git diff` command.")]
    public async Task<SKContext> GenerateCommitMessage(SKContext context)
    {
        try
        {
            context.Log.LogTrace("GenerateCommitMessage called");

            var commitGenerator = context.Func(SEMANTIC_FUNCTION_PATH, "CommitMessageGenerator");

            var commitGeneratorCapture = context.Func(SEMANTIC_FUNCTION_PATH, "CommitMessageGenerator");
            commitGeneratorCapture.SetAIService(() => new RedirectTextCompletion());
            var prompt = (await commitGeneratorCapture.InvokeAsync()).Result;

            var chunkedInput = CommitChunker.ChunkCommitInfo(context.Variables.Input, CHUNK_SIZE);
            return await commitGenerator.CondenseChunkProcess(this.condenseSkill, chunkedInput, prompt, context);
        }
        catch (Exception e)
        {
            return context.Fail(e.Message, e);
        }
    }

    [SKFunction(description: "Generate a pull request description based on a git diff or git show file output using a rolling query mechanism.")]
    [SKFunctionContextParameter(Name = "Input", Description = "Output of a `git diff` or `git show` command.")]
    public async Task<SKContext> GeneratePR_Rolling(SKContext context)
    {
        try
        {
            var prGenerator_Rolling = context.Func(SEMANTIC_FUNCTION_PATH, "PullRequestDescriptionGenerator_Rolling");
            var chunkedInput = CommitChunker.ChunkCommitInfo(context.Variables.Input, CHUNK_SIZE);
            return await prGenerator_Rolling.RollingChunkProcess(chunkedInput, context);
        }
        catch (Exception e)
        {
            return context.Fail(e.Message, e);
        }
    }

    // DesignDocGenerator wrapper
    [SKFunction(description: "Generate a design doc based on a git diff or git show file output using a rolling query mechanism.")]
    [SKFunctionContextParameter(Name = "Input", Description = "Output of a `git diff` or `git show` command.")]
    public async Task<SKContext> GenerateDesignDoc(SKContext context)
    {
        try
        {
            context.Log.LogTrace("GenerateDesignDoc called");

            var designDocGenerator = context.Func(SEMANTIC_FUNCTION_PATH, "DesignDocGenerator");

            var designDocGeneratorCapture = context.Func(SEMANTIC_FUNCTION_PATH, "DesignDocGenerator");
            designDocGeneratorCapture.SetAIService(() => new RedirectTextCompletion());
            var prompt = (await designDocGeneratorCapture.InvokeAsync()).Result;

            var chunkedInput = CommitChunker.ChunkCommitInfo(context.Variables.Input, CHUNK_SIZE);
            return await designDocGenerator.CondenseChunkProcess(this.condenseSkill, chunkedInput, prompt, context);
        }
        catch (Exception e)
        {
            return context.Fail(e.Message, e);
        }
    }

    // MotivationAndContextGenerator wrapper
    [SKFunction(description: "Generate a motivation and context based on a git diff or git show file output using a rolling query mechanism.")]
    [SKFunctionContextParameter(Name = "Input", Description = "Output of a `git diff` or `git show` command.")]
    public async Task<SKContext> GenerateMotivationAndContext(SKContext context)
    {
        try
        {
            context.Log.LogTrace("GenerateMotivationAndContext called");

            var motivationAndContextGenerator = context.Func(SEMANTIC_FUNCTION_PATH, "MotivationAndContextGenerator");

            var motivationAndContextGeneratorCapture = context.Func(SEMANTIC_FUNCTION_PATH, "MotivationAndContextGenerator");
            motivationAndContextGeneratorCapture.SetAIService(() => new RedirectTextCompletion());
            var prompt = (await motivationAndContextGeneratorCapture.InvokeAsync()).Result;

            var chunkedInput = CommitChunker.ChunkCommitInfo(context.Variables.Input, CHUNK_SIZE);
            // if chunkedInput count is above 5 log warning with # of chunks
            if (chunkedInput.Count > 5)
            {
                context.Log.LogWarning($"ChunkedInput count is {chunkedInput.Count} which is above 5.");
            }
            return await motivationAndContextGenerator.CondenseChunkProcess(this.condenseSkill, chunkedInput, prompt, context);
        }
        catch (Exception e)
        {
            return context.Fail(e.Message, e);
        }
    }

    // DescriptionGenerator wrapper
    [SKFunction(description: "Generate a description based on a git diff or git show file output using a rolling query mechanism.")]
    [SKFunctionContextParameter(Name = "Input", Description = "Output of a `git diff` or `git show` command.")]
    public async Task<SKContext> GenerateDescription(SKContext context)
    {
        try
        {
            context.Log.LogTrace("GenerateDescription called");

            var descriptionGenerator = context.Func(SEMANTIC_FUNCTION_PATH, "DescriptionGenerator");

            var descriptionGeneratorCapture = context.Func(SEMANTIC_FUNCTION_PATH, "DescriptionGenerator");
            descriptionGeneratorCapture.SetAIService(() => new RedirectTextCompletion());
            var prompt = (await descriptionGeneratorCapture.InvokeAsync()).Result;

            var chunkedInput = CommitChunker.ChunkCommitInfo(context.Variables.Input, CHUNK_SIZE);
            // if chunkedInput count is above 5 log warning with # of chunks
            if (chunkedInput.Count > 5)
            {
                context.Log.LogWarning($"ChunkedInput count is {chunkedInput.Count} which is above 5.");
            }
            return await descriptionGenerator.CondenseChunkProcess(this.condenseSkill, chunkedInput, prompt, context);
        }
        catch (Exception e)
        {
            return context.Fail(e.Message, e);
        }
    }

    [SKFunction(description: "Generate a pull request description based on a git diff or git show file output using a reduce mechanism.")]
    [SKFunctionContextParameter(Name = "Input", Description = "Output of a `git diff` or `git show` command.")]
    public async Task<SKContext> GeneratePR(SKContext context)
    {
        try
        {
            var prGenerator = context.Func(SEMANTIC_FUNCTION_PATH, "PullRequestDescriptionGenerator");

            var prGeneratorCapture = context.Func(SEMANTIC_FUNCTION_PATH, "PullRequestDescriptionGenerator");
            prGeneratorCapture.SetAIService(() => new RedirectTextCompletion());
            var prompt = (await prGeneratorCapture.InvokeAsync()).Result;

            var chunkedInput = CommitChunker.ChunkCommitInfo(context.Variables.Input, CHUNK_SIZE);
            return await prGenerator.CondenseChunkProcess(this.condenseSkill, chunkedInput, prompt, context);
        }
        catch (Exception e)
        {
            return context.Fail(e.Message, e);
        }
    }

    #region MISC
    private static string PRSkillsPath()
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
            throw new Exception("Skills directory not found. The app needs the skills from the library to work.");
        }

        return path;
    }
    #endregion MISC
}

public class RedirectTextCompletion : ITextCompletion
{
    public Task<string> CompleteAsync(string text, CompleteRequestSettings requestSettings, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(text);
    }

    public IAsyncEnumerable<string> CompleteStreamAsync(string text, CompleteRequestSettings requestSettings, CancellationToken cancellationToken = default)
    {
        return AsyncEnumerable.Empty<string>(); // TODO
    }
}