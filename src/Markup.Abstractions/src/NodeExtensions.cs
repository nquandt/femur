using Femur.Markup.Abstractions.Nodes;
using Femur.Parsing.Nodes;

namespace Femur.Markup.Abstractions;

/// <summary>
/// Extension methods for Node that provide sibling navigation functionality.
/// These methods are defined in Markup.Abstractions to avoid circular dependencies
/// between Parsing and Markup.Abstractions.
/// </summary>
public static class NodeExtensions
{
    /// <summary>
    /// Gets all sibling nodes (including this node) in the parent's children list.
    /// </summary>
    /// <param name="node">The node to get siblings for</param>
    /// <returns>All siblings, or empty enumerable if this node has no parent</returns>
    public static IEnumerable<Node> GetSiblings(this Node node)
    {
        if (node.GetParent() == null)
        {
            return Enumerable.Empty<Node>();
        }

        // Use ContainerNode pattern if available
        return node.GetParent() is ContainerNode container
            ? container.Children
            : Enumerable.Empty<Node>();
    }

    /// <summary>
    /// Gets all sibling element nodes (including this node if it's an element) in the parent's children list.
    /// Filters siblings to only return ElementNode instances.
    /// </summary>
    /// <param name="node">The node to get element siblings for</param>
    /// <returns>All element siblings, or empty enumerable if this node has no parent</returns>
    public static IEnumerable<ElementNode> GetElementSiblings(this Node node)
    {
        return node.GetSiblings().OfType<ElementNode>();
    }
}
