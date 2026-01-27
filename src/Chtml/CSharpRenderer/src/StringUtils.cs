using System.Text;
using System.Text.RegularExpressions;

namespace Femur.Chtml.CSharpRenderer;

/// <summary>
/// Utility methods for string manipulation and name conversion.
/// </summary>
public static class StringUtils
{
    /// <summary>
    /// Converts a string to PascalCase.
    /// </summary>
    public static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return s;
        }

        var parts = Regex.Split(s, "[^A-Za-z0-9]+");
        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            if (string.IsNullOrEmpty(p))
            {
                continue;
            }

            sb.Append(char.ToUpperInvariant(p[0]));
            if (p.Length > 1)
            {
                sb.Append(p.AsSpan(1));
            }
        }

        return sb.ToString();
    }
}



