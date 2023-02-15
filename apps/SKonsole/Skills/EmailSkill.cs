using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Registry;

namespace SKonsole.Skills;

internal class EmailSkill
{
    [SKFunction(description: "Given an e-mail and message body, send an email")]
    [SKFunctionInput(Description = "The body of the email message to send.")]
    [SKFunctionContextParameter(Name = "email_address", Description = "The email address to send email to.")]
    public Task<SKContext> SendEmail(string input, SKContext context)
    {
        context.Variables.Update($"Sent email to: {context.Variables["email_address"]}.\n\n{input}");
        return Task.FromResult(context);
    }

    [SKFunction(description: "Given a name, find email address")]
    [SKFunctionInput(Description = "The name of the person to email.")]
    public Task<SKContext> GetEmailAddress(string input, SKContext context)
    {
        context.Log.LogDebug("Returning hard coded email for {0}", input);
        context.Variables.Update("johndoe1234@example.com");
        return Task.FromResult(context);
    }
}