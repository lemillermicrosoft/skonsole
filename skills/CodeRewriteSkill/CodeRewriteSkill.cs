// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using System.Text.Json;
using CodeRewriteSkillLib.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.KernelExtensions;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace CodeRewriteSkillLib;

public class CodeRewriteSkill
{
    // public static readonly string RESULTS_SEPARATOR = string.Format("\n====={0}=====\n", "EndResult");
    public const string SEMANTIC_FUNCTION_PATH = "CodeRewriteSkill";

    public CodeRewriteSkill(IKernel kernel)
    {
        try
        {
            // Load semantic skill defined with prompt templates
            var folder = CodeRewriteSkillPath();
            var codeRewriteSkill = kernel.ImportSemanticSkillFromDirectory(folder, SEMANTIC_FUNCTION_PATH);
        }
        catch (Exception e)
        {
            throw new Exception("Failed to load skill.", e);
        }
    }

    [SKFunction(description: "Rewrite the following C# file in TypeScript and follow the same structure and logic as the original file.")]
    [SKFunctionContextParameter(Name = "Input", Description = "Path to the C# file to be rewritten in TypeScript.")]
    public async Task<SKContext> CSharpToTypescript(SKContext context)
    {
        try
        {
            var rewrite = context.Func(SEMANTIC_FUNCTION_PATH, "Rewrite");

            // File name: {{$filename}}
            // Namespace: {{$namespace}}
            // Imports: {{$imports}}
            // Class name: {{$classname}}
            // Method signatures:
            // {{$input}}

            // Get the file from input and parse out the above details like filename, namespace, etc.
            // Then, use the details to generate the TypeScript code.
            // Finally, return the generated code as the output.

            var inputFilePath = context.Variables.Input;
            var inputFileName = Path.GetFileName(inputFilePath);

            // read contents of file
            var input = File.ReadAllText(inputFilePath);

            // use code chunker to get the metadata like namespace, imports and then class/interfance + methods
            var chunks = CodeChunker.ChunkCodeFile(input, 8000); // TODO: use the chunk size from the config

            string result = string.Empty;

            // loop through chunks
            foreach (var chunk in chunks)
            {
                // TODO -- get context for imports that are local to the project?

                SKContext chunkContext = new(new ContextVariables(chunk.methodChunk), context.Memory, context.Skills, context.Log, context.CancellationToken);
                chunkContext.Variables.Set("namespace", chunk._namespace);
                chunkContext.Variables.Set("filename", inputFileName);
                chunkContext.Variables.Set("classname", chunk.className);
                chunkContext.Variables.Set("imports", chunk.imports);

                var rewrittenCode = await rewrite.InvokeAsync(chunkContext);
                result += rewrittenCode.Result;
            }

            context.Variables.Update(result);
            return context;
        }
        catch (Exception e)
        {
            context.Log.LogWarning(e, "Failed to rewrite the code.");
            return context.Fail(e.Message, e);
        }
    }

    [SKFunction(description: "Find all files in the current directory and its subdirectories.")]
    [SKFunctionContextParameter(Name = "Input", Description = "The path to the directory to search.")]
    public Task<SKContext> FindAllFiles(SKContext context)
    {
        var rootPath = context.Variables.Input;
        var files = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories);

        context.Variables.Update(JsonSerializer.Serialize(files));
        return Task.FromResult(context);
    }

    private static string CodeRewriteSkillPath()
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
