using Femur.Markup.Abstractions.Nodes;
using Femur.Parsing.Nodes;
using HtmlParserInstance = Femur.Html.Parser.HtmlParser;

namespace HtmlParserTests;

/// <summary>
/// Tests for OnNodeCreatedCallback functionality in HtmlParser.
/// Verifies that callbacks are invoked correctly for all node types during parsing.
/// </summary>
public class NodeCreatedCallbackTests : IClassFixture<TestFixture>, IDisposable
{
    public NodeCreatedCallbackTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Basic Callback Invocation

    [Fact]
    public void Parse_WithCallback_InvokesCallbackForEachNode()
    {
        // Arrange
        var html = "<div><p>Test</p></div>";
        var createdNodes = new List<Node>();

        // Act
        HtmlParserInstance.Parse(html, node => createdNodes.Add(node));

        // Assert
        Assert.True(createdNodes.Count >= 3); // Document, div, p, and text node
        Assert.Contains(createdNodes, n => n is DocumentNode);
        Assert.Contains(createdNodes, n => n is ElementNode e && e.TagName == "div");
        Assert.Contains(createdNodes, n => n is ElementNode e && e.TagName == "p");
    }

    [Fact]
    public void Parse_WithCallback_InvokesCallbackForTextNodes()
    {
        // Arrange
        var html = "<p>Test Content</p>";
        var textNodes = new List<TextNode>();

        // Act
        HtmlParserInstance.Parse(html, node =>
        {
            if (node is TextNode textNode)
            {
                textNodes.Add(textNode);
            }
        });

        // Assert
        Assert.NotEmpty(textNodes);
        Assert.Contains(textNodes, t => t.Content.Contains("Test Content"));
    }

    [Fact]
    public void Parse_WithCallback_InvokesCallbackForElementNodes()
    {
        // Arrange
        var html = "<div><span>Content</span></div>";
        var elementNodes = new List<ElementNode>();

        // Act
        HtmlParserInstance.Parse(html, node =>
        {
            if (node is ElementNode elementNode)
            {
                elementNodes.Add(elementNode);
            }
        });

        // Assert
        Assert.True(elementNodes.Count >= 2);
        Assert.Contains(elementNodes, e => e.TagName == "div");
        Assert.Contains(elementNodes, e => e.TagName == "span");
    }

    [Fact]
    public void Parse_WithNullCallback_DoesNotThrow()
    {
        // Arrange
        var html = "<div>Content</div>";

        // Act & Assert
        var document = HtmlParserInstance.Parse(html, null);
        Assert.NotNull(document);
    }

    #endregion

    #region Callback Order and Timing

    [Fact]
    public void Parse_WithCallback_InvokesCallbackInCreationOrder()
    {
        // Arrange
        var html = "<div><p>First</p><p>Second</p></div>";
        var creationOrder = new List<string>();

        // Act
        HtmlParserInstance.Parse(html, node =>
        {
            if (node is ElementNode e)
            {
                creationOrder.Add(e.TagName);
            }
            else if (node is DocumentNode)
            {
                creationOrder.Add("document");
            }
        });

        // Assert
        // Document should be created first
        Assert.Equal("document", creationOrder[0]);
        // Div should be created before its children
        Assert.True(creationOrder.IndexOf("div") < creationOrder.IndexOf("p"));
    }

    [Fact]
    public void Parse_WithCallback_InvokesCallbackAfterParentIsSet()
    {
        // Arrange
        var html = "<div><p>Test</p></div>";
        var nodeParentAtCallback = new List<Node?>();

        // Act
        HtmlParserInstance.Parse(html, node =>
        {
            // At callback time, parent should already be set for child nodes
            nodeParentAtCallback.Add(node.GetParent());
        });

        // Assert
        // Document node should have null parent
        Assert.Contains(nodeParentAtCallback, p => p == null);
        // Element nodes should have their parents set
        var elementCallbacks = nodeParentAtCallback.Where(p => p != null).ToList();
        Assert.NotEmpty(elementCallbacks);
    }

    #endregion

    #region Callback with Node Tracking

    [Fact]
    public void Parse_WithCallback_CanTrackAllNodes()
    {
        // Arrange
        var html = "<div><p>First</p><span>Second</span></div>";
        var allNodes = new List<Node>();
        var elementNodes = new List<ElementNode>();
        var textNodes = new List<TextNode>();

        // Act
        HtmlParserInstance.Parse(html, node =>
        {
            allNodes.Add(node);
            if (node is ElementNode e)
            {
                elementNodes.Add(e);
            }
            else if (node is TextNode t)
            {
                textNodes.Add(t);
            }
        });

        // Assert
        Assert.NotEmpty(allNodes);
        Assert.True(elementNodes.Count >= 3); // div, p, span
        Assert.True(textNodes.Count >= 2); // Text in p and span
    }

