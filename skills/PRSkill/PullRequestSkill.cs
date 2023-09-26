// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Reflection;
using CondenseSkillLib;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.TypeChat;
using PRSkill.Utils;

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

            context.Variables.Set("previousresults", context.Result);
        }

        return context;
    }

    public static async Task<SKContext> CondenseChunkProcess<T>(this ISKFunction func, CondenseSkill condenseSkill, List<string> chunkedInput, string prompt, SKContext context, string resultTag, IJsonTypeValidator<T>? validator = null)
    {
        var results = new List<string>();
        foreach (var chunk in chunkedInput)
        {
            context.Variables.Update(chunk);
            // todo plumb through formatting
            var prompts = CommitMessageTranslatorPrompts.Default;
            // context.Variables.Set("resultformat", "The result format should be \"<TITLE>\n\n<SUMMARY>\"");
            var formatInstructions = validator is not null ? prompts.CreateRequestPrompt(validator.Schema, new Prompt()).ToString() : "The result format should be \"<TITLE>\n\n<SUMMARY>\"";
            context.Variables.Set("resultformat", formatInstructions);
            context = await func.InvokeAsync(context);

            results.Add(context.Result);
        }

        if (chunkedInput.Count <= 1)
        {
            context.Variables.Update(context.Result);
            return context;
        }

        // update memory with serialized list of results
        context.Variables.Set("prompt", prompt);
        return await condenseSkill.Condense(context, string.Join($"\n ====={resultTag}=====\n", results) + $"\n ====={resultTag}=====\n");
    }

    public static async Task<SKContext> AggregateChunkProcess(this ISKFunction func, List<string> chunkedInput, SKContext context)
    {
        var results = new List<string>();
        foreach (var chunk in chunkedInput)
        {
            context.Variables.Update(chunk);
            context = await func.InvokeAsync(context);

            results.Add(context.Result);
        }

        context.Variables.Update(string.Join("\n", results));
        return context;
    }
}

public class PullRequestSkill
{
    public const string SEMANTIC_FUNCTION_PATH = "PRSkill";
    private const int CHUNK_SIZE = 8000; // Eventually this should come from the kernel

    private readonly CondenseSkill _condenseSkill;

    private readonly IKernel _kernel;
    private readonly ILogger _logger;

    private readonly JsonTranslator<BasicCommitMessage> _translator;
    private readonly JsonTranslator<ConventionalCommitMessage> _conventionalTranslator;
    private readonly JsonTranslator<EmojiCommitMessage> _emojiTranslator;

    public PullRequestSkill(IKernel kernel)
    {
        try
        {
            // Load semantic skill defined with prompt templates
            var folder = PRSkillsPath();
            var PRSkill = kernel.ImportSemanticSkillFromDirectory(folder, SEMANTIC_FUNCTION_PATH);
            this._condenseSkill = new CondenseSkill(kernel);

            this._kernel = Kernel.Builder
                .WithAIService<ITextCompletion>(null, new RedirectTextCompletion(), true)
                .Build();
            this._kernel.ImportSemanticSkillFromDirectory(folder, SEMANTIC_FUNCTION_PATH);

            this._translator = new JsonTranslator<BasicCommitMessage>(kernel.LanguageModel(new ModelInfo("gpt-4-32k"))); // todo why do I have to name the model?
            this._conventionalTranslator = new JsonTranslator<ConventionalCommitMessage>(kernel.LanguageModel(new ModelInfo("gpt-4-32k")));
            this._emojiTranslator = new JsonTranslator<EmojiCommitMessage>(kernel.LanguageModel(new ModelInfo("gpt-4-32k")));

            this._logger = this._kernel.LoggerFactory.CreateLogger<PullRequestSkill>();
        }
        catch (Exception e)
        {
            throw new Exception("Failed to load skill.", e);
        }
    }

    [SKFunction, Description("Generate feedback for a pull request based on a git diff or git show file output.")]
    public async Task<SKContext> GeneratePullRequestFeedback(
        [Description("Output of a `git diff` or `git show` command.")]
        string input,
        SKContext context)
    {
        this._logger.LogTrace("GeneratePullRequestFeedback called");

        var prFeedbackGenerator = context.Skills.GetFunction(SEMANTIC_FUNCTION_PATH, "PullRequestFeedbackGenerator");
        var chunkedInput = CommitChunker.ChunkCommitInfo(input, CHUNK_SIZE);
        return await prFeedbackGenerator.AggregateChunkProcess(chunkedInput, context);
    }

    [SKFunction, Description("Generate a commit message based on a git diff file output.")]
    public async Task<string> GenerateCommitMessage(
        [Description("Output of a `git diff` command.")]
        string input,
        CommitMessageType commitMessageType,
        SKContext context,
        CancellationToken cancellationToken = default)
    {
        this._logger.LogTrace("GenerateCommitMessage called");

        var commitGenerator = context.Skills.GetFunction(SEMANTIC_FUNCTION_PATH, "CommitMessageGenerator");

        var commitGeneratorCapture = this._kernel.Skills.GetFunction(SEMANTIC_FUNCTION_PATH, "CommitMessageGenerator");

        var contextVariables = new ContextVariables();
        var formatInstructions = this.GetFormatInstructions(commitMessageType);
        contextVariables.Set("resultformat", formatInstructions);
        var prompt = (await commitGeneratorCapture.InvokeAsync(contextVariables, cancellationToken: cancellationToken)).Result;

        var chunkedInput = CommitChunker.ChunkCommitInfo(input, CHUNK_SIZE);
        var result = await commitGenerator.CondenseChunkProcess(this._condenseSkill, chunkedInput, prompt, context, "CommitMessageResult", this._conventionalTranslator?.Validator);

        var validationResult = this.GetValidatedResponse(commitMessageType, result.Result);
        return validationResult;
    }

