using System.Buffers;
using System.Text;

namespace Femur.Parsing;

/// <summary>
/// Base class for streaming parsers that read from a Stream and build an AST.
/// Provides shared buffer management and parsing utilities.
/// 
/// Uses the Template Method pattern - subclasses implement abstract methods
/// to define parser-specific behavior while sharing common infrastructure.
/// 
/// Implements IDisposable to properly clean up the StreamReader and buffer resources.
/// </summary>
/// <typeparam name="TDocument">The document type returned by the parser</typeparam>
public abstract class StreamParser<TDocument> : IDisposable
{
    private bool _disposed;
    /// <summary>
    /// The stream reader for properly decoding UTF-8 characters
    /// </summary>
    protected StreamReader Reader { get; }

    /// <summary>
    /// Current buffer chunk from stream
    /// </summary>
    protected char[] Buffer { get; }

    /// <summary>
    /// Current position within buffer
    /// </summary>
    protected int Position { get; set; }

    /// <summary>
    /// Number of valid characters in current buffer
    /// </summary>
    protected int Length { get; set; }

    /// <summary>
    /// Total characters consumed across all buffers (for absolute positioning)
    /// </summary>
    protected int TotalCharsRead { get; set; }

    /// <summary>
    /// Reusable string builder for parsing
    /// </summary>
    protected StringBuilder StringBuilder { get; }

