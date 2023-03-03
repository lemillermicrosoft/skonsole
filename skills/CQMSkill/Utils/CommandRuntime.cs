using System.Text;
using CQMSkillLib.Utils.Markup;
using Microsoft.SemanticKernel.Orchestration;

namespace CQMSkillLib.Utils;

public static class CommandRuntime
{
    public static async Task<string> RunMarkupAsync(SKContext parentContext, XmlMarkup doc)
    {
        // Verify.NotNull(doc, nameof(doc));
        if (doc == null)
        {
            throw new ArgumentNullException(nameof(doc));
        }

        var result = parentContext;
        var nodes = doc.SelectElements();
        if (nodes.Count == 0)
        {
            return result.Variables.Input;
        }

        // using (var pooledContext = this.Buffers.AllocContext())
        // {
        // using (var pooledSb = this.Buffers.AllocStringBuilder())
        // {
        //     CommandContext context = pooledContext;
        SKContext context = parentContext;
        // StringBuilder sb = pooledSb;
        // StringBuilder sb = new();
        for (var i = 0; i < nodes.Count; ++i)
        {
            var node = nodes[i];
            if (!context.Skills.HasFunction(node.LocalName)) // TODO what about planner stuff
            // if (!this.Dispatchers.CanHandle(node.LocalName))
            {
                continue;
            }

            // var command = node.ToCommand(context, sb, false);
            context.IsFunctionRegistered("", node.LocalName, out var command);
            var hasChildElements = node.HasChildElements();
            var cmdInput = hasChildElements ? node.InnerXml : node.InnerText;
            // context.Init(parentContext, command, cmdInput);
            context.Variables.Update(cmdInput);
            result = await command.InvokeAsync(context);
            // if (result.IsAbort)
            // {
            //     break;
            // }

            if (hasChildElements)
            {
                continue;
            }

            doc.ReplaceWithText(node, context.Result);
        }
        //     }
        // }

        // return result;
        return doc.Document.InnerXml;
    }
}