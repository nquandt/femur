namespace Femur.Chtml.CSharpRenderer;

/// <summary>
/// Generates routes from file paths using convention-based routing.
/// Supports bracket notation: [slug] for single parameters, [...slugs] for catch-all.
/// </summary>
public static class RouteGenerator
{
    /// <summary>
    /// Generates a route path from a relative file path.
    /// Convention: routes are relative to /pages folder.
    /// - index files remove themselves from the route ("lift one left")
    /// - [slug] converts to {slug} route parameter
    /// - [...slugs] converts to {*slugs} catch-all route parameter
    /// </summary>
    public static string GenerateFromPath(string relativePath)
    {
        var segments = relativePath.Split('/');
        var pagesIndex = -1;

        // Find the pages folder
        for (var i = 0; i < segments.Length; i++)
        {
            if (string.Equals(segments[i], "pages", StringComparison.OrdinalIgnoreCase))
            {
                pagesIndex = i;
                break;
            }
        }

        if (pagesIndex == -1)
        {
            return "/";
        }

        var routeSegments = new List<string>();
        var lastSegmentIndex = segments.Length - 1;

        // Process segments after "pages"
        for (var i = pagesIndex + 1; i < segments.Length; i++)
        {
            var segment = segments[i];
            var isLastSegment = i == lastSegmentIndex;

            // Remove file extension if present
            if (segment.EndsWith(".chtml", StringComparison.OrdinalIgnoreCase))
            {
                segment = segment.Substring(0, segment.Length - 6);
            }
            else if (segment.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                segment = segment.Substring(0, segment.Length - 5);
            }

            if (string.IsNullOrEmpty(segment))
            {
                continue;
            }

            // If this is an "index" file/folder, it removes itself from the route
            if (segment.Equals("index", StringComparison.OrdinalIgnoreCase))
            {
                // If this is the last segment and it's "index", stop here (don't add it)
                if (isLastSegment)
                {
                    if (routeSegments.Count == 0)
                    {
                        return "/";
                    }

                    break;
                }
                // If it's a folder named "index", skip it (lift one left)
                continue;
            }

            // Convert bracket notation to ASP.NET Core route format
            // [slug] -> {slug}
            // [...slugs] -> {*slugs} (catch-all)
            if (segment.StartsWith('[') && segment.EndsWith(']'))
            {
                var paramName = segment.Substring(1, segment.Length - 2);
                if (paramName.StartsWith("..."))
                {
                    // Catch-all: [...slugs] -> {*slugs}
                    var catchAllName = paramName.Substring(3);
                    routeSegments.Add($"{{*{catchAllName}}}");
                }
                else
                {
                    // Single parameter: [slug] -> {slug}
                    routeSegments.Add($"{{{paramName}}}");
                }
            }
            else
            {
                routeSegments.Add(segment);
            }
        }

        if (routeSegments.Count == 0)
        {
            return "/";
        }

        return "/" + string.Join("/", routeSegments);
    }

    /// <summary>
    /// Extracts route parameter names from a file path.
    /// Looks for bracket notation: [slug] and [...slugs]
    /// Returns a list of parameter names (without brackets).
    /// </summary>
    public static List<string> ExtractParametersFromPath(string relativePath)
    {
        var parameters = new List<string>();
        var segments = relativePath.Split('/');

        foreach (var segment in segments)
        {
            // Remove file extension
            var cleanSegment = segment;
            if (cleanSegment.EndsWith(".chtml", StringComparison.OrdinalIgnoreCase))
            {
                cleanSegment = cleanSegment.Substring(0, cleanSegment.Length - 6);
            }
            else if (cleanSegment.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                cleanSegment = cleanSegment.Substring(0, cleanSegment.Length - 5);
            }

            // Check for bracket notation
            if (cleanSegment.StartsWith('[') && cleanSegment.EndsWith(']'))
            {
                var paramName = cleanSegment.Substring(1, cleanSegment.Length - 2);
                if (paramName.StartsWith("..."))
                {
                    // Catch-all parameter
                    parameters.Add(paramName.Substring(3));
                }
                else
                {
                    // Single parameter
                    parameters.Add(paramName);
                }
            }
        }

        return parameters;
    }
}



