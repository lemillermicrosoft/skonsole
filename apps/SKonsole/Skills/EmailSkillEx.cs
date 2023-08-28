using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Orchestration;

namespace SKonsole.Skills;

internal static class EmailSkillEx
{
    public static ILogger Logger(this SKContext context)
    {
        return context.LoggerFactory.CreateLogger<EmailSkill>();
    }
}
