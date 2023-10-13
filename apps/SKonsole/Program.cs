using System.CommandLine;
using System.Text;
using Microsoft.SemanticKernel;
using SKonsole;
using SKonsole.Commands;

Console.OutputEncoding = Encoding.Unicode;

var rootCommand = new RootCommand(description: "SKonsole is a powerful command-line tool that leverages AI to assist you with various tasks.")
{
    new ConfigCommand(ConfigurationProvider.Instance),
    new CommitCommand(ConfigurationProvider.Instance),
    new PRCommand(ConfigurationProvider.Instance),
    new PlannerCommand(ConfigurationProvider.Instance),
    new StepwisePlannerCommand(ConfigurationProvider.Instance),
    new PromptChatCommand(ConfigurationProvider.Instance)
};

return await rootCommand.InvokeAsync(args);
