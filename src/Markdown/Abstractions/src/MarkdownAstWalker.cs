using Femur.Parsing.Nodes;
using Femur.Markdown.Abstractions.Nodes;

namespace Femur.Markdown.Abstractions;

/// <summary>
/// Base class for walking a Markdown AST.
/// Provides traversal logic and visitor methods for each node type.
/// Subclasses can override specific visitor methods to implement custom behavior.
/// </summary>
public abstract class MarkdownAstWalker
{
    /// <summary>
    /// Walks the AST starting from the given document node.
    /// </summary>
    public void Walk(MarkdownDocumentNode document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        this.VisitDocument(document);
    }

    /// <summary>
    /// Visits a document node and walks its children.
    /// </summary>
    protected virtual void VisitDocument(MarkdownDocumentNode node)
    {
        this.WalkChildren(node);
    }

    /// <summary>
    /// Visits a heading node and walks its children.
    /// </summary>
    protected virtual void VisitHeading(HeadingNode node)
    {
        this.WalkChildren(node);
    }

    /// <summary>
    /// Visits a paragraph node and walks its children.
    /// </summary>
    protected virtual void VisitParagraph(ParagraphNode node)
    {
        this.WalkChildren(node);
    }

    /// <summary>
    /// Visits a block quote node and walks its children.
    /// </summary>
    protected virtual void VisitBlockQuote(BlockQuoteNode node)
    {
        this.WalkChildren(node);
    }

    /// <summary>
    /// Visits a code block node.
    /// </summary>
    protected virtual void VisitCodeBlock(CodeBlockNode node)
    {
        // Leaf node - no children to walk
    }

    /// <summary>
    /// Visits a list node and walks its children.
    /// </summary>
    protected virtual void VisitList(ListNode node)
    {
        this.WalkChildren(node);
    }

    /// <summary>
    /// Visits a list item node and walks its children.
    /// </summary>
    protected virtual void VisitListItem(ListItemNode node)
    {
        this.WalkChildren(node);
    }

    /// <summary>
    /// Visits a thematic break node.
    /// </summary>
    protected virtual void VisitThematicBreak(ThematicBreakNode node)
    {
        // Leaf node - no children to walk
    }

    /// <summary>
    /// Visits an HTML block node.
    /// </summary>
    protected virtual void VisitHtmlBlock(HtmlBlockNode node)
    {
        // Leaf node - no children to walk
    }

    /// <summary>
    /// Visits a fenced div node and walks its children.
    /// </summary>
    protected virtual void VisitFencedDiv(FencedDivNode node)
    {
        this.WalkChildren(node);
    }

    /// <summary>
    /// Visits an emphasis node and walks its children.
    /// </summary>
    protected virtual void VisitEmphasis(EmphasisNode node)
    {
        this.WalkChildren(node);
    }

    /// <summary>
    /// Visits a strong emphasis node and walks its children.
    /// </summary>
    protected virtual void VisitStrongEmphasis(StrongEmphasisNode node)
    {
        this.WalkChildren(node);
    }

    /// <summary>
    /// Visits a link node and walks its children.
    /// </summary>
    protected virtual void VisitLink(LinkNode node)
    {
        this.WalkChildren(node);
    }

    /// <summary>
    /// Visits an image node and walks its children.
    /// </summary>
    protected virtual void VisitImage(ImageNode node)
    {
        this.WalkChildren(node);
    }

    /// <summary>
    /// Visits a code span node.
    /// </summary>
    protected virtual void VisitCodeSpan(CodeSpanNode node)
    {
        // Leaf node - no children to walk
    }

    /// <summary>
    /// Visits a hard line break node.
    /// </summary>
    protected virtual void VisitHardLineBreak(HardLineBreakNode node)
    {
        // Leaf node - no children to walk
    }

    /// <summary>
    /// Visits a soft line break node.
    /// </summary>
    protected virtual void VisitSoftLineBreak(SoftLineBreakNode node)
    {
        // Leaf node - no children to walk
    }

    /// <summary>
    /// Visits a text node.
    /// </summary>
    protected virtual void VisitText(MarkdownTextNode node)
    {
        // Leaf node - no children to walk
    }

    /// <summary>
    /// Walks all children of a container node.
    /// </summary>
    protected void WalkChildren(MarkdownContainerNode container)
    {
        if (container == null)
        {
            return;
        }

        foreach (var child in container.Children)
        {
            this.VisitNode(child);
        }
    }

    /// <summary>
    /// Routes to the appropriate visitor method based on the node type.
    /// </summary>
    protected virtual void VisitNode(Node node)
    {
        if (node == null)
        {
            return;
        }

        // Route to specific visitor method based on node type
        var nodeType = node.NodeType;
        if (nodeType == MarkdownNodeType.Document)
        {
            this.VisitDocument((MarkdownDocumentNode)node);
        }
        else if (nodeType == MarkdownNodeType.Heading)
        {
            this.VisitHeading((HeadingNode)node);
        }
        else if (nodeType == MarkdownNodeType.Paragraph)
        {
            this.VisitParagraph((ParagraphNode)node);
        }
        else if (nodeType == MarkdownNodeType.BlockQuote)
        {
            this.VisitBlockQuote((BlockQuoteNode)node);
        }
        else if (nodeType == MarkdownNodeType.CodeBlock)
        {
            this.VisitCodeBlock((CodeBlockNode)node);
        }
        else if (nodeType == MarkdownNodeType.List)
        {
            this.VisitList((ListNode)node);
        }
        else if (nodeType == MarkdownNodeType.ListItem)
        {
            this.VisitListItem((ListItemNode)node);
        }
        else if (nodeType == MarkdownNodeType.ThematicBreak)
        {
            this.VisitThematicBreak((ThematicBreakNode)node);
        }
        else if (nodeType == MarkdownNodeType.HtmlBlock)
        {
            this.VisitHtmlBlock((HtmlBlockNode)node);
        }
        else if (nodeType == MarkdownNodeType.FencedDiv)
        {
            this.VisitFencedDiv((FencedDivNode)node);
        }
        else if (nodeType == MarkdownNodeType.Emphasis)
        {
            this.VisitEmphasis((EmphasisNode)node);
        }
        else if (nodeType == MarkdownNodeType.StrongEmphasis)
        {
            this.VisitStrongEmphasis((StrongEmphasisNode)node);
        }
        else if (nodeType == MarkdownNodeType.Link)
        {
            this.VisitLink((LinkNode)node);
        }
        else if (nodeType == MarkdownNodeType.Image)
        {
            this.VisitImage((ImageNode)node);
        }
        else if (nodeType == MarkdownNodeType.CodeSpan)
        {
            this.VisitCodeSpan((CodeSpanNode)node);
        }
        else if (nodeType == MarkdownNodeType.HardLineBreak)
        {
            this.VisitHardLineBreak((HardLineBreakNode)node);
        }
        else if (nodeType == MarkdownNodeType.SoftLineBreak)
        {
            this.VisitSoftLineBreak((SoftLineBreakNode)node);
        }
        else if (nodeType == NodeType.Text)
        {
            this.VisitText((MarkdownTextNode)node);
        }
        else
        {
            // Unknown node type - try to walk children if it's a container
            if (node is MarkdownContainerNode container)
            {
                this.WalkChildren(container);
            }
        }
    }
}