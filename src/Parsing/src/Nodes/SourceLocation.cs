namespace Femur.Parsing.Nodes;

/// <summary>
/// Represents a location in the source stream
/// </summary>
public struct SourceLocation : IEquatable<SourceLocation>
{
    /// <summary>
    /// The byte offset from the start of the stream
    /// </summary>
    public int Offset { get; set; }

    /// <summary>
    /// The length in bytes
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// Line number (1-based) if available
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Column number (1-based) if available
    /// </summary>
    public int Column { get; set; }

    public SourceLocation(int offset, int length, int line = 0, int column = 0)
    {
        this.Offset = offset;
        this.Length = length;
        this.Line = line;
        this.Column = column;
    }

    public override string ToString() => $"Offset: {this.Offset}, Length: {this.Length}" +
        (this.Line > 0 ? $", Line: {this.Line}, Column: {this.Column}" : string.Empty);

    public bool Equals(SourceLocation other)
    {
        return this.Offset == other.Offset &&
               this.Length == other.Length &&
               this.Line == other.Line &&
               this.Column == other.Column;
    }

    public override bool Equals(object? obj)
    {
        return obj is SourceLocation other && this.Equals(other);
    }

    public override int GetHashCode()
    {
#if NETSTANDARD2_0
        unchecked
        {
            var hash = 17;
            hash = hash * 23 + this.Offset.GetHashCode();
            hash = hash * 23 + this.Length.GetHashCode();
            hash = hash * 23 + this.Line.GetHashCode();
            hash = hash * 23 + this.Column.GetHashCode();
            return hash;
        }
#else
        return HashCode.Combine(this.Offset, this.Length, this.Line, this.Column);
#endif
    }

    public static bool operator ==(SourceLocation left, SourceLocation right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SourceLocation left, SourceLocation right)
    {
        return !left.Equals(right);
    }
}