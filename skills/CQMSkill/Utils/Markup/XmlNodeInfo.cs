using System.Xml;

// namespace Microsoft.AI.CommandRuntime.Dispatcher.Markup;
namespace CQMSkillLib.Utils.Markup;

public struct XmlNodeInfo
{
    public int StackDepth;
    public XmlNode Parent;
    public XmlNode Node;

    public static implicit operator XmlNode(XmlNodeInfo info)
    {
        return info.Node;
    }
}
