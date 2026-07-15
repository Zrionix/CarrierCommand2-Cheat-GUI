using System.Text;
using System.Xml;

namespace CC2CheatGUI.Core;

/// <summary>
/// Loads and saves Carrier Command 2's XML.
///
/// A CC2 <c>save.xml</c> is NOT a well-formed single-rooted document: it is an XML
/// *fragment* — an <c>&lt;?xml?&gt;</c> declaration followed by several sibling top-level
/// elements (<c>&lt;meta/&gt;</c>, <c>&lt;scene&gt;</c>, <c>&lt;vehicles&gt;</c>,
/// <c>&lt;missiles&gt;</c>). <see cref="XmlDocument.Load(string)"/> throws
/// "There are multiple root elements" on every real save, which is why the original tool
/// could not open any of them.
///
/// We wrap the body in a synthetic root so it parses as one document, then serialize with a
/// writer that reproduces the game's own formatting byte-for-byte (verified identical on real
/// saves): single-quoted attribute values that contain <c>"</c>, no space before <c>/&gt;</c>,
/// literal tabs/newlines kept inside attribute values, and only <c>&amp; &lt; &gt;</c> escaped.
///
/// The same class parses the nested inventory document stored (escaped) inside a vehicle's
/// <c>state</c> attribute — that inner blob is a single-rooted doc with its own declaration.
/// </summary>
public sealed class Cc2Document
{
    private const string WrapperName = "__cc2_wrapper__";

    /// <summary>The verbatim <c>&lt;?xml ... ?&gt;</c> declaration, or "" if none.</summary>
    public string Declaration { get; }

    /// <summary>The backing document. Its root is a synthetic wrapper; real content are its children.</summary>
    public XmlDocument Xml { get; }

    private readonly XmlElement _wrapper;

    private Cc2Document(string declaration, XmlDocument xml, XmlElement wrapper)
    {
        Declaration = declaration;
        Xml = xml;
        _wrapper = wrapper;
    }

    public static Cc2Document Load(string path) =>
        Parse(File.ReadAllText(path, new UTF8Encoding(false)));

    public static Cc2Document Parse(string text)
    {
        string declaration = "";
        string body = text;

        int declStart = text.IndexOf("<?xml", StringComparison.Ordinal);
        if (declStart >= 0)
        {
            int declEnd = text.IndexOf("?>", declStart, StringComparison.Ordinal);
            if (declEnd >= 0)
            {
                declaration = text.Substring(0, declEnd + 2);
                body = text.Substring(declEnd + 2);
            }
        }

        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml($"<{WrapperName}>{body}</{WrapperName}>");
        return new Cc2Document(declaration, doc, doc.DocumentElement!);
    }

    /// <summary>The real top-level nodes (the wrapper's children), in document order.</summary>
    public IEnumerable<XmlNode> TopLevelNodes => _wrapper.ChildNodes.Cast<XmlNode>();

    /// <summary>Element from which whole-tree walks should start (the synthetic wrapper).</summary>
    public XmlElement Root => _wrapper;

    public XmlNodeList? SelectNodes(string xpath) => _wrapper.SelectNodes(xpath);

    public XmlElement CreateElement(string name) => Xml.CreateElement(name);

    /// <summary>Serialize back to CC2's exact on-disk format.</summary>
    public string ToXmlString()
    {
        var sb = new StringBuilder(Math.Max(1024, Declaration.Length + 64));
        sb.Append(Declaration);
        foreach (XmlNode child in _wrapper.ChildNodes)
            WriteNode(child, sb);
        return sb.ToString();
    }

    public void Save(string path) =>
        File.WriteAllText(path, ToXmlString(), new UTF8Encoding(false));

    // -----------------------------------------------------------------
    // Faithful serializer (matches the game's writer, verified on real saves)
    // -----------------------------------------------------------------

    public static void WriteNode(XmlNode node, StringBuilder sb)
    {
        switch (node.NodeType)
        {
            case XmlNodeType.Element:
                var el = (XmlElement)node;
                sb.Append('<').Append(el.Name);
                foreach (XmlAttribute a in el.Attributes)
                {
                    sb.Append(' ').Append(a.Name).Append('=');
                    WriteAttributeValue(a.Value, sb);
                }
                if (el.IsEmpty)
                {
                    sb.Append("/>");
                }
                else
                {
                    sb.Append('>');
                    foreach (XmlNode child in el.ChildNodes) WriteNode(child, sb);
                    sb.Append("</").Append(el.Name).Append('>');
                }
                break;

            case XmlNodeType.Text:
                AppendEscapedText(node.Value ?? "", sb);
                break;

            case XmlNodeType.Whitespace:
            case XmlNodeType.SignificantWhitespace:
                sb.Append(node.Value);
                break;

            case XmlNodeType.Comment:
                sb.Append("<!--").Append(node.Value).Append("-->");
                break;

            case XmlNodeType.CDATA:
                sb.Append("<![CDATA[").Append(node.Value).Append("]]>");
                break;

            case XmlNodeType.XmlDeclaration:
                sb.Append("<?xml ").Append(node.Value).Append("?>");
                break;
        }
    }

    private static void WriteAttributeValue(string value, StringBuilder sb)
    {
        // The game single-quotes any attribute whose value contains a double quote (e.g. the
        // escaped-XML "state" blobs), so those inner quotes stay literal instead of &quot;.
        char delim = value.Contains('"') && !value.Contains('\'') ? '\'' : '"';
        sb.Append(delim);
        foreach (char c in value)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append(delim == '"' ? "&quot;" : "\""); break;
                case '\'': sb.Append(delim == '\'' ? "&apos;" : "'"); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append(delim);
    }

    private static void AppendEscapedText(string value, StringBuilder sb)
    {
        foreach (char c in value)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                default: sb.Append(c); break;
            }
        }
    }
}
