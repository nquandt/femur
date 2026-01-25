using Femur.Markdown.Extended.Parser;
using Femur.Markdown.Renderer;
using Xunit;

namespace MarkdownRendererTests;

/// <summary>
/// Snapshot tests for markdown HTML rendering.
/// These tests parse markdown files from the shared testfiles folder, render them to HTML,
/// and compare the resulting HTML against saved snapshots. If a snapshot doesn't exist, it will be created on the first run.
/// </summary>
public class RendererSnapshotTests
{
    private const string TestFilesBasePath = "testfiles";

    /// <summary>
    /// Gets all markdown test files from the testfiles directory.
    /// </summary>
    public static IEnumerable<object[]> GetMarkdownTestFiles()
    {
        var testFilesPath = Path.Combine(
            AppContext.BaseDirectory,
            TestFilesBasePath
        );

        if (!Directory.Exists(testFilesPath))
        {
            // If directory doesn't exist, return empty to avoid test discovery failure
            yield break;
        }

        var markdownFiles = Directory.GetFiles(
            testFilesPath,
            "document.md",
            SearchOption.AllDirectories
        );

        foreach (var filePath in markdownFiles)
        {
            // Use relative path from testfiles as the test name
            var relativePath = Path.GetRelativePath(testFilesPath, filePath);
            var testName = Path.GetDirectoryName(relativePath) ?? "root";

            yield return new object[] { filePath, testName };
        }
    }

    [Theory]
    [MemberData(nameof(GetMarkdownTestFiles))]
    public void MarkdownFile_GeneratesExpectedHtml(string markdownFilePath, string testName)
    {
        // Arrange
        var snapshotFilePath = Path.ChangeExtension(markdownFilePath, ".html");
        var markdown = File.ReadAllText(markdownFilePath);

        // Act - Parse and render to HTML
        var document = ExtendedMarkdownParser.Parse(markdown);
        var renderer = new MarkdownHtmlRenderer();
        var actualHtml = renderer.Render(document);

        // Assert
        if (!File.Exists(snapshotFilePath))
        {
            // First run - create the snapshot
            File.WriteAllText(snapshotFilePath, actualHtml);
            Assert.True(true, $"Created initial HTML snapshot for test '{testName}' at {snapshotFilePath}");
        }
        else
        {
            // Compare with existing snapshot
            var expectedHtml = File.ReadAllText(snapshotFilePath);

            Assert.Equal(
                NormalizeHtml(expectedHtml),
                NormalizeHtml(actualHtml),
                ignoreLineEndingDifferences: true,
                ignoreWhiteSpaceDifferences: false
            );
        }
    }

    /// <summary>
    /// Normalizes HTML for comparison by ensuring consistent line endings.
    /// </summary>
    private static string NormalizeHtml(string html)
    {
        return html.Replace("\r\n", "\n").TrimEnd();
    }

    [Fact]
    public void TestFilesDirectory_Exists()
    {
        var testFilesPath = Path.Combine(
            AppContext.BaseDirectory,
            TestFilesBasePath
        );

        Assert.True(
            Directory.Exists(testFilesPath),
            $"Test files directory should exist at {testFilesPath}"
        );
    }

    [Fact]
    public void TestFilesDirectory_ContainsMarkdownFiles()
    {
        var testFiles = GetMarkdownTestFiles().ToList();

        Assert.NotEmpty(testFiles);
        Assert.True(
            testFiles.Count > 0,
            "At least one document.md file should exist in the testfiles directory"
        );
    }
}
