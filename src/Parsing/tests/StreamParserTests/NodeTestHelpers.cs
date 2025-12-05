using Femur.Markup.Abstractions.Nodes;
using Femur.Parsing.Nodes;

namespace StreamParserTests;

/// <summary>
/// Helper methods for creating test nodes and setting up sibling relationships.
/// </summary>
internal static class NodeTestHelpers
{
    /// <summary>
    /// Creates a parent-child relationship and updates sibling references.
    /// </summary>
    public static void AddChild(ContainerNode parent, Node child)
    {
        child.SetParent(parent);
        parent.Children.Add(child);
        UpdateSiblingReferences(parent);
    }

    /// <summary>
    /// Updates sibling references for all children in a container.
    /// Uses reflection to access the internal UpdateSiblingReferences method.
    /// </summary>
    private static void UpdateSiblingReferences(ContainerNode container)
    {
        var method = typeof(ContainerNode).GetMethod(
            "UpdateSiblingReferences",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(container, null);
    }

    /// <summary>
    /// Creates an element node with the specified tag name.
    /// </summary>
    public static ElementNode CreateElement(string tagName)
    {
        return new ElementNode { TagName = tagName };
    }

    /// <summary>
    /// Creates a text node with the specified content.
    /// </summary>
    public static TextNode CreateText(string content)
    {
        return new TextNode { Content = content };
    }

    /// <summary>
    /// Creates a document node.
    /// </summary>
    public static DocumentNode CreateDocument()
    {
        return new DocumentNode();
    }
}

