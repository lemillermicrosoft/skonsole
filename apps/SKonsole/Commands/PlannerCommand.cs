using System.CommandLine;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning.Handlebars;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using SKonsole.Plugins;
using SKonsole.Utils;

namespace SKonsole.Commands;

public class PlannerCommand : Command
{
    public PlannerCommand(ConfigurationProvider config, ILogger? logger = null) : base("planning", "skonsole planning command")
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

        this.Add(this.GenerateCreatePlanCommand());
        this.SetHandler(async context => await RunCreatePlan(context.GetCancellationToken(), this._logger));
    }

    private Command GenerateCreatePlanCommand()
    {
        var messageArgument = new Argument<string>
            ("message", "An argument that is parsed as a string.");
        var createPlanCommand = new Command("create", "Create Plan subcommand")
        {
            messageArgument
        };
        createPlanCommand.SetHandler(async (messageArgumentValue) => await RunCreatePlan(CancellationToken.None, this._logger, messageArgumentValue), messageArgument);
        return createPlanCommand;
    }

    private static async Task RunCreatePlan(CancellationToken token, ILogger logger, string message = "")
    {
        var kernel = KernelProvider.Instance.Get();

        kernel.ImportPluginFromObject(new WriterPlugin(kernel), "writer");
        var bingConnector = new BingConnector(Configuration.ConfigVar("BING_API_KEY"));
        var bing = new WebSearchEnginePlugin(bingConnector);
        var search = kernel.ImportPluginFromObject(bing, "bing");

        // var planner = new ActionPlanner();
        var planner = new HandlebarsPlanner();
        var plan = await planner.CreatePlanAsync(kernel, message, token);

        plan.Invoke(kernel, new KernelArguments(), token);
    }

    private readonly ILogger _logger;
}
