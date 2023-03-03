// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using CQMSkillLib.Utils;
using CQMSkillLib.Utils.Markup;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.KernelExtensions;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace CQMSkillLib;

public class CQMSkill
{
    public const string SEMANTIC_FUNCTION_PATH = "CQMSkill";

    public CQMSkill(IKernel kernel)
    {
        try
        {
            // Load semantic skill defined with prompt templates
            var folder = CQMSkillPath();
            this.SemanticFunctions = kernel.ImportSemanticSkillFromDirectory(folder, SEMANTIC_FUNCTION_PATH);
        }
        catch (Exception e)
        {
            throw new Exception("Failed to load skill.", e);
        }
    }

    public IDictionary<string, ISKFunction> SemanticFunctions { get; }

    [SKFunction(description: "Execute a markup using known registered functions when possible.")]
    [SKFunctionContextParameter(Name = "Input", Description = "Markup to execute using known registered functions when possible.")]
    public async Task<SKContext> RunMarkup(SKContext context)
    {
        var markup = new XmlMarkup(context.Variables.Input);
        var result = await CommandRuntime.RunMarkupAsync(context, markup);
        context.Variables.Update(result);

        return context;
    }

    [SKFunction(description: "Lookup a fact")]
    public Task<SKContext> Lookup(SKContext context)
    {
        var fact = context.Variables.Input;
        context.Variables.Update("i've looked up: " + fact);
        return Task.FromResult(context);
    }

    private static string CQMSkillPath()
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
