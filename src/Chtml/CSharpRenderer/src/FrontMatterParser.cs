using System.Text;
using YamlDotNet.RepresentationModel;

namespace Femur.Chtml.CSharpRenderer;

/// <summary>
/// Handles parsing of front matter (YAML metadata) from .chtml files.
/// </summary>
public static class FrontMatterParser
{
    /// <summary>
    /// Splits front matter from the HTML body.
    /// </summary>
    public static (string? frontMatter, string body) Split(string text)
    {
        var reader = new StringReader(text);
        var first = reader.ReadLine();
        if (first?.Trim() != "---")
        {
            return (null, text);
        }

        var fm = new StringBuilder();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Trim() == "---")
            {
                break;
            }

            fm.AppendLine(line);
        }

        var rest = reader.ReadToEnd() ?? string.Empty;
        return (fm.ToString(), rest);
    }

    /// <summary>
    /// Parses YAML front matter into a dictionary.
    /// </summary>
    public static Dictionary<string, object>? Parse(string? yamlText)
    {
        if (string.IsNullOrWhiteSpace(yamlText))
        {
            return null;
        }

        var dict = new Dictionary<string, object>();
        var yaml = new YamlStream();
        yaml.Load(new StringReader(yamlText));

        if (yaml.Documents.Count == 0)
        {
            return dict;
        }

        var root = yaml.Documents[0].RootNode as YamlMappingNode;
        if (root == null)
        {
            return dict;
        }

        foreach (var entry in root.Children)
        {
            var key = ((YamlScalarNode)entry.Key).Value ?? string.Empty;

            if (entry.Value is YamlMappingNode mappingNode)
            {
                // Nested dictionary (e.g., Components, Props, Vars)
                var nestedDict = new Dictionary<string, object>();
                foreach (var nestedEntry in mappingNode.Children)
                {
                    var nestedKey = ((YamlScalarNode)nestedEntry.Key).Value ?? string.Empty;
                    var nestedValue = nestedEntry.Value switch
                    {
                        YamlScalarNode s => s.Value ?? string.Empty,
                        _ => nestedEntry.Value.ToString() ?? string.Empty
                    };
                    nestedDict[nestedKey] = nestedValue;
                }

                dict[key] = nestedDict;
            }
            else
            {
                // Simple value
                var val = entry.Value switch
                {
                    YamlScalarNode s => s.Value ?? string.Empty,
                    _ => entry.Value.ToString() ?? string.Empty
                };
                dict[key] = val;
            }
        }

        return dict;
    }

    /// <summary>
    /// Parses props from front matter YAML.
    /// </summary>
    /// <param name="frontMatter">The front matter dictionary</param>
    /// <param name="sectionName">The section name to parse ("Props" or "ComputedProps")</param>
    public static List<(string name, string type, bool isNullable)> ParseProps(Dictionary<string, object>? frontMatter, string sectionName)
    {
        var props = new List<(string name, string type, bool isNullable)>();

        if (frontMatter == null || !frontMatter.TryGetValue(sectionName, out var propsObj))
        {
            return props;
        }

        if (propsObj is Dictionary<string, object> propsDict)
        {
            foreach (var (name, typeObj) in propsDict)
            {
                var typeStr = typeObj?.ToString() ?? "System.String";
                var parts = typeStr.Split('|', StringSplitOptions.TrimEntries);
                var isNullable = parts.Length > 1 && parts[1].Equals("null", StringComparison.OrdinalIgnoreCase);
                var type = isNullable ? parts[0].Trim() : typeStr;
                props.Add((name, type, isNullable));
            }
        }

        return props;
    }
}



