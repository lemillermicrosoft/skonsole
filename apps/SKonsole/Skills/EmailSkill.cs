using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace SKonsole.Skills;

internal sealed class EmailSkill
{
    [SKFunction, Description("Given an e-mail and message body, send an email")]
    public static Task<SKContext> SendEmail(
        [Description("The body of the email message to send.")]
        string input,
        [Description("The email address to send email to.")]
        string email_address,
        SKContext context,
        CancellationToken cancellationToken = default)
    {
        context.Variables.Update($"Sent email to: {email_address}.\n\n{input}");
        return Task.FromResult(context);
    }

    [SKFunction, Description("Given a name, find email address")]
    public static Task<SKContext> GetEmailAddress([Description("The name of the person to email.")] string input, SKContext context,
        CancellationToken cancellationToken = default)
    {
        context.Logger().LogDebug("Returning hard coded email for {input}", input);
        context.Variables.Update("johndoe1234@example.com");
        return Task.FromResult(context);
    }
}