    /// <summary>
    /// Creates a new streaming parser for the given stream
    /// </summary>
    /// <param name="stream">The stream to parse</param>
    /// <param name="bufferSize">Size of the buffer for reading chunks (default 4096)</param>
    protected StreamParser(Stream stream, int bufferSize = 4096)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        this.Reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: bufferSize, leaveOpen: false);
        this.Buffer = ArrayPool<char>.Shared.Rent(bufferSize);
        this.StringBuilder = new StringBuilder();
    }

    /// <summary>
    /// Parses the stream and returns the document.
    /// 
    /// This is the template method that defines the parsing algorithm:
    /// 1. Create document
    /// 2. Initialize parsing state
    /// 3. Main parsing loop (process characters)
    /// 4. Cleanup
    /// </summary>
    public TDocument Parse()
    {
        var document = this.CreateDocument();

        if (!this.ReadMore())
        {
            return document;
        }

        this.InitializeParsing(document);

        // Main parsing loop
        const int maxIterations = 100_000_000; // Safety limit to prevent infinite loops (100M chars)
        var iterations = 0;

        while (true)
        {
            if (++iterations > maxIterations)
            {
                throw new InvalidOperationException($"Parse exceeded maximum iterations ({maxIterations}). Possible infinite loop - ProcessCharacter may not be advancing Position.");
            }

            if (!this.ReadMore())
            {
                break;
            }

            var ch = this.Buffer[this.Position];
            this.ProcessCharacter(ch, document);
        }

        this.Cleanup();
        return document;
    }

    /// <summary>
    /// Creates a new document instance.
    /// Must be implemented by subclasses.
    /// </summary>
    protected abstract TDocument CreateDocument();

    /// <summary>
    /// Initializes parsing state (stacks, flags, etc.).
    /// Called once before the main parsing loop.
    /// </summary>
    protected abstract void InitializeParsing(TDocument document);

    /// <summary>
    /// Processes a single character from the stream.
    /// Called for each character in the main parsing loop.
    /// </summary>
    /// <param name="ch">The character to process</param>
    /// <param name="document">The document being built</param>
    protected abstract void ProcessCharacter(char ch, TDocument document);

    /// <summary>
    /// Cleanup after parsing is complete.
    /// Called by Parse() after parsing completes.
    /// Note: This now calls Dispose(true) to ensure proper cleanup.
    /// </summary>
    protected virtual void Cleanup()
    {
        this.Dispose(true);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the StreamParser and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (this._disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose managed resources
            ArrayPool<char>.Shared.Return(this.Buffer);
            this.Reader.Dispose();
        }

        this._disposed = true;
    }

    /// <summary>
    /// Disposes the parser and releases all resources.
    /// This allows the parser to be used with 'using' statements.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Ensures buffer has data available. Reads next chunk if buffer is exhausted.
    /// 
    /// BUFFER MANAGEMENT:
    /// - Returns true immediately if buffer still has data (Position &lt; Length)
    /// - When buffer exhausted, reads next chunk from stream
    /// - Tracks total characters read for absolute position calculation
    /// - Returns false when stream is exhausted (end of file)
    /// 
    /// This method is called frequently throughout parsing to ensure data is available.
    /// It's safe to call even when buffer has data (no-op in that case).
    /// </summary>
    protected bool ReadMore()
    {
        // If we still have data in current buffer, no need to read more
        if (this.Position < this.Length)
        {
            return true;
        }

        // Buffer exhausted - read next chunk
        // Track characters consumed from previous buffer before overwriting
        this.TotalCharsRead += this.Length;

        // Read next chunk from stream (may read less than buffer size at end)
        this.Length = this.Reader.Read(this.Buffer, 0, this.Buffer.Length);
        this.Position = 0;

        // Return false if no data was read (end of stream)
        return this.Length > 0;
    }

    /// <summary>
    /// Gets the absolute character offset from the start of the stream
    /// 
    /// Calculates position accounting for:
    /// - Characters consumed from previous buffers (TotalCharsRead)
    /// - Current position within current buffer (Position)
    /// 
    /// This allows location tracking to work correctly across buffer boundaries.
    /// </summary>
    protected int GetAbsolutePosition()
    {
        return this.TotalCharsRead + this.Position;
    }

    /// <summary>
    /// Skips whitespace characters, reading more data if buffer is exhausted
    /// 
    /// Used when parsing attributes and other places where whitespace is insignificant.
    /// Stops at first non-whitespace character or end of stream.
    /// </summary>
    protected void SkipWhitespace()
    {
        const int maxIterations = 1_000_000; // Safety limit to prevent infinite loops
        var iterations = 0;

        while (this.Position < this.Length && char.IsWhiteSpace(this.Buffer[this.Position]))
        {
            if (++iterations > maxIterations)
            {
                throw new InvalidOperationException($"SkipWhitespace exceeded maximum iterations ({maxIterations}). Possible infinite loop.");
            }

            this.Position++;
            // If we've consumed buffer, try to read more
            if (this.Position >= this.Length)
            {
                _ = this.ReadMore();
            }
        }
    }

    /// <summary>
    /// Reads characters until a specific stop character is encountered
    /// 
    /// Continues reading across buffer boundaries if needed.
    /// Stops when stopChar is found (consumes it) or stream ends.
    /// </summary>
    /// <param name="stopChar">The character to stop at</param>
    /// <param name="includeStopChar">Whether to include the stop character in the result</param>
    /// <returns>The string read up to (and optionally including) the stop character</returns>
    protected string ReadUntil(char stopChar, bool includeStopChar = false)
    {
        _ = this.StringBuilder.Clear();
        const int maxIterations = 1_000_000; // Safety limit to prevent infinite loops
        var iterations = 0;

        while (this.Position < this.Length || this.ReadMore())
        {
            if (++iterations > maxIterations)
            {
                throw new InvalidOperationException($"ReadUntil exceeded maximum iterations ({maxIterations}). Possible infinite loop.");
            }

            if (this.Position >= this.Length)
            {
                break;
            }

            var ch = this.Buffer[this.Position];
            if (ch == stopChar)
            {
                if (includeStopChar)
                {
                    _ = this.StringBuilder.Append(ch);
                }

                this.Position++;
                break;
            }

            _ = this.StringBuilder.Append(ch);
            this.Position++;
        }

        return this.StringBuilder.ToString();
    }

    /// <summary>
    /// Reads characters until any of the stop characters is encountered
    /// 
    /// Returns the matched character via out parameter.
    /// Useful for parsing tag names and attributes where multiple delimiters are valid.
    /// </summary>
    /// <param name="stopChars">Array of characters to stop at</param>
    /// <param name="matchedChar">The character that was matched (or '\0' if stream ended)</param>
    /// <returns>The string read up to the matched character</returns>
    protected string ReadUntilAny(char[] stopChars, out char matchedChar)
    {
        _ = this.StringBuilder.Clear();
        matchedChar = '\0';
        const int maxIterations = 1_000_000; // Safety limit to prevent infinite loops
        var iterations = 0;

        while (this.Position < this.Length || this.ReadMore())
        {
            if (++iterations > maxIterations)
            {
                throw new InvalidOperationException($"ReadUntilAny exceeded maximum iterations ({maxIterations}). Possible infinite loop.");
            }

            if (this.Position >= this.Length)
            {
                break;
            }

            var ch = this.Buffer[this.Position];
            if (stopChars.Contains(ch))
            {
                matchedChar = ch;
                this.Position++;
                break;
            }

            _ = this.StringBuilder.Append(ch);
            this.Position++;
        }

        return this.StringBuilder.ToString();
    }
}