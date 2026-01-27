using System;
using System.IO.Pipelines;
using System.Text;

namespace Femur.Chtml.Runtime;

public static class PipeWriterExtensions
{

    public static void Write(this PipeWriter pipeWriter, ReadOnlySpan<char> text)
    {
        var encoding = Encoding.UTF8;

        var minimumByteSize = GetEncodingMaxByteSize(encoding);

        var encodedLength = encoding.GetByteCount(text);
        var destination = pipeWriter.GetSpan(minimumByteSize);

        if (encodedLength <= destination.Length)
        {
            // Just call Encoding.GetBytes if everything will fit into a single segment.
            var bytesWritten = encoding.GetBytes(text, destination);
            pipeWriter.Advance(bytesWritten);
        }
        else
        {
            WriteMultiSegmentEncoded(pipeWriter, text, encoding, destination, encodedLength, minimumByteSize);
        }
    }

    private const int UTF8MaxByteLength = 6;

    private static int GetEncodingMaxByteSize(Encoding encoding)
    {
        if (encoding == Encoding.UTF8)
        {
            return UTF8MaxByteLength;
        }

        return encoding.GetMaxByteCount(1);
    }

    private static void WriteMultiSegmentEncoded(PipeWriter writer, ReadOnlySpan<char> source, Encoding encoding, Span<byte> destination, int encodedLength, int minimumByteSize)
    {
        var encoder = encoding.GetEncoder();
        var completed = false;
        var totalBytesUsed = 0;

        // This may be a bug, but encoder.Convert returns completed = true for UTF7 too early.
        // Therefore, we check encodedLength - totalBytesUsed too.
        while (!completed || encodedLength - totalBytesUsed != 0)
        {
            // 'text' is a complete string, the converter should always flush its buffer.
            encoder.Convert(source, destination, flush: true, out var charsUsed, out var bytesUsed, out completed);
            totalBytesUsed += bytesUsed;

            writer.Advance(bytesUsed);
            source = source.Slice(charsUsed);

            destination = writer.GetSpan(minimumByteSize);
        }
    }
}
