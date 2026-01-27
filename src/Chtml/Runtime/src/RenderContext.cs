

using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Femur.Chtml.Runtime;


/// <summary>
/// Struct-based context object passed to all render methods for zero-allocation rendering.
/// Contains both the writer and global properties as structs/references (no heap allocation).
/// Supports tuple deconstruction: var (writer, globalProps) = renderContext;
/// </summary>
/// <typeparam name="TGlobalProps">The global props type.</typeparam>
public struct RenderContext<TGlobalProps> : IEquatable<RenderContext<TGlobalProps>>
    where TGlobalProps : class
{
    public MetaWriter Writer { get; }
    public TGlobalProps GlobalProps { get; }

    public RenderContext(MetaWriter writer, TGlobalProps globalProps)
    {
        this.Writer = writer;
        this.GlobalProps = globalProps;
    }

    /// <summary>
    /// Enables tuple deconstruction: var (writer, globalProps) = renderContext;
    /// </summary>
    public void Deconstruct(out MetaWriter writer, out TGlobalProps globalProps)
    {
        writer = this.Writer;
        globalProps = this.GlobalProps;
    }

    public override bool Equals(object? obj)
    {
        return obj is RenderContext<TGlobalProps> other && this.Equals(other);
    }

    public bool Equals(RenderContext<TGlobalProps> other)
    {
        return this.Writer.Equals(other.Writer) && ReferenceEquals(this.GlobalProps, other.GlobalProps);
    }

    public override int GetHashCode()
    {
        return System.HashCode.Combine(this.Writer, this.GlobalProps);
    }

    public static bool operator ==(RenderContext<TGlobalProps> left, RenderContext<TGlobalProps> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(RenderContext<TGlobalProps> left, RenderContext<TGlobalProps> right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Helper method to create a RenderContext from a PipeWriter and GlobalProps.
    /// Uses struct-based MetaWriter for zero-allocation rendering.
    /// </summary>
    public static RenderContext<TGlobalProps> Create(PipeWriter pipeWriter, TGlobalProps globalProps)
    {
        var writer = new MetaWriter(pipeWriter);
        return new RenderContext<TGlobalProps>(writer, globalProps);
    }

    /// <summary>
    /// Static helper to render with a RenderPipe, similar to MetaWriter.RenderAsync.
    /// </summary>
    public static ValueTask RenderAsync(PipeWriter pipeWriter, TGlobalProps globalProps, RenderPipe<TGlobalProps> renderPipe)
    {
        var context = Create(pipeWriter, globalProps);
        return renderPipe(context);
    }
}

