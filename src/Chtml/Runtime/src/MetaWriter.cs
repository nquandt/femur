using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Femur.Chtml.Runtime;


/// <summary>
/// Struct version of MetaWriter for zero-allocation rendering.
/// Contains only value types and a reference to PipeWriter.
/// </summary>
public struct MetaWriter : IEquatable<MetaWriter>
{
    private int _indentLevel;
    private readonly PipeWriter _writer;

    public MetaWriter(PipeWriter writer)
    {
        this._indentLevel = 0;
        this._writer = writer;
    }

    public void Write(ReadOnlySpan<char> text)
    {
        this._writer.Write(text);
    }

    public void WriteNewLine()
    {
        this._writer.Write("\n");
    }

    public void WriteWhitespace(int spaces)
    {
        if (spaces <= 0)
        {
            return;
        }
        // Optimize: use stackalloc for small spaces to avoid allocation
        if (spaces <= 20)
        {
            Span<char> buffer = stackalloc char[spaces];
            buffer.Fill(' ');
            this._writer.Write(buffer);
        }
        else
        {
            this._writer.Write(new string(' ', spaces));
        }
    }

    public void WriteIndented(ReadOnlySpan<char> text, int indent = 0)
    {
        if (indent < 0)
        {
            this.Unindent();
        }

        for (var i = 0; i < this._indentLevel; i++)
        {
            this._writer.Write("  ");
        }

        this._writer.Write(text);
        if (indent > 0)
        {
            this.Indent();
        }
    }

    public ValueTask WriteAsync(ReadOnlySpan<char> text)
    {
        this.Write(text);
        return ValueTask.CompletedTask;
    }

    public void Indent(int levels = 1)
    {
        this._indentLevel += levels;
    }

    public void Unindent(int levels = 1)
    {
        this._indentLevel -= levels;
    }

    public override bool Equals(object? obj)
    {
        return obj is MetaWriter other && this.Equals(other);
    }

    public bool Equals(MetaWriter other)
    {
        return this._indentLevel == other._indentLevel && ReferenceEquals(this._writer, other._writer);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(this._indentLevel, this._writer);
    }

    public static bool operator ==(MetaWriter left, MetaWriter right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(MetaWriter left, MetaWriter right)
    {
        return !left.Equals(right);
    }
}