    [Fact]
    public void Parse_WithCallback_CanCollectNodeAttributes()
    {
        // Arrange
        var html = "<div class='test' id='main'><p data-value='123'>Content</p></div>";
        var attributes = new Dictionary<string, List<string>>();

        // Act
        HtmlParserInstance.Parse(html, node =>
        {
            if (node is ElementNode e && e.HasAttributes)
            {
                foreach (var attr in e.Attributes)
                {
                    if (!attributes.ContainsKey(attr.Key))
                    {
                        attributes[attr.Key] = new List<string>();
                    }
                    attributes[attr.Key].Add(attr.Value);
                }
            }
        });

        // Assert
        Assert.Contains("class", attributes.Keys);
        Assert.Contains("id", attributes.Keys);
        Assert.Contains("data-value", attributes.Keys);
        Assert.Contains("test", attributes["class"]);
        Assert.Contains("main", attributes["id"]);
        Assert.Contains("123", attributes["data-value"]);
    }

    [Fact]
    public void Parse_WithCallback_CanTrackSiblingRelationships()
    {
        // Arrange
        var html = "<div><p>First</p><p>Second</p><p>Third</p></div>";
        var siblingsByIndex = new Dictionary<int, List<Node>>();

        // Act
        HtmlParserInstance.Parse(html, node =>
        {
            var index = node.GetSiblingIndex();
            if (index >= 0)
            {
                if (!siblingsByIndex.ContainsKey(index))
                {
                    siblingsByIndex[index] = new List<Node>();
                }
                siblingsByIndex[index].Add(node);
            }
        });

        // Assert
        // Should have nodes at various sibling indices
        Assert.NotEmpty(siblingsByIndex);
    }

    #endregion

    #region Complex Document Callback Tests

    [Fact]
    public void Parse_ComplexDocument_InvokesCallbackForAllNodes()
    {
        // Arrange
        var html = @"
            <html>
                <head><title>Test</title></head>
                <body>
                    <div>
                        <p>Paragraph 1</p>
                        <p>Paragraph 2</p>
                        <span>Span content</span>
                    </div>
                </body>
            </html>";
        var nodeCount = 0;
        var elementCount = 0;
        var textCount = 0;

        // Act
        HtmlParserInstance.Parse(html, node =>
        {
            nodeCount++;
            if (node is ElementNode)
            {
                elementCount++;
            }
            else if (node is TextNode)
            {
                textCount++;
            }
        });

        // Assert
        Assert.True(nodeCount > 0);
        Assert.True(elementCount >= 6); // html, head, title, body, div, p, p, span
        Assert.True(textCount >= 3); // Text in title, paragraphs, and span
    }

    [Fact]
    public void Parse_NestedElements_InvokesCallbackForEachLevel()
    {
        // Arrange
        var html = "<div><section><article><p>Deep</p></article></section></div>";
        var depthByTag = new Dictionary<string, int>();

        // Act
        HtmlParserInstance.Parse(html, node =>
        {
            if (node is ElementNode e)
            {
                var depth = e.GetAncestors().Count();
                depthByTag[e.TagName] = depth;
            }
        });

        // Assert
        Assert.True(depthByTag["div"] < depthByTag["section"]);
        Assert.True(depthByTag["section"] < depthByTag["article"]);
        Assert.True(depthByTag["article"] < depthByTag["p"]);
    }

    #endregion

    #region Callback Error Handling

    [Fact]
    public void Parse_WithCallbackThatThrows_PropagatesException()
    {
        // Arrange
        var html = "<div>Content</div>";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            HtmlParserInstance.Parse(html, node =>
            {
                throw new InvalidOperationException("Test exception");
            });
        });
    }

    #endregion

    #region Callback Performance

    [Fact]
    public void Parse_LargeDocument_InvokesCallbackForAllNodes()
    {
        // Arrange
        var html = string.Join("", Enumerable.Range(0, 100).Select(i => $"<p>Item {i}</p>"));
        var nodeCount = 0;

        // Act
        HtmlParserInstance.Parse(html, node => nodeCount++);

        // Assert
        // Should have at least 100 paragraph elements plus text nodes
        Assert.True(nodeCount >= 100);
    }

    #endregion
}

