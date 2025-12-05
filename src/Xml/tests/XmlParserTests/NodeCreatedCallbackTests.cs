using Femur.Parsing.Nodes;
using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

/// <summary>
/// Tests for OnNodeCreatedCallback functionality in XmlParser.
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
        var xml = "<root><child>Test</child></root>";
        var createdNodes = new List<Node>();

        // Act
        XmlParserInstance.Parse(xml, node => createdNodes.Add(node));

        // Assert
        Assert.True(createdNodes.Count >= 3); // Document, root, child, and text node
        Assert.Contains(createdNodes, n => n is XmlDocumentNode);
    }

    [Fact]
    public void Parse_WithCallback_InvokesCallbackForTextNodes()
    {
        // Arrange
        var xml = "<element>Test Content</element>";
        var textNodes = new List<Femur.Markup.Abstractions.Nodes.TextNode>();

        // Act
        XmlParserInstance.Parse(xml, node =>
        {
            if (node is Femur.Markup.Abstractions.Nodes.TextNode textNode)
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
        var xml = "<root><child>Content</child></root>";
        var elementNodes = new List<Femur.Markup.Abstractions.Nodes.ElementNode>();

        // Act
        XmlParserInstance.Parse(xml, node =>
        {
            if (node is Femur.Markup.Abstractions.Nodes.ElementNode elementNode)
            {
                elementNodes.Add(elementNode);
            }
        });

        // Assert
        Assert.True(elementNodes.Count >= 2);
        Assert.Contains(elementNodes, e => e.TagName == "root");
        Assert.Contains(elementNodes, e => e.TagName == "child");
    }

    [Fact]
    public void Parse_WithNullCallback_DoesNotThrow()
    {
        // Arrange
        var xml = "<root>Content</root>";

        // Act & Assert
        var document = XmlParserInstance.Parse(xml, null);
        Assert.NotNull(document);
    }

    #endregion

    #region Callback Order and Timing

    [Fact]
    public void Parse_WithCallback_InvokesCallbackInCreationOrder()
    {
        // Arrange
        var xml = "<root><child1/><child2/></root>";
        var creationOrder = new List<string>();

        // Act
        XmlParserInstance.Parse(xml, node =>
        {
            if (node is Femur.Markup.Abstractions.Nodes.ElementNode e)
            {
                creationOrder.Add(e.TagName);
            }
            else if (node is XmlDocumentNode)
            {
                creationOrder.Add("document");
            }
        });

        // Assert
        // Document should be created first
        Assert.Equal("document", creationOrder[0]);
        // Root should be created before its children
        Assert.True(creationOrder.IndexOf("root") < creationOrder.IndexOf("child1"));
    }

    #endregion

    #region Callback with Node Tracking

    [Fact]
    public void Parse_WithCallback_CanTrackAllNodes()
    {
        // Arrange
        var xml = "<root><child1>First</child1><child2>Second</child2></root>";
        var allNodes = new List<Node>();
        var elementNodes = new List<Femur.Markup.Abstractions.Nodes.ElementNode>();
        var textNodes = new List<Femur.Markup.Abstractions.Nodes.TextNode>();

        // Act
        XmlParserInstance.Parse(xml, node =>
        {
            allNodes.Add(node);
            if (node is Femur.Markup.Abstractions.Nodes.ElementNode e)
            {
                elementNodes.Add(e);
            }
            else if (node is Femur.Markup.Abstractions.Nodes.TextNode t)
            {
                textNodes.Add(t);
            }
        });

        // Assert
        Assert.NotEmpty(allNodes);
        Assert.True(elementNodes.Count >= 3); // root, child1, child2
        Assert.True(textNodes.Count >= 2); // Text in child1 and child2
    }

    [Fact]
    public void Parse_WithCallback_CanCollectNodeAttributes()
    {
        // Arrange
        var xml = "<root attr1='value1' attr2='value2'><child id='123'/></root>";
        var attributes = new Dictionary<string, List<string>>();

        // Act
        XmlParserInstance.Parse(xml, node =>
        {
            if (node is Femur.Markup.Abstractions.Nodes.ElementNode e && e.HasAttributes)
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
        Assert.Contains("attr1", attributes.Keys);
        Assert.Contains("attr2", attributes.Keys);
        Assert.Contains("id", attributes.Keys);
    }

    #endregion

    #region Complex Document Callback Tests

    [Fact]
    public void Parse_ComplexDocument_InvokesCallbackForAllNodes()
    {
        // Arrange
        var xml = @"
            <root>
                <section>
                    <item>Item 1</item>
                    <item>Item 2</item>
                </section>
            </root>";
        var nodeCount = 0;
        var elementCount = 0;
        var textCount = 0;

        // Act
        XmlParserInstance.Parse(xml, node =>
        {
            nodeCount++;
            if (node is Femur.Markup.Abstractions.Nodes.ElementNode)
            {
                elementCount++;
            }
            else if (node is Femur.Markup.Abstractions.Nodes.TextNode)
            {
                textCount++;
            }
        });

        // Assert
        Assert.True(nodeCount > 0);
        Assert.True(elementCount >= 4); // root, section, item, item
        Assert.True(textCount >= 2); // Text in items
    }

    #endregion

    #region Callback Error Handling

    [Fact]
    public void Parse_WithCallbackThatThrows_PropagatesException()
    {
        // Arrange
        var xml = "<root>Content</root>";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            XmlParserInstance.Parse(xml, node =>
            {
                throw new InvalidOperationException("Test exception");
            });
        });
    }

    #endregion
}

