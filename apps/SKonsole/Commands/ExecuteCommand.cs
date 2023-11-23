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
                           "The template to use for the semantic function.");

        this.AddOption(templateOption);

        this.SetHandler(async context => await RunExecuteAsync(context.GetCancellationToken(),
            context.BindingContext.ParseResult.GetValueForArgument<string>(promptArgument),
            context.BindingContext.ParseResult.GetValueForOption<string>(templateOption),
            this._logger));
    }

    private static async Task RunExecuteAsync(CancellationToken cancellationToken,
        string prompt,
        string? template = null,
        ILogger? logger = null)
    {
        var kernel = KernelProvider.Instance.Get();

        if (string.IsNullOrWhiteSpace(template))
        {
            template = "{{$input}}";
        }

        var func = kernel.CreateSemanticFunction(template);

        var output = await kernel.RunAsync(prompt, func);

        var result = output.GetValue<string>();

        Console.WriteLine(result);
    }
}
