using System.CommandLine;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SKonsole.Utils;

namespace SKonsole.Commands;
internal sealed class ExecuteCommand : Command
{
    private ILogger _logger;

    public ExecuteCommand(ConfigurationProvider config, ILogger? logger = null) : base("exec", "Execute semantic function directly.")
    {
        if (logger is null)
        {
            using var loggerFactory = Logging.GetFactory();
            this._logger = loggerFactory.CreateLogger(this.GetType());
        }
        else
        {
            this._logger = logger;
        }

        var promptArgument = new Argument<string>
            ("prompt", "An argument that is parsed as a string.");

        this.AddArgument(promptArgument);

        var templateOption = new Option<string>(
                            new string[] { "--template", "-t" },
                            () => { return "{{$input}}"; },
                           "The template (file) to use for the semantic function.");

        this.AddOption(templateOption);

        var outputOption = new Option<string>(
                          new string[] { "--output", "-o" },
                          () => { return "{{$output}}"; },
                          "Output the result to the specified file.");
        this.AddOption(outputOption);

        this.SetHandler(async context => await RunExecuteAsync(context.GetCancellationToken(),
            context.BindingContext.ParseResult.GetValueForArgument<string>(promptArgument),
            context.BindingContext.ParseResult.GetValueForOption<string>(templateOption),
            context.BindingContext.ParseResult.GetValueForOption<string>(outputOption),
            this._logger));
    }

    private static async Task RunExecuteAsync(CancellationToken cancellationToken,
        string prompt,
        string? template = null,
        string? outputFile = null,
        ILogger? logger = null)
    {
        var kernel = KernelProvider.Instance.Get();

        if (string.IsNullOrWhiteSpace(template))
        {
            template = "{{$input}}";
        }
        else if (Path.Exists(template))
        {
            template = await File.ReadAllTextAsync(template, cancellationToken);
        }
        else if (Path.Exists(Path.Combine(template, "skprompt.txt")))
        {
            template = await File.ReadAllTextAsync(Path.Combine(template, "skprompt.txt"), cancellationToken);
        }

        var func = kernel.CreateSemanticFunction(template);

        var output = await kernel.RunAsync(prompt, func);

        var result = output.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(outputFile))
        {
            var directory = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            System.IO.File.WriteAllText(outputFile, result);
        }
        else
        {
            Console.WriteLine(result);
        }
    }
}
