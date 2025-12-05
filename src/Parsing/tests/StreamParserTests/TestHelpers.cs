using System.Text;
using Femur.Parsing;
using Femur.Parsing.Nodes;

namespace StreamParserTests;

/// <summary>
/// Shared test helpers for StreamParser tests.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// Test implementation of StreamParser for testing base class functionality
    /// </summary>
    public class TestStreamParser : StreamParser<TestDocument>
    {
        public bool CreateDocumentCalled { get; private set; }
        public bool InitializeParsingCalled { get; private set; }
        public int ProcessCharacterCallCount { get; private set; }
        public bool CleanupCalled { get; private set; }

        public TestStreamParser(Stream stream, int bufferSize = 4096) : base(stream, bufferSize)
        {
        }

        protected override TestDocument CreateDocument()
        {
            CreateDocumentCalled = true;
            return new TestDocument();
        }

        protected override void InitializeParsing(TestDocument document)
        {
            InitializeParsingCalled = true;
        }

        protected override void ProcessCharacter(char ch, TestDocument document)
        {
            ProcessCharacterCallCount++;
            _ = document.Content.Append(ch);
            Position++; // Advance position - real parsers do this internally
        }

        protected override void Cleanup()
        {
            CleanupCalled = true;
            base.Cleanup();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CleanupCalled = true;
            }

            base.Dispose(disposing);
        }

        // Expose protected members for testing
        public new bool ReadMore() => base.ReadMore();
        public new int GetAbsolutePosition() => base.GetAbsolutePosition();
        public new void SkipWhitespace() => base.SkipWhitespace();
        public new string ReadUntil(char stopChar, bool includeStopChar = false) => base.ReadUntil(stopChar, includeStopChar);
        public new string ReadUntilAny(char[] stopChars, out char matchedChar) => base.ReadUntilAny(stopChars, out matchedChar);

        // Expose protected properties for testing
        public new char[] Buffer => base.Buffer;
        public new int Position { get => base.Position; set => base.Position = value; }
        public new int Length => base.Length;
        public new int TotalCharsRead => base.TotalCharsRead;
        public new StringBuilder StringBuilder => base.StringBuilder;
    }

    public class TestDocument : Node
    {
        public StringBuilder Content { get; } = new StringBuilder();

        public override NodeType NodeType => NodeType.Custom("TestDocument");
    }
}

