using System.Text;
using Femur.Markdown.Abstractions;
using Femur.Markdown.Abstractions.Nodes;
using Femur.Parsing.Nodes;
using FsCheck;
using FsCheck.Xunit;
using MarkdownParserInstance = Femur.Markdown.Parser.MarkdownParser;

namespace MarkdownParserTests;

/// <summary>
/// Property-based tests for the Markdown parser using FsCheck.
/// These tests verify invariants that should hold for ANY input using randomly generated data.
///
/// Property-based testing helps discover edge cases that might not be covered by traditional
/// unit tests. FsCheck generates hundreds of test cases automatically.
/// </summary>
public class PropertyBasedTests
{
    #region Parser Safety Invariants

    [Property(MaxTest = 200)]
    public bool Parser_NeverThrows_ForAnyInput(string markdown)
    {
        // INVARIANT: Parser should never throw for any input
        // It may produce an empty document or unexpected structure, but should not crash

        try
        {
            var result = MarkdownParserInstance.Parse(markdown ?? string.Empty);
            return result != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [Property(MaxTest = 100)]
    public bool Parser_AlwaysReturnsDocumentNode(string markdown)
    {
        // INVARIANT: Parser should always return a Document node
        var result = MarkdownParserInstance.Parse(markdown ?? string.Empty);
        return result.NodeType == MarkdownNodeType.Document;
    }

    [Property(MaxTest = 100)]
    public bool Parser_EmptyOrWhitespaceInput_ReturnsEmptyDocument(string whitespace)
    {
        // INVARIANT: Empty or whitespace-only input should produce empty document
        if (string.IsNullOrWhiteSpace(whitespace))
        {
            var result = MarkdownParserInstance.Parse(whitespace ?? string.Empty);
            return result is MarkdownDocumentNode doc && doc.Children.Count == 0;
        }

        return true; // Not whitespace, skip this test case
    }

    #endregion

    #region Structural Invariants

    [Property(MaxTest = 100)]
    public bool Parser_AllParentNodes_HaveValidChildren(string markdown)
    {
        // INVARIANT: All ParentNodes should have valid children (non-null)
        var result = MarkdownParserInstance.Parse(markdown ?? string.Empty);
        return ValidateNoNullChildren(result);
    }

    private bool ValidateNoNullChildren(Node node)
    {
        if (node is ParentNode parent)
        {
            // Check that no children are null
            if (parent.Children.Any(c => c == null))
                return false;

            // Recursively check all children
            foreach (var child in parent.Children)
            {
                if (!ValidateNoNullChildren(child))
                    return false;
            }
        }

        return true;
    }

    [Property(MaxTest = 100)]
    public bool Parser_AllNodes_HaveValidParentRelationships(string markdown)
    {
        // INVARIANT: All child nodes should have their Parent property pointing to their container
        var result = MarkdownParserInstance.Parse(markdown ?? string.Empty);
        return ValidateParentRelationships(result);
    }

    private bool ValidateParentRelationships(Node node)
    {
        if (node is ParentNode parent)
        {
            foreach (var child in parent.Children)
            {
                // Child's parent should point back to this node
                if (child.GetParent() != parent)
                    return false;

                // Recursively validate
                if (!ValidateParentRelationships(child))
                    return false;
            }
        }

        return true;
    }

    #endregion

    #region Idempotency Tests

    [Property(MaxTest = 50)]
    public bool Parser_RepeatedParsing_ProducesSameStructure(string markdown)
    {
        // INVARIANT: Parsing the same input multiple times should produce the same node structure
        var result1 = MarkdownParserInstance.Parse(markdown ?? string.Empty);
        var result2 = MarkdownParserInstance.Parse(markdown ?? string.Empty);

        return NodesAreStructurallyEqual(result1, result2);
    }

    private bool NodesAreStructurallyEqual(Node node1, Node node2)
    {
        if (node1.NodeType != node2.NodeType)
            return false;

        if (node1 is ParentNode parent1 && node2 is ParentNode parent2)
        {
            if (parent1.Children.Count != parent2.Children.Count)
                return false;

            for (int i = 0; i < parent1.Children.Count; i++)
            {
                if (!NodesAreStructurallyEqual(parent1.Children[i], parent2.Children[i]))
                    return false;
            }
        }

        return true;
    }

    #endregion

    #region Node Type Invariants

    [Property(MaxTest = 100)]
    public bool Parser_Headings_HaveLevelBetween1And6(string markdown)
    {
        // INVARIANT: All heading nodes should have levels between 1 and 6
        var result = MarkdownParserInstance.Parse(markdown ?? string.Empty);
        var headings = FindAllNodes<HeadingNode>(result);

        return headings.All(h => h.Level >= 1 && h.Level <= 6);
    }

    [Property(MaxTest = 100)]
    public bool Parser_Lists_OnlyHaveListItemChildren(string markdown)
    {
        // INVARIANT: List nodes should only have ListItem children (or be empty)
        var result = MarkdownParserInstance.Parse(markdown ?? string.Empty);
        var lists = FindAllNodes<ListNode>(result);

        return lists.All(list =>
            list.Children.Count == 0 ||
            list.Children.All(child => child is ListItemNode));
    }

    [Property(MaxTest = 100)]
    public bool Parser_ValidUtf8_ParsesWithoutError(byte[] bytes)
    {
        // INVARIANT: Any valid UTF-8 sequence should parse without error
        if (bytes == null || bytes.Length == 0)
            return true;

        try
        {
            // Try to decode as UTF-8
            var markdown = System.Text.Encoding.UTF8.GetString(bytes);
            var result = MarkdownParserInstance.Parse(markdown);
            return result != null;
        }
        catch (DecoderFallbackException)
        {
            // Not valid UTF-8, that's fine
            return true;
        }
        catch (Exception)
        {
            // Parser shouldn't throw even for weird UTF-8
            return false;
        }
    }

    #endregion

    #region Helper Methods

    private List<T> FindAllNodes<T>(Node root) where T : Node
    {
        var results = new List<T>();
        FindAllNodesRecursive(root, results);
        return results;
    }

    private void FindAllNodesRecursive<T>(Node node, List<T> results) where T : Node
    {
        if (node is T typedNode)
        {
            results.Add(typedNode);
        }

        if (node is ParentNode parent)
        {
            foreach (var child in parent.Children)
            {
                FindAllNodesRecursive(child, results);
            }
        }
    }

    #endregion
}
