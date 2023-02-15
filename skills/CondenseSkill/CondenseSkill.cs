// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.KernelExtensions;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Registry;

namespace CondenseSkillLib;

public class CondenseSkill
{
    public static readonly string RESULTS_SEPARATOR = string.Format("\n====={0}=====\n", "EndResult");
    public const string SEMANTIC_FUNCTION_PATH = "CondenseSkill";

    public CondenseSkill(IKernel kernel)
    {
        try
        {
            // Load semantic skill defined with prompt templates
            var folder = CondenseSkillPath();
            var prSkill = kernel.ImportSemanticSkillFromDirectory(folder, SEMANTIC_FUNCTION_PATH);
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
            var condenser = context.SFunc(SEMANTIC_FUNCTION_PATH, "Condenser");

            // TODO After we can get max tokens frm semantic functions, chunk the input.
            context.Variables.Update(context.Variables.Input + RESULTS_SEPARATOR);
            return await condenser(context);
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
