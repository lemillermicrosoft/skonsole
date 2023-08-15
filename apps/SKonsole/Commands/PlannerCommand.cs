using System.CommandLine;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Skills.Web;
using Microsoft.SemanticKernel.Skills.Web.Bing;
using SKonsole.Skills;
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
        var createPlanCommand = new Command("create", "Create Plan subcommand");
        createPlanCommand.Add(messageArgument);
        createPlanCommand.SetHandler(async (messageArgumentValue) => await RunCreatePlan(CancellationToken.None, this._logger, messageArgumentValue), messageArgument);
        return createPlanCommand;
    }

    private static async Task RunCreatePlan(CancellationToken token, ILogger logger, string message = "")
    {
        var kernel = KernelProvider.Instance.Get();

        // Eventually, Kernel will be smarter about what skills it uses for an ask.
        // kernel.ImportSkill(new EmailSkill(), "email");
        // kernel.ImportSkill(new GitSkill(), "git");
        // kernel.ImportSkill(new SearchUrlSkill(), "url");
        // kernel.ImportSkill(new HttpSkill(), "http");
        // kernel.ImportSkill(new PRSkill.PullRequestSkill(kernel), "PullRequest");

        kernel.ImportSkill(new WriterSkill(kernel), "writer");
        var bingConnector = new BingConnector(Configuration.ConfigVar("BING_API_KEY"));
        var bing = new WebSearchEngineSkill(bingConnector);
        var search = kernel.ImportSkill(bing, "bing");

        // var planner = new ActionPlanner();
        var planner = new SequentialPlanner(kernel);
        var plan = await planner.CreatePlanAsync(message);

        await plan.InvokeAsync();
    }

    private readonly ILogger _logger;
}
