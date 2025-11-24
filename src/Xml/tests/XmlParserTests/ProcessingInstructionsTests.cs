using Femur.Xml.Abstractions;
using XmlParserInstance = Femur.Xml.Parser.XmlParser;

namespace XmlParserTests;

public class ProcessingInstructionsTests : IClassFixture<TestFixture>, IDisposable
{
    public ProcessingInstructionsTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        // Force GC after each test to release parser resources
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }

    #region Processing Instructions

    [Fact]
    public void Parse_ProcessingInstruction_ParsesCorrectly()
    {
        var xml = "<?target data?><root></root>";
        var result = XmlParserInstance.Parse(xml);

        Assert.Equal(2, result.Children.Count);
        var pi = Assert.IsType<ProcessingInstructionNode>(result.Children[0]);
        Assert.Equal("target", pi.Target);
        Assert.Equal("data", pi.Content);
    }

    [Fact]
    public void Parse_ProcessingInstructionWithSpaces_ParsesCorrectly()
    {
        var xml = "<?target attribute=\"value\"?><root></root>";
        var result = XmlParserInstance.Parse(xml);

        var pi = Assert.IsType<ProcessingInstructionNode>(result.Children[0]);
        Assert.Equal("target", pi.Target);
        Assert.Contains("attribute", pi.Content);
        Assert.Contains("value", pi.Content);
    }

    [Fact]
    public void Parse_MultipleProcessingInstructions_ParsesAll()
    {
        var xml = "<?pi1 data1?><?pi2 data2?><root></root>";
        var result = XmlParserInstance.Parse(xml);

        Assert.Equal(3, result.Children.Count);
        _ = Assert.IsType<ProcessingInstructionNode>(result.Children[0]);
        _ = Assert.IsType<ProcessingInstructionNode>(result.Children[1]);
    }

    #endregion
}

