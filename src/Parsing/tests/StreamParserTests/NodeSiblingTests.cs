using Femur.Markup.Abstractions;
using Femur.Markup.Abstractions.Nodes;

namespace StreamParserTests;

/// <summary>
/// Tests for Node sibling navigation methods: GetNextSibling, GetPreviousSibling, GetSiblingIndex,
/// GetAncestors, GetSiblings, and GetElementSiblings.
/// These tests use manually constructed nodes to avoid parser dependencies.
/// </summary>
public class NodeSiblingTests : IClassFixture<TestFixture>, IDisposable
{
    public NodeSiblingTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region GetNextSibling Tests

    [Fact]
    public void GetNextSibling_FirstChild_ReturnsNextSibling()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var first = NodeTestHelpers.CreateElement("p");
        var second = NodeTestHelpers.CreateElement("p");
        var third = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, first);
        NodeTestHelpers.AddChild(parent, second);
        NodeTestHelpers.AddChild(parent, third);

        // Act
        var nextSibling = first.GetNextSibling();

        // Assert
        Assert.NotNull(nextSibling);
        Assert.Equal(second, nextSibling);
    }

    [Fact]
    public void GetNextSibling_LastChild_ReturnsNull()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var first = NodeTestHelpers.CreateElement("p");
        var last = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, first);
        NodeTestHelpers.AddChild(parent, last);

        // Act
        var nextSibling = last.GetNextSibling();

        // Assert
        Assert.Null(nextSibling);
    }

    [Fact]
    public void GetNextSibling_SingleChild_ReturnsNull()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var child = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, child);

        // Act
        var nextSibling = child.GetNextSibling();

        // Assert
        Assert.Null(nextSibling);
    }

    [Fact]
    public void GetNextSibling_RootNode_ReturnsNull()
    {
        // Arrange
        var document = NodeTestHelpers.CreateDocument();

        // Act
        var nextSibling = document.GetNextSibling();

        // Assert
        Assert.Null(nextSibling);
    }

    [Fact]
    public void GetNextSibling_MixedNodeTypes_ReturnsNextSibling()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var textNode = NodeTestHelpers.CreateText("Text");
        var elementNode = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, textNode);
        NodeTestHelpers.AddChild(parent, elementNode);

        // Act
        var nextSibling = textNode.GetNextSibling();

        // Assert
        Assert.NotNull(nextSibling);
        Assert.Equal(elementNode, nextSibling);
    }

    #endregion

    #region GetPreviousSibling Tests

    [Fact]
    public void GetPreviousSibling_SecondChild_ReturnsPreviousSibling()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var first = NodeTestHelpers.CreateElement("p");
        var second = NodeTestHelpers.CreateElement("p");
        var third = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, first);
        NodeTestHelpers.AddChild(parent, second);
        NodeTestHelpers.AddChild(parent, third);

        // Act
        var previousSibling = second.GetPreviousSibling();

        // Assert
        Assert.NotNull(previousSibling);
        Assert.Equal(first, previousSibling);
    }

    [Fact]
    public void GetPreviousSibling_FirstChild_ReturnsNull()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var first = NodeTestHelpers.CreateElement("p");
        var second = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, first);
        NodeTestHelpers.AddChild(parent, second);

        // Act
        var previousSibling = first.GetPreviousSibling();

        // Assert
        Assert.Null(previousSibling);
    }

    [Fact]
    public void GetPreviousSibling_SingleChild_ReturnsNull()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var child = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, child);

        // Act
        var previousSibling = child.GetPreviousSibling();

        // Assert
        Assert.Null(previousSibling);
    }

    [Fact]
    public void GetPreviousSibling_RootNode_ReturnsNull()
    {
        // Arrange
        var document = NodeTestHelpers.CreateDocument();

        // Act
        var previousSibling = document.GetPreviousSibling();

        // Assert
        Assert.Null(previousSibling);
    }

    #endregion

    #region GetSiblingIndex Tests

    [Fact]
    public void GetSiblingIndex_FirstChild_ReturnsZero()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var first = NodeTestHelpers.CreateElement("p");
        var second = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, first);
        NodeTestHelpers.AddChild(parent, second);

        // Act
        var index = first.GetSiblingIndex();

        // Assert
        Assert.Equal(0, index);
    }

    [Fact]
    public void GetSiblingIndex_SecondChild_ReturnsOne()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var first = NodeTestHelpers.CreateElement("p");
        var second = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, first);
        NodeTestHelpers.AddChild(parent, second);

        // Act
        var index = second.GetSiblingIndex();

        // Assert
        Assert.Equal(1, index);
    }

    [Fact]
    public void GetSiblingIndex_MultipleChildren_ReturnsCorrectIndex()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var first = NodeTestHelpers.CreateElement("p");
        var second = NodeTestHelpers.CreateElement("p");
        var third = NodeTestHelpers.CreateElement("p");
        var fourth = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, first);
        NodeTestHelpers.AddChild(parent, second);
        NodeTestHelpers.AddChild(parent, third);
        NodeTestHelpers.AddChild(parent, fourth);

        // Act
        var index = third.GetSiblingIndex();

        // Assert
        Assert.Equal(2, index);
    }

    [Fact]
    public void GetSiblingIndex_RootNode_ReturnsNegativeOne()
    {
        // Arrange
        var document = NodeTestHelpers.CreateDocument();

        // Act
        var index = document.GetSiblingIndex();

        // Assert
        Assert.Equal(-1, index);
    }

    [Fact]
    public void GetSiblingIndex_MixedNodeTypes_ReturnsCorrectIndex()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var textNode = NodeTestHelpers.CreateText("Text");
        var elementNode = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, textNode);
        NodeTestHelpers.AddChild(parent, elementNode);

        // Act
        var index = elementNode.GetSiblingIndex();

        // Assert
        Assert.Equal(1, index);
    }

    #endregion

    #region GetAncestors Tests

    [Fact]
    public void GetAncestors_DeeplyNestedNode_ReturnsAllAncestors()
    {
        // Arrange
        var div = NodeTestHelpers.CreateElement("div");
        var section = NodeTestHelpers.CreateElement("section");
        var article = NodeTestHelpers.CreateElement("article");
        var p = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(div, section);
        NodeTestHelpers.AddChild(section, article);
        NodeTestHelpers.AddChild(article, p);

        // Act
        var ancestors = p.GetAncestors().ToList();

        // Assert
        Assert.Equal(3, ancestors.Count);
        Assert.Equal(article, ancestors[0]);
        Assert.Equal(section, ancestors[1]);
        Assert.Equal(div, ancestors[2]);
    }

    [Fact]
    public void GetAncestors_DirectChild_ReturnsSingleAncestor()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var child = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, child);

        // Act
        var ancestors = child.GetAncestors().ToList();

        // Assert
        Assert.Single(ancestors);
        Assert.Equal(parent, ancestors[0]);
    }

    [Fact]
    public void GetAncestors_RootNode_ReturnsEmpty()
    {
        // Arrange
        var document = NodeTestHelpers.CreateDocument();

        // Act
        var ancestors = document.GetAncestors().ToList();

        // Assert
        Assert.Empty(ancestors);
    }

    [Fact]
    public void GetAncestors_TextNode_ReturnsElementAncestors()
    {
        // Arrange
        var div = NodeTestHelpers.CreateElement("div");
        var p = NodeTestHelpers.CreateElement("p");
        var textNode = NodeTestHelpers.CreateText("Text");
        NodeTestHelpers.AddChild(div, p);
        NodeTestHelpers.AddChild(p, textNode);

        // Act
        var ancestors = textNode.GetAncestors().ToList();

        // Assert
        Assert.Equal(2, ancestors.Count);
        Assert.Equal(p, ancestors[0]);
        Assert.Equal(div, ancestors[1]);
    }

    #endregion

    #region GetSiblings Tests

    [Fact]
    public void GetSiblings_MultipleSiblings_ReturnsAllSiblings()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var first = NodeTestHelpers.CreateElement("p");
        var second = NodeTestHelpers.CreateElement("p");
        var third = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, first);
        NodeTestHelpers.AddChild(parent, second);
        NodeTestHelpers.AddChild(parent, third);

        // Act
        var siblings = second.GetSiblings().ToList();

        // Assert
        Assert.Equal(3, siblings.Count);
        Assert.All(siblings, s => Assert.NotNull(s));
        Assert.Contains(first, siblings);
        Assert.Contains(second, siblings);
        Assert.Contains(third, siblings);
    }

    [Fact]
    public void GetSiblings_SingleChild_ReturnsSingleSibling()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var child = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, child);

        // Act
        var siblings = child.GetSiblings().ToList();

        // Assert
        Assert.Single(siblings);
        Assert.Equal(child, siblings[0]);
    }

    [Fact]
    public void GetSiblings_RootNode_ReturnsEmpty()
    {
        // Arrange
        var document = NodeTestHelpers.CreateDocument();

        // Act
        var siblings = document.GetSiblings().ToList();

        // Assert
        Assert.Empty(siblings);
    }

    [Fact]
    public void GetSiblings_MixedNodeTypes_ReturnsAllSiblings()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var textNode1 = NodeTestHelpers.CreateText("Text");
        var elementNode = NodeTestHelpers.CreateElement("p");
        var textNode2 = NodeTestHelpers.CreateText("More Text");
        NodeTestHelpers.AddChild(parent, textNode1);
        NodeTestHelpers.AddChild(parent, elementNode);
        NodeTestHelpers.AddChild(parent, textNode2);

        // Act
        var siblings = elementNode.GetSiblings().ToList();

        // Assert
        Assert.Equal(3, siblings.Count);
        Assert.IsType<TextNode>(siblings[0]);
        Assert.IsType<ElementNode>(siblings[1]);
        Assert.IsType<TextNode>(siblings[2]);
    }

    [Fact]
    public void GetSiblings_IncludesSelf_ReturnsSelfInCollection()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var first = NodeTestHelpers.CreateElement("p");
        var second = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, first);
        NodeTestHelpers.AddChild(parent, second);

        // Act
        var siblings = second.GetSiblings().ToList();

        // Assert
        Assert.Contains(second, siblings);
    }

    #endregion

    #region GetElementSiblings Tests

    [Fact]
    public void GetElementSiblings_AllElementSiblings_ReturnsOnlyElements()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var first = NodeTestHelpers.CreateElement("p");
        var second = NodeTestHelpers.CreateElement("p");
        var third = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, first);
        NodeTestHelpers.AddChild(parent, second);
        NodeTestHelpers.AddChild(parent, third);

        // Act
        var elementSiblings = second.GetElementSiblings().ToList();

        // Assert
        Assert.Equal(3, elementSiblings.Count);
        Assert.All(elementSiblings, s => Assert.IsType<ElementNode>(s));
    }

    [Fact]
    public void GetElementSiblings_MixedNodeTypes_FiltersToElementsOnly()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var textNode = NodeTestHelpers.CreateText("Text");
        var p = NodeTestHelpers.CreateElement("p");
        var span = NodeTestHelpers.CreateElement("span");
        NodeTestHelpers.AddChild(parent, textNode);
        NodeTestHelpers.AddChild(parent, p);
        NodeTestHelpers.AddChild(parent, span);

        // Act
        var elementSiblings = p.GetElementSiblings().ToList();

        // Assert
        Assert.Equal(2, elementSiblings.Count);
        Assert.All(elementSiblings, s => Assert.IsType<ElementNode>(s));
        Assert.Contains(elementSiblings, s => ((ElementNode)s).TagName == "p");
        Assert.Contains(elementSiblings, s => ((ElementNode)s).TagName == "span");
    }

    [Fact]
    public void GetElementSiblings_OnlyTextNodes_ReturnsEmpty()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var textNode1 = NodeTestHelpers.CreateText("Text One");
        var textNode2 = NodeTestHelpers.CreateText("Text Two");
        NodeTestHelpers.AddChild(parent, textNode1);
        NodeTestHelpers.AddChild(parent, textNode2);

        // Act
        var elementSiblings = textNode1.GetElementSiblings().ToList();

        // Assert
        Assert.Empty(elementSiblings);
    }

    [Fact]
    public void GetElementSiblings_SingleElement_ReturnsSingleElement()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var child = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, child);

        // Act
        var elementSiblings = child.GetElementSiblings().ToList();

        // Assert
        Assert.Single(elementSiblings);
        Assert.Equal(child, elementSiblings[0]);
    }

    [Fact]
    public void GetElementSiblings_RootNode_ReturnsEmpty()
    {
        // Arrange
        var document = NodeTestHelpers.CreateDocument();

        // Act
        var elementSiblings = document.GetElementSiblings().ToList();

        // Assert
        Assert.Empty(elementSiblings);
    }

    [Fact]
    public void GetElementSiblings_IncludesSelf_ReturnsSelfInCollection()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var first = NodeTestHelpers.CreateElement("p");
        var second = NodeTestHelpers.CreateElement("p");
        var third = NodeTestHelpers.CreateElement("span");
        NodeTestHelpers.AddChild(parent, first);
        NodeTestHelpers.AddChild(parent, second);
        NodeTestHelpers.AddChild(parent, third);

        // Act
        var elementSiblings = second.GetElementSiblings().ToList();

        // Assert
        Assert.Contains(second, elementSiblings);
        Assert.Equal(3, elementSiblings.Count);
    }

    #endregion

    #region Integration Tests - Sibling Navigation

    [Fact]
    public void SiblingNavigation_CanTraverseForward()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var first = NodeTestHelpers.CreateElement("p");
        var second = NodeTestHelpers.CreateElement("p");
        var third = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, first);
        NodeTestHelpers.AddChild(parent, second);
        NodeTestHelpers.AddChild(parent, third);

        // Act - Traverse forward using GetNextSibling
        var next1 = first.GetNextSibling();
        var next2 = next1?.GetNextSibling();
        var next3 = next2?.GetNextSibling();

        // Assert
        Assert.NotNull(next1);
        Assert.NotNull(next2);
        Assert.Equal(second, next1);
        Assert.Equal(third, next2);
        Assert.Null(next3);
    }

    [Fact]
    public void SiblingNavigation_CanTraverseBackward()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var first = NodeTestHelpers.CreateElement("p");
        var second = NodeTestHelpers.CreateElement("p");
        var third = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, first);
        NodeTestHelpers.AddChild(parent, second);
        NodeTestHelpers.AddChild(parent, third);

        // Act - Traverse backward using GetPreviousSibling
        var prev1 = third.GetPreviousSibling();
        var prev2 = prev1?.GetPreviousSibling();
        var prev3 = prev2?.GetPreviousSibling();

        // Assert
        Assert.NotNull(prev1);
        Assert.NotNull(prev2);
        Assert.Equal(second, prev1);
        Assert.Equal(first, prev2);
        Assert.Null(prev3);
    }

    [Fact]
    public void SiblingIndex_MatchesActualIndex()
    {
        // Arrange
        var parent = NodeTestHelpers.CreateElement("div");
        var first = NodeTestHelpers.CreateElement("p");
        var second = NodeTestHelpers.CreateElement("p");
        var third = NodeTestHelpers.CreateElement("p");
        NodeTestHelpers.AddChild(parent, first);
        NodeTestHelpers.AddChild(parent, second);
        NodeTestHelpers.AddChild(parent, third);

        // Act & Assert
        for (int i = 0; i < parent.Children.Count; i++)
        {
            var child = parent.Children[i];
            Assert.Equal(i, child.GetSiblingIndex());
        }
    }

    #endregion
}
