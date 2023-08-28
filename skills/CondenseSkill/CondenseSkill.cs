// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.Tokenizers;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Text;

namespace CondenseSkillLib;

public class CondenseSkill
{
    public static readonly string RESULTS_SEPARATOR = string.Format("\n====={0}=====\n", "EndResult");
    public const string SEMANTIC_FUNCTION_PATH = "CondenseSkill";
    private const int CHUNK_SIZE = 8000; // Eventually this should come from the kernel
    private readonly ILogger _logger;
    public CondenseSkill(IKernel kernel)
    {
        try
        {
            // Load semantic skill defined with prompt templates
            var folder = CondenseSkillPath();
            var condenseSkill = kernel.ImportSemanticSkillFromDirectory(folder, SEMANTIC_FUNCTION_PATH);
            this._logger = kernel.LoggerFactory.CreateLogger<CondenseSkill>();
        }
        catch (Exception e)
        {
            throw new Exception("Failed to load skill.", e);
        }
    }

    [SKFunction, Description("Condense multiple chunks of text into a single chunk.")]
    public async Task<SKContext> Condense(
        SKContext context,
        [Description("String of text that contains multiple chunks of similar formatting, style, and tone.")]
        string input,
        [Description("Separator to use between chunks.")]
        string separator = "")
    {
        var condenser = context.Skills.GetFunction(SEMANTIC_FUNCTION_PATH, "Condenser");

        List<string> lines = TextChunker.SplitPlainTextLines(input, CHUNK_SIZE / 8, TokenCounter);
        List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(lines, CHUNK_SIZE, 100, TokenCounter);

        var condenseResult = new List<string>();
        foreach (var paragraph in paragraphs)
        {
            context.Variables.Update(paragraph + separator);
            context = await condenser.InvokeAsync(context);
            condenseResult.Add(context.Result);
        }

        if (paragraphs.Count <= 1)
        {
            return context;
        }

        // update memory with serialized list of results and call condense again
        this._logger.LogWarning($"Condensing {paragraphs.Count} paragraphs");
        return await Condense(context, string.Join("\n", condenseResult), RESULTS_SEPARATOR);
    }

    private static int TokenCounter(string input)
    {
        var tokens = GPT3Tokenizer.Encode(input);

        return tokens.Count;
    }

    private static string CondenseSkillPath()
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
}
