using Femur.Markup.Abstractions.Nodes;
using HtmlParserInstance = Femur.Html.Parser.HtmlParser;

namespace HtmlParserTests;

public class FormAndTableTests : IClassFixture<TestFixture>, IDisposable
{
    public FormAndTableTests(TestFixture fixture)
    {
        // Fixture ensures cleanup between tests
    }

    public void Dispose()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
    }
    #region Forms

    [Fact]
    public void Parse_Form_ParsesCorrectly()
    {
        var html = "<form action=\"/submit\" method=\"post\"></form>";
        var result = HtmlParserInstance.Parse(html);

        var form = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("form", form.TagName, ignoreCase: true);
        Assert.Equal("/submit", form.Attributes["action"]);
        Assert.Equal("post", form.Attributes["method"]);
    }

    [Fact]
    public void Parse_InputText_ParsesAsVoidElement()
    {
        var html = "<input type=\"text\" name=\"username\" value=\"test\">";
        var result = HtmlParserInstance.Parse(html);

        var input = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("input", input.TagName, ignoreCase: true);
        Assert.True(input.IsVoidElement);
        Assert.Equal("text", input.Attributes["type"]);
        Assert.Equal("username", input.Attributes["name"]);
        Assert.Equal("test", input.Attributes["value"]);
    }

    [Fact]
    public void Parse_InputCheckbox_ParsesCorrectly()
    {
        var html = "<input type=\"checkbox\" name=\"agree\" checked>";
        var result = HtmlParserInstance.Parse(html);

        var input = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("checkbox", input.Attributes["type"]);
        Assert.Equal("agree", input.Attributes["name"]);
        Assert.Equal(string.Empty, input.Attributes["checked"]); // Boolean attribute
    }

    [Fact]
    public void Parse_InputRadio_ParsesCorrectly()
    {
        var html = "<input type=\"radio\" name=\"choice\" value=\"yes\">";
        var result = HtmlParserInstance.Parse(html);

        var input = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("radio", input.Attributes["type"]);
    }

    [Fact]
    public void Parse_Textarea_ParsesCorrectly()
    {
        var html = "<textarea name=\"comment\" rows=\"5\" cols=\"40\">Default text</textarea>";
        var result = HtmlParserInstance.Parse(html);

        var textarea = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("textarea", textarea.TagName, ignoreCase: true);
        Assert.Equal("comment", textarea.Attributes["name"]);
        Assert.Equal("5", textarea.Attributes["rows"]);
        Assert.Equal("40", textarea.Attributes["cols"]);

        var text = Assert.IsType<TextNode>(textarea.Children[0]);
        Assert.Equal("Default text", text.Content);
    }

    [Fact]
    public void Parse_SelectWithOptions_ParsesCorrectly()
    {
        var html = "<select name=\"country\"><option value=\"us\">USA</option><option value=\"uk\">UK</option></select>";
        var result = HtmlParserInstance.Parse(html);

        var select = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("select", select.TagName, ignoreCase: true);
        Assert.Equal(2, select.Children.Count);

        var option1 = Assert.IsType<ElementNode>(select.Children[0]);
        Assert.Equal("option", option1.TagName, ignoreCase: true);
        Assert.Equal("us", option1.Attributes["value"]);
    }

    [Fact]
    public void Parse_OptionSelected_ParsesSelectedAttribute()
    {
        var html = "<select><option value=\"1\">One</option><option value=\"2\" selected>Two</option></select>";
        var result = HtmlParserInstance.Parse(html);

        var select = Assert.IsType<ElementNode>(result.Children[0]);
        var option2 = Assert.IsType<ElementNode>(select.Children[1]);
        Assert.Equal(string.Empty, option2.Attributes["selected"]);
    }

    #endregion

    #region Tables

    [Fact]
    public void Parse_Table_ParsesCorrectly()
    {
        var html = "<table><tr><td>Cell 1</td><td>Cell 2</td></tr></table>";
        var result = HtmlParserInstance.Parse(html);

        var table = Assert.IsType<ElementNode>(result.Children[0]);
        Assert.Equal("table", table.TagName, ignoreCase: true);

        var tr = Assert.IsType<ElementNode>(table.Children[0]);
        Assert.Equal("tr", tr.TagName, ignoreCase: true);

        Assert.Equal(2, tr.Children.Count);
        var td1 = Assert.IsType<ElementNode>(tr.Children[0]);
        Assert.Equal("td", td1.TagName, ignoreCase: true);
    }

    [Fact]
    public void Parse_TableWithHeader_ParsesTh()
    {
        var html = "<table><tr><th>Header 1</th><th>Header 2</th></tr></table>";
        var result = HtmlParserInstance.Parse(html);

        var table = Assert.IsType<ElementNode>(result.Children[0]);
        var tr = Assert.IsType<ElementNode>(table.Children[0]);
        var th = Assert.IsType<ElementNode>(tr.Children[0]);
        Assert.Equal("th", th.TagName, ignoreCase: true);
    }

    [Fact]
    public void Parse_TableWithCaption_ParsesCaption()
    {
        var html = "<table><caption>Table Title</caption><tr><td>Data</td></tr></table>";
        var result = HtmlParserInstance.Parse(html);

        var table = Assert.IsType<ElementNode>(result.Children[0]);
        var caption = Assert.IsType<ElementNode>(table.Children[0]);
        Assert.Equal("caption", caption.TagName, ignoreCase: true);
    }

    [Fact]
    public void Parse_NestedTable_ParsesCorrectly()
    {
        var html = "<table><tr><td><table><tr><td>Nested</td></tr></table></td></tr></table>";
        var result = HtmlParserInstance.Parse(html);

        var outerTable = Assert.IsType<ElementNode>(result.Children[0]);
        var outerTr = Assert.IsType<ElementNode>(outerTable.Children[0]);
        var outerTd = Assert.IsType<ElementNode>(outerTr.Children[0]);
        var innerTable = Assert.IsType<ElementNode>(outerTd.Children[0]);
        Assert.Equal("table", innerTable.TagName, ignoreCase: true);
    }

    #endregion
}
