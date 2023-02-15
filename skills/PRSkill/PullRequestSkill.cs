// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using Microsoft.Extensions.Logging;
using CondenseSkillLib;
using PRSkill.Utils;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Registry;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.KernelExtensions;

namespace PRSkill;

public static class FunctionEx
{
    public static async Task<SKContext> RollingChunkProcess(this Func<SKContext, Task<SKContext>> func, List<string> chunkedInput, SKContext context)
    {
        context.Variables.Set("previousresults", string.Empty);
        foreach (var chunk in chunkedInput)
        {
            context.Variables.Update(chunk);
            context = await func(context);

            context.Variables.Set("previousresults", context.Variables.ToString());
        }

        return context;
    }

    public static async Task<SKContext> CondenseChunkProcess(this Func<SKContext, Task<SKContext>> func, CondenseSkill condenseSkill, List<string> chunkedInput, SKContext context)
    {
        var results = new List<string>();
        foreach (var chunk in chunkedInput)
        {
            context.Variables.Update(chunk);
            context = await func(context);

            results.Add(context.Variables.ToString());
        }

        if (chunkedInput.Count <= 1)
        {
            context.Variables.Update(context.Variables.ToString());
            return context;
        }

        // update memory with serialized list of results
        context.Variables.Update(string.Join(CondenseSkill.RESULTS_SEPARATOR, results));
        return await condenseSkill.Condense(context);
    }

    public static async Task<SKContext> AggregateChunkProcess(this Func<SKContext, Task<SKContext>> func, List<string> chunkedInput, SKContext context)
    {
        var results = new List<string>();
        foreach (var chunk in chunkedInput)
        {
            context.Variables.Update(chunk);
            context = await func(context);

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

            var prFeedbackGenerator = context.SFunc(SEMANTIC_FUNCTION_PATH, "PullRequestFeedbackGenerator");
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

            var commitGenerator = context.SFunc(SEMANTIC_FUNCTION_PATH, "CommitMessageGenerator");
            var chunkedInput = CommitChunker.ChunkCommitInfo(context.Variables.Input, CHUNK_SIZE);
            return await commitGenerator.CondenseChunkProcess(this.condenseSkill, chunkedInput, context);
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
            var prGenerator_Rolling = context.SFunc(SEMANTIC_FUNCTION_PATH, "PullRequestDescriptionGenerator_Rolling");
            var chunkedInput = CommitChunker.ChunkCommitInfo(context.Variables.Input, CHUNK_SIZE);
            return await prGenerator_Rolling.RollingChunkProcess(chunkedInput, context);
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
            var prGenerator = context.SFunc(SEMANTIC_FUNCTION_PATH, "PullRequestDescriptionGenerator");
            var chunkedInput = CommitChunker.ChunkCommitInfo(context.Variables.Input, CHUNK_SIZE);
            return await prGenerator.CondenseChunkProcess(this.condenseSkill, chunkedInput, context);
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
