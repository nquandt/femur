#pragma warning disable IDE0005 // Using directive is unnecessary
#if NETSTANDARD2_0
using System.IO;
using System.Text;
#endif
#pragma warning restore IDE0005 // Using directive is unnecessary

#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable IDE0161 // Convert to file-scoped namespace
namespace Femur.Markdown.Parser.Compatibility
{
    /// <summary>
    /// Compatibility helpers for string operations
    /// Works on all frameworks by delegating to the appropriate implementation
    /// </summary>
    internal static class StringCompat
    {
        /// <summary>
        /// Helper method for string.Join(char, IEnumerable&lt;string&gt;)
        /// </summary>
        public static string Join(char separator, IEnumerable<string> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

#if NETSTANDARD2_0
            return string.Join(separator.ToString(), values);
#else
            return string.Join(separator, values);
#endif
        }
    }

    /// <summary>
    /// Compatibility helpers for int parsing with ReadOnlySpan
    /// Works on all frameworks by delegating to the appropriate implementation
    /// </summary>
    internal static class Int32Compat
    {
        /// <summary>
        /// Parses a ReadOnlySpan of characters representing a number to an integer.
        /// </summary>
        public static int Parse(ReadOnlySpan<char> s)
        {
#if NETSTANDARD2_0
            return int.Parse(s.ToString());
#else
            return int.Parse(s);
#endif
        }

        /// <summary>
        /// Tries to parse a ReadOnlySpan of characters representing a number to an integer.
        /// </summary>
        public static bool TryParse(ReadOnlySpan<char> s, out int result)
        {
#if NETSTANDARD2_0
            return int.TryParse(s.ToString(), out result);
#else
            return int.TryParse(s, out result);
#endif
        }
    }
}

#if NETSTANDARD2_0
#pragma warning disable IDE0130 // Namespace does not match folder structure
#pragma warning disable IDE0161 // Convert to file-scoped namespace
#pragma warning disable SA1649 // File name should match first type name
namespace System
{
    /// <summary>
    /// Compatibility extensions for netstandard2.0
    /// </summary>
    internal static class NetStandard20CompatibilityExtensions
    {
        /// <summary>
        /// Extension method to write ReadOnlySpan&lt;char&gt; to StreamWriter for netstandard2.0
        /// </summary>
        public static void Write(this StreamWriter writer, ReadOnlySpan<char> value)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.Write(value.ToString());
        }

        /// <summary>
        /// Extension method to write ReadOnlySpan&lt;char&gt; line to StreamWriter for netstandard2.0
        /// </summary>
        public static void WriteLine(this StreamWriter writer, ReadOnlySpan<char> value)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteLine(value.ToString());
        }

        /// <summary>
        /// Extension method for ReadOnlySpan&lt;char&gt;.StartsWith(char) for netstandard2.0
        /// </summary>
        public static bool StartsWith(this ReadOnlySpan<char> span, char value)
        {
            return span.Length > 0 && span[0] == value;
        }

        /// <summary>
        /// Extension method for StringBuilder.Append(ReadOnlySpan&lt;char&gt;) for netstandard2.0
        /// </summary>
        public static StringBuilder Append(this StringBuilder sb, ReadOnlySpan<char> value)
        {
            if (sb == null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

            return sb.Append(value.ToString());
        }
    }

    /// <summary>
    /// Compatibility extensions for string in netstandard2.0
    /// </summary>
    internal static class StringNetStandard20Extensions
    {
        /// <summary>
        /// Extension method for string.StartsWith(char) for netstandard2.0
        /// </summary>
        public static bool StartsWith(this string str, char value)
        {
            if (str == null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            return str.Length > 0 && str[0] == value;
        }

        /// <summary>
        /// Extension method for string.Contains(string, StringComparison) for netstandard2.0
        /// </summary>
        public static bool Contains(this string str, string value, StringComparison comparisonType)
        {
            if (str == null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return str.IndexOf(value, comparisonType) >= 0;
        }
    }
}
#pragma warning restore IDE0130 // Namespace does not match folder structure
#pragma warning restore IDE0161 // Convert to file-scoped namespace
#pragma warning restore SA1649 // File name should match first type name
#endif

