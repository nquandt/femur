using Femur.Markdown.Extended.Parser;
using Xunit;

namespace ExtendedMarkdownParserTests;

/// <summary>
/// Snapshot tests for markdown parsing.
/// These tests parse markdown files from the shared testfiles folder and compare the resulting AST
/// against saved JSON snapshots. If a snapshot doesn't exist, it will be created on the first run.
/// </summary>
public class SnapshotTests
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
    public void MarkdownFile_GeneratesExpectedAst(string markdownFilePath, string testName)
    {
        // Arrange
        var snapshotFilePath = Path.ChangeExtension(markdownFilePath, ".ast.json");
        var markdown = File.ReadAllText(markdownFilePath);

        // Act
        var document = ExtendedMarkdownParser.Parse(markdown);
        var actualJson = AstSerializer.SerializeToJson(document);

        // Assert
        if (!File.Exists(snapshotFilePath))
        {
            // First run - create the snapshot
            File.WriteAllText(snapshotFilePath, actualJson);
            Assert.True(true, $"Created initial snapshot for test '{testName}' at {snapshotFilePath}");
        }
        else
        {
            // Compare with existing snapshot
            var expectedJson = File.ReadAllText(snapshotFilePath);

            Assert.Equal(
                NormalizeJson(expectedJson),
                NormalizeJson(actualJson),
                ignoreLineEndingDifferences: true,
                ignoreWhiteSpaceDifferences: false
            );
        }
    }

    /// <summary>
    /// Normalizes JSON for comparison by ensuring consistent line endings.
    /// </summary>
    private static string NormalizeJson(string json)
    {
        return json.Replace("\r\n", "\n").TrimEnd();
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
