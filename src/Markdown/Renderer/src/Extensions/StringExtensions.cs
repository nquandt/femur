
#if NETSTANDARD2_0
namespace Femur.Markdown.Renderer.Extensions;

public static class StringExtensions
{
    public static bool EndsWith(this string value, char test)
    {
        return value.Length > 0 && value[value.Length - 1] == test;
    }
}

#endif