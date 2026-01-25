using System.Text.Json;
using Femur.Parsing.Nodes;
using Femur.Markdown.Abstractions.Nodes;
using Femur.Markdown.Extended.Abstractions.Nodes;

namespace ExtendedMarkdownParserTests;

/// <summary>
/// Serializes an AST node tree to JSON for snapshot testing.
/// Creates a detailed, readable JSON representation of the complete node tree structure.
/// </summary>
public static class AstSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Serializes a node tree to formatted JSON string.
    /// </summary>
    public static string SerializeToJson(Node node)
    {
        var nodeData = SerializeNode(node);
        return JsonSerializer.Serialize(nodeData, JsonOptions);
    }

    /// <summary>
    /// Recursively converts a node to a dictionary representation.
    /// </summary>
    private static Dictionary<string, object?> SerializeNode(Node node)
    {
        var data = new Dictionary<string, object?>
        {
            ["nodeType"] = node.NodeType.ToString(),
            ["location"] = new Dictionary<string, object>
            {
                ["offset"] = node.Location.Offset,
                ["length"] = node.Location.Length,
                ["line"] = node.Location.Line,
                ["column"] = node.Location.Column
            }
        };

        // Add node-specific properties
        AddNodeProperties(node, data);

        // Add children if this is a parent node
        if (node is ParentNode parent && parent.HasChildren)
        {
            data["children"] = parent.Children.Select(SerializeNode).ToList();
        }

        return data;
    }

    /// <summary>
    /// Adds node-specific properties to the serialized data.
    /// </summary>
    private static void AddNodeProperties(Node node, Dictionary<string, object?> data)
    {
        switch (node)
        {
            case ExtendedMarkdownDocumentNode extendedDoc:
                // FrontMatterBlock property handled separately, but also in children
                if (extendedDoc.FrontMatterBlock != null)
                {
                    data["hasFrontMatter"] = true;
                }
                break;

            case FrontMatterBlockNode frontMatter:
                data["rawContent"] = frontMatter.RawContent;
                if (frontMatter.ParsedData != null)
                {
                    data["parsedData"] = frontMatter.ParsedData;
                }
                break;

            case HeadingNode heading:
                data["level"] = heading.Level;
                break;

            case CodeBlockNode codeBlock:
                data["content"] = codeBlock.Content;
                data["info"] = codeBlock.Info;
                data["isFenced"] = codeBlock.IsFenced;
                break;

            case FencedDivNode fencedDiv:
                data["tag"] = fencedDiv.Tag;
                data["attributes"] = fencedDiv.Attributes;
                data["rawContent"] = fencedDiv.RawContent;
                if (fencedDiv.ParsedAttributes != null)
                {
                    data["parsedAttributes"] = new Dictionary<string, object?>
                    {
                        ["id"] = fencedDiv.ParsedAttributes.Id,
                        ["classes"] = fencedDiv.ParsedAttributes.Classes.Count > 0
                            ? fencedDiv.ParsedAttributes.Classes
                            : null,
                        ["keyValueAttributes"] = fencedDiv.ParsedAttributes.KeyValueAttributes.Count > 0
                            ? fencedDiv.ParsedAttributes.KeyValueAttributes
                            : null
                    };
                }
                break;

            case MarkdownTextNode text:
                data["content"] = text.Content;
                break;

            case ListNode list:
                data["isOrdered"] = list.IsOrdered;
                if (list.IsOrdered)
                {
                    data["startNumber"] = list.StartNumber;
                }
                else
                {
                    data["bulletChar"] = list.BulletChar.ToString();
                }
                data["isLoose"] = list.IsLoose;
                break;

            case LinkNode link:
                data["url"] = link.Url;
                data["title"] = link.Title;
                break;

            case ImageNode image:
                data["url"] = image.Url;
                data["title"] = image.Title;
                // Alt text is stored as children
                break;

            case CodeSpanNode codeSpan:
                data["content"] = codeSpan.Content;
                break;

            case HtmlBlockNode htmlBlock:
                data["content"] = htmlBlock.Content;
                break;

            case ListItemNode listItem:
                // ListItemNode can contain children, no additional properties
                break;

            case ParagraphNode:
            case BlockQuoteNode:
            case EmphasisNode:
            case StrongEmphasisNode:
                // Container nodes without additional properties beyond children
                break;

            case ThematicBreakNode:
            case HardLineBreakNode:
            case SoftLineBreakNode:
                // Leaf nodes without additional properties
                break;
        }
    }
}
