using System.CommandLine;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Planners;
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
            this._logger = loggerFactory.CreateLogger<PlannerCommand>();
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

        // Eventually, Kernel will be smarter about what plugins it uses for an ask.
        // kernel.ImportFunctions(new EmailPlugin(), "email");
        // kernel.ImportFunctions(new GitPlugin(), "git");
        // kernel.ImportFunctions(new SearchUrlPlugin(), "url");
        // kernel.ImportFunctions(new HttpPlugin(), "http");
        // kernel.ImportFunctions(new PRPlugin.PullRequestPlugin(kernel), "PullRequest");

        kernel.ImportFunctions(new WriterPlugin(kernel), "writer");
        var bingConnector = new BingConnector(Configuration.ConfigVar("BING_API_KEY"));
        var bing = new WebSearchEnginePlugin(bingConnector);
        var search = kernel.ImportFunctions(bing, "bing");

        // var planner = new ActionPlanner();
        var planner = new SequentialPlanner(kernel);
        var plan = await planner.CreatePlanAsync(message);

        await kernel.RunAsync(plan);
    }

    private readonly ILogger _logger;
}
