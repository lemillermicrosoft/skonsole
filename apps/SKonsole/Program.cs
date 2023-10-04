using System.CommandLine;
using System.Text;
using Microsoft.SemanticKernel;
using SKonsole;
using SKonsole.Commands;

Console.OutputEncoding = Encoding.Unicode;

var rootCommand = new RootCommand();

rootCommand.Add(new ConfigCommand(ConfigurationProvider.Instance));
rootCommand.Add(new CommitCommand(ConfigurationProvider.Instance));
rootCommand.Add(new PRCommand(ConfigurationProvider.Instance));
rootCommand.Add(new PlannerCommand(ConfigurationProvider.Instance));
rootCommand.Add(new StepwisePlannerCommand(ConfigurationProvider.Instance));
rootCommand.Add(new TypeChatCommand(ConfigurationProvider.Instance));
rootCommand.Add(new PromptChatCommand(ConfigurationProvider.Instance));

return await rootCommand.InvokeAsync(args);
