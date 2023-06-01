// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Text;

namespace CondenseSkillLib;

public class CondenseSkill
{
    public static readonly string RESULTS_SEPARATOR = string.Format("\n====={0}=====\n", "EndResult");
    public const string SEMANTIC_FUNCTION_PATH = "CondenseSkill";
    private const int CHUNK_SIZE = 8000; // Eventually this should come from the kernel
    public CondenseSkill(IKernel kernel)
    {
        try
        {
            // Load semantic skill defined with prompt templates
            var folder = CondenseSkillPath();
            var condenseSkill = kernel.ImportSemanticSkillFromDirectory(folder, SEMANTIC_FUNCTION_PATH);
        }
        catch (Exception e)
        {
            throw new Exception("Failed to load skill.", e);
        }
    }

    [SKFunction(description: "Condense multiple chunks of text into a single chunk.")]
    [SKFunctionContextParameter(Name = "Input", Description = "String of text that contains multiple chunks of similar formatting, style, and tone.")]
    public async Task<SKContext> Condense(SKContext context)
    {
        try
        {
            var condenser = context.Func(SEMANTIC_FUNCTION_PATH, "Condenser");
            context.Variables.Get("separator", out var separator);

            var input = context.Variables.Input;

            List<string> lines = TextChunker.SplitPlainTextLines(input, CHUNK_SIZE / 8);
            List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(lines, CHUNK_SIZE);

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
            context.Variables.Update(string.Join("\n", condenseResult));
            context.Log.LogWarning($"Condensing {paragraphs.Count} paragraphs");
            context.Variables.Set("separator", RESULTS_SEPARATOR);
            return await Condense(context);
        }
        catch (Exception e)
        {
            return context.Fail(e.Message, e);
        }
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