    private string GetValidatedResponse(CommitMessageType commitMessageType, string responseToParse)
    {
        Func<string, string> Validator;
        switch (commitMessageType)
        {
            case CommitMessageType.Default:
                Validator = this.GetValidationResult(this._translator);
                break;
            case CommitMessageType.Conventional:
                Validator = this.GetValidationResult(this._conventionalTranslator);
                break;
            case CommitMessageType.Emoji:
                Validator = this.GetValidationResult(this._emojiTranslator);
                break;
            default:
                throw new Exception("Invalid commit message type");
        }

        return Validator(responseToParse);
    }

    private Func<string, string> GetValidationResult<T>(JsonTranslator<T> translator)
    {
        var Validator = translator.Validator;
        var _constraintsValidator = translator.ConstraintsValidator;

        Result<T> ValidateJson(string json)
        {
            if (Validator is null)
            {
                return Result<T>.Error("No validator found.");
            }

            var result = Validator.Validate(json);

            if (result.Success)
            {
                result = (_constraintsValidator != null) ?
                         _constraintsValidator.Validate(result.Value) :
                         result;
            }
            return result;
        }

        string getValidationResult(string s)
        {
            JsonResponse jsonResponse = JsonResponse.Parse(s);
            Result<T> validationResult;
            if (jsonResponse.HasCompleteJson)
            {
                validationResult = ValidateJson(jsonResponse.Json!);
                if (validationResult.Success)
                {
                    return validationResult.Value?.ToString() ?? string.Empty; // formatter options will come into play here, maybe you want the raw json, copy/paste formatting, etc.
                }
            }
            else if (jsonResponse.HasJson)
            {
                throw new Exception("Incomplete json");
            }
            else
            {
                throw new Exception("No json");
            }

            throw new Exception("Failed to validate json");
        }

        return getValidationResult;
    }

    private string GetFormatInstructions(CommitMessageType commitMessageType)
    {
        var formatInstructions = "The result format should be \"<TITLE>\n\n<SUMMARY>\"";
        var prompts = CommitMessageTranslatorPrompts.Default;

        switch (commitMessageType)
        {
            case CommitMessageType.Default:
                if (this._translator is not null)
                {
                    formatInstructions = prompts.CreateRequestPrompt(this._translator.Validator.Schema, new Prompt()).ToString();
                }
                break;
            case CommitMessageType.Conventional:
                if (this._conventionalTranslator is not null)
                {
                    formatInstructions = prompts.CreateRequestPrompt(this._conventionalTranslator.Validator.Schema, new Prompt()).ToString();
                }
                break;
            case CommitMessageType.Emoji:
                if (this._emojiTranslator is not null)
                {
                    formatInstructions = prompts.CreateRequestPrompt(this._emojiTranslator.Validator.Schema, new Prompt()).ToString();
                }
                break;
        }

        return formatInstructions;
    }

    [SKFunction, Description("Generate a pull request description based on a git diff or git show file output using a rolling query mechanism.")]
    public async Task<SKContext> GeneratePR_Rolling(
        [Description("Output of a `git diff` or `git show` command.")]
        string input,
        SKContext context,
        CancellationToken cancellationToken = default)
    {
        var prGenerator_Rolling = context.Skills.GetFunction(SEMANTIC_FUNCTION_PATH, "PullRequestDescriptionGenerator_Rolling");
        var chunkedInput = CommitChunker.ChunkCommitInfo(input, CHUNK_SIZE);
        return await prGenerator_Rolling.RollingChunkProcess(chunkedInput, context);
    }

    [SKFunction, Description("Generate a pull request description based on a git diff or git show file output using a reduce mechanism.")]
    public async Task<SKContext> GeneratePR(
        [Description("Output of a `git diff` or `git show` command.")]
        string input,
        SKContext context,
        CancellationToken cancellationToken = default)
    {
        var prGenerator = context.Skills.GetFunction(SEMANTIC_FUNCTION_PATH, "PullRequestDescriptionGenerator");

        var prGeneratorCapture = this._kernel.Skills.GetFunction(SEMANTIC_FUNCTION_PATH, "PullRequestDescriptionGenerator");
        var contextVariablesWithoutInput = context.Variables.Clone();
        contextVariablesWithoutInput.Set("input", "");
        var prompt = (await prGeneratorCapture.InvokeAsync(variables: contextVariablesWithoutInput, cancellationToken: cancellationToken)).Result;

        var chunkedInput = CommitChunker.ChunkCommitInfo(input, CHUNK_SIZE);
        // TODO Should be a different type
        return await prGenerator.CondenseChunkProcess<BasicCommitMessage>(this._condenseSkill, chunkedInput, prompt, context, "PullRequestDescriptionResult", null);
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
    Task<IReadOnlyList<ITextResult>> ITextCompletion.GetCompletionsAsync(string text, CompleteRequestSettings requestSettings, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ITextResult>>(new List<ITextResult> { new RedirectTextCompletionResult(text) });
    }

    IAsyncEnumerable<ITextStreamingResult> ITextCompletion.GetStreamingCompletionsAsync(string text, CompleteRequestSettings requestSettings, CancellationToken cancellationToken)
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
