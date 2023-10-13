// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Reflection;
using CondensePluginLib.Tokenizers;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Text;

namespace CondensePluginLib;

public class CondensePlugin
{
    public static readonly string RESULTS_SEPARATOR = string.Format("\n====={0}=====\n", "EndResult");
    public const string SEMANTIC_FUNCTION_PATH = "CondensePlugin";
    private const int CHUNK_SIZE = 8000; // Eventually this should come from the kernel
    private readonly ILogger _logger;
    public CondensePlugin(IKernel kernel)
    {
        try
        {
            // Load semantic plugin defined with prompt templates
            var folder = CondensePluginPath();
            var condensePlugin = kernel.ImportSemanticFunctionsFromDirectory(folder, SEMANTIC_FUNCTION_PATH);
            this._logger = kernel.LoggerFactory.CreateLogger(this.GetType());
        }
        catch (Exception e)
        {
            throw new Exception("Failed to load plugin.", e);
        }
    }

    [SKFunction, Description("Condense multiple chunks of text into a single chunk.")]
    public async Task<SKContext> Condense(
        SKContext context,
        [Description("String of text that contains multiple chunks of similar formatting, style, and tone.")]
        string input,
        [Description("Separator to use between chunks.")]
        string separator = "",
        CancellationToken cancellationToken = default)
    {
        var condenser = context.Functions.GetFunction(SEMANTIC_FUNCTION_PATH, "Condenser");
        cancellationToken.ThrowIfCancellationRequested();
        List<string> lines = TextChunker.SplitPlainTextLines(input, CHUNK_SIZE / 8, EnglishRobertaTokenizer.Counter);
        List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(lines, CHUNK_SIZE, 100, tokenCounter: EnglishRobertaTokenizer.Counter);

        var condenseResult = new List<string>();
        foreach (var paragraph in paragraphs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Variables.Update(paragraph + separator);
            var result = await context.Runner.RunAsync(condenser, context.Variables, cancellationToken: cancellationToken);
            condenseResult.Add(result.GetValue<string>());
        }

        if (paragraphs.Count <= 1)
        {
            return context;
        }

        // update memory with serialized list of results and call condense again
        this._logger.LogWarning($"Condensing {paragraphs.Count} paragraphs");
        return await this.Condense(context, string.Join("\n", condenseResult), RESULTS_SEPARATOR, cancellationToken);
    }

    private static string CondensePluginPath()
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
}
