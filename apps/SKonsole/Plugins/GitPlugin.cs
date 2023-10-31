using System.ComponentModel;
using System.Diagnostics;
using CondensePluginLib;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Orchestration;
using PRPlugin;
using PRPlugin.Utils;
using static PRPlugin.FunctionEx;

namespace SKonsole.Plugins;

public class GitPlugin
{
    private const int CHUNK_SIZE = 8000; // Eventually this should come from the kernel

    private readonly IKernel _kernel;

    private readonly ILogger _logger;

    private readonly CondensePlugin _condensePlugin;

    private readonly Dictionary<string, ISKFunction> _functions = new();

    public GitPlugin(IKernel kernel)
    {
        this._logger = kernel.LoggerFactory.CreateLogger(this.GetType());
        this._condensePlugin = new CondensePlugin(kernel);

        this._kernel = new KernelBuilder()
            .WithAIService<ITextCompletion>(null, new RedirectTextCompletion(), true)
            .Build();

        this.ImportSemanticFunctions();
    }

    private void ImportSemanticFunctions()
    {
        var promptTemplate = @"[GITDIFFCONTENT]
{{$input}}
[END GITDIFFCONTENT]

[GITDIFFCONTENT] is part or all of the output of `git diff`.

Use[GITDIFFCONTENT] as knowledge for completing tasks.

Task:
{{$instructions}}

Result:
";
        var function = this._kernel.CreateSemanticFunction(promptTemplate, "DynamicGenerator", "DynamicResult");
        this._functions.Add("DynamicGenerator", function);
    }

    [SKFunction, Description("Run 'git diff --staged' and return it's output.")]
    public static async Task<SKContext> GitDiffStaged(SKContext context,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "diff --staged",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            }
        };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        context.Variables.Update(output);
        return context;
    }

    [SKFunction, Description("Run 'git diff' with filter, target, source options and return its output.")]
    public async Task<SKContext> GitDiffDynamic(
        SKContext context,
        [Description("The filter to apply to the diff.")]
        string filter = "-- . \":!*.md\" \":!*skprompt.txt\" \":!*encoder.json\" \":!*vocab.bpe\" \":!*dict.txt\"",
        [Description("The target commit hash.")]
        string target = "HEAD",
        [Description("The source commit hash. Default is the empty tree SHA.")]
        string source = "4b825dc642cb6eb9a060e54bf8d69288fbee4904",
        CancellationToken cancellationToken = default)
    {
        // Workaround due to inability to pass null or empty string to SKFunction via parameterized Plan
        filter = filter == "$filter" ? "-- . \":!*.md\" \":!*skprompt.txt\" \":!*encoder.json\" \":!*vocab.bpe\" \":!*dict.txt\"" : filter;
        target = target == "$target" ? "HEAD" : target;
        source = source == "$source" ? "4b825dc642cb6eb9a060e54bf8d69288fbee4904" : source;

        this._logger.LogDebug("GitDiffDynamic called:\n\tfilter:{filter}\n\ttarget:{target}\n\tsource:{source}", filter, target, source);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"diff {source}..{target} {filter}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            }
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);

        context.Variables.Update(output);
        return context;
    }

    [SKFunction, Description("Generate an output based on a git diff or git show file output for a given instruction.")]
    public async Task<SKContext> GenerateDynamic(
        [Description("Output of a `git diff` or `git show` command.")]
        string fullDiff,
        [Description("Instructions to generate a specific output.")]
        string instructions,
        SKContext context,
        CancellationToken cancellationToken = default)
    {
        this._logger.LogDebug("GenerateDynamic called:\n\tfullDiff:{fullDiff}\n\tinstructions:{instructions}", fullDiff.Substring(0, Math.Max(0, Math.Min(250, fullDiff.Length - 1))), instructions);
        var chunkedInput = CommitChunker.ChunkCommitInfo(fullDiff, CHUNK_SIZE, cancellationToken);
        return await this._functions["DynamicGenerator"].CondenseChunkProcess(this._condensePlugin, chunkedInput, instructions, context, "DynamicResult", cancellationToken);
    }
}
