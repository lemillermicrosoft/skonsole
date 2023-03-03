using System.Text;
using System.Xml;
using System.Xml.XPath;
// using Microsoft.SemanticKernel.Orchestration;

// namespace Microsoft.AI.CommandRuntime.Dispatcher.Markup;
namespace CQMSkillLib.Utils.Markup;

public static class XmlEx
{
    public static bool HasChildElements(this XmlNode elt)
    {
        if (!elt.HasChildNodes)
        {
            return false;
        }

        var childNodes = elt.ChildNodes;
        for (int i = 0, count = childNodes.Count; i < count; ++i)
        {
            if (childNodes[i].NodeType == XmlNodeType.Element)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Walks the Markup DOM using an XPathNavigator, allowing recursive descent WITHOUT requiring a Stack Hit
    /// This is safe for very large and highly nested documents.
    /// </summary>
    /// <param name="node">The node to start from</param>
    /// <param name="maxStackDepth">The maximum stack depth to allow. 32 is a good default.</param>
    public static IEnumerable<XmlNodeInfo> EnumerateNodes(this XmlNode node, int maxStackDepth = 32)
    {
        var nav = node.CreateNavigator();
        return EnumerateNodes(nav, maxStackDepth);
    }

    public static IEnumerable<XmlNodeInfo> EnumerateNodes(this XmlDocument doc, int maxStackDepth = 32)
    {
        var nav = doc.CreateNavigator();
        nav.MoveToRoot();
        return EnumerateNodes(nav, maxStackDepth);
    }

    public static IEnumerable<XmlNodeInfo> EnumerateNodes(this XPathNavigator nav, int maxStackDepth = 32)
    {
        var info = new XmlNodeInfo
        {
            StackDepth = 0
        };
        var hasChildren = nav.HasChildren;
        while (true)
        {
            info.Parent = (XmlNode)nav.UnderlyingObject;
            if (hasChildren && info.StackDepth < maxStackDepth)
            {
                nav.MoveToFirstChild();
                info.StackDepth++;
            }
            else
            {
                bool hasParent;
                while (hasParent = nav.MoveToParent())
                {
                    info.StackDepth--;
                    if (info.StackDepth == 0)
                    {
                        hasParent = false;
                        break;
                    }

                    if (nav.MoveToNext())
                    {
                        break;
                    }
                }

                if (!hasParent)
                {
                    break;
                }
            }

            do
            {
                info.Node = (XmlNode)nav.UnderlyingObject;
                yield return info;
                if (hasChildren = nav.HasChildren)
                {
                    break;
                }
            } while (nav.MoveToNext());
        }
    }

    public static IEnumerable<XmlNodeInfo> EnumerateElements(this XmlNode node, int maxStackDepth = 32)
    {
        return from info in EnumerateNodes(node, maxStackDepth)
               where info.Node.NodeType == XmlNodeType.Element
               select info;
    }

    public static IEnumerable<XmlNodeInfo> EnumerateElements(this XmlDocument doc, int maxStackDepth = 32)
    {
        return from info in EnumerateNodes(doc, maxStackDepth)
               where info.Node.NodeType == XmlNodeType.Element
               select info;
    }

    // public static ISKFunction ToCommand(this XmlNode node, SKContext context, StringBuilder sb, bool includeInput)
    // {
    //     var cmdInput = includeInput ? node.InnerText : null;
    //     var attributes = node.Attributes;
    //     if (string.IsNullOrEmpty(cmdInput) && (attributes == null || attributes.Count == 0))
    //     {
    //         context.IsFunctionRegistered("", node.LocalName, out var func);
    //         return func;
    //     }

    //     sb.Append(node.LocalName);
    //     if (!string.IsNullOrEmpty(cmdInput))
    //     {
    //         // sb.Append(CommandParser.Terminals.WordBreak);
    //         sb.Append(" ");
    //         sb.Append(cmdInput);
    //     }

    //     attributes.ToArgs(sb);
    //     return sb.ToString();//AndClear();
    // }

    // public static void ToArgs(this XmlAttributeCollection attributes, StringBuilder sb)
    // {
    //     for (int i = 0, count = attributes.Count; i < count; ++i)
    //     {
    //         var attrib = attributes[i];
    //         if (sb.Length > 0)
    //         {
    //             sb.Append(CommandParser.Terminals.WordBreak);
    //         }

    //         var name = attrib.Name;
    //         var value = attrib.Value;
    //         if (!name.IsNullOrEmpty() && !value.IsNullOrEmpty())
    //         {
    //             sb.Append(CommandParser.Terminals.ArgNamePrefix);
    //             sb.Append(attrib.Name);
    //             sb.Append(CommandParser.Terminals.WordBreak);
    //             sb.AppendQuoted(attrib.Value);
    //         }
    //         else
    //         {
    //             sb.AppendQuoted(name);
    //         }
    //     }
    // }
}
