using System.Xml;

// namespace Microsoft.AI.CommandRuntime.Dispatcher.Markup;
namespace CQMSkillLib.Utils.Markup;

public class XmlMarkup
{
    public XmlMarkup(TextReader response)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }
        this.Document = LoadXml(response);
    }

    public XmlMarkup(string response, string wrapperTag = null)
    {
        if (!string.IsNullOrEmpty(wrapperTag))
        {
            response = $"<{wrapperTag}>{response}</{wrapperTag}>";
        }

        this.Document = new XmlDocument();
        this.Document.LoadXml(response);
    }

    public XmlDocument Document { get; }

    public XmlNode Root => this.Document.DocumentElement;

    public IEnumerable<XmlNodeInfo> EnumerateElements()
    {
        return this.Document.EnumerateElements();
    }

    public XmlNodeList SelectElements()
    {
        return this.Document.SelectNodes("//*");
    }

    public XmlNodeList SelectElements(string tag)
    {
        return this.Document.GetElementsByTagName(tag);
    }

    public string GetText()
    {
        return this.Document.InnerXml;
    }

    public void ReplaceWithText(XmlNode elt, string text)
    {
        var parent = elt.ParentNode;
        parent.InsertAfter(this.Document.CreateTextNode(text), elt);
        parent.RemoveChild(elt);
    }

    public static implicit operator XmlDocument(XmlMarkup doc)
    {
        return doc.Document;
    }

    public static implicit operator XmlNode(XmlMarkup doc)
    {
        return doc.Document;
    }

    public static XmlDocument LoadXml(TextReader reader)
    {
        // Sometimes random chars in stream.. scan ahead skipping as XmlDocument
        // loader does not like
        var ch = -1;
        while ((ch = reader.Peek()) != '<' && ch != -1)
        {
            reader.Read();
        }

        var doc = new XmlDocument();
        doc.Load(reader);
        return doc;
    }
}
