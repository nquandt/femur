using Femur.Markup.Abstractions.Nodes;

namespace Femur.Xml.Abstractions;

/// <summary>
/// XML element node.
/// XML elements are case-sensitive and all tags must be closed.
/// </summary>
public class XmlElementNode : ElementNode
{
    /// <summary>
    /// XML namespace prefix (e.g., "ns" in "ns:element")
    /// </summary>
    public string? NamespacePrefix { get; set; }

    /// <summary>
    /// XML namespace URI (from xmlns:prefix="uri" or xmlns="uri")
    /// </summary>
    public string? NamespaceUri { get; set; }

    /// <summary>
    /// Local name without prefix (e.g., "element" in "ns:element")
    /// </summary>
    public string LocalName => this.NamespacePrefix != null
        ? this.TagName.Substring(this.NamespacePrefix.Length + 1)
        : this.TagName;

    /// <summary>
    /// Qualified name (prefix:localname or just localname)
    /// </summary>
    public string QualifiedName => this.NamespacePrefix != null
        ? $"{this.NamespacePrefix}:{this.LocalName}"
        : this.LocalName;
}