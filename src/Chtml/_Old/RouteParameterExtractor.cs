using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChtmlCompiler;

/// <summary>
/// Extracts route parameters from route patterns.
/// Supports both bracket notation ([slug]) and ASP.NET Core format ({slug}).
/// </summary>
public static class RouteParameterExtractor
{
    /// <summary>
    /// Extracts parameter names from a route pattern.
    /// Example: "/blog/{slug}" -> ["slug"]
    /// Example: "/posts/{category}/{id}" -> ["category", "id"]
    /// Example: "/blog/[slug]" -> ["slug"]
    /// Example: "/blog/{*slugs}" -> ["slugs"] (catch-all)
    /// Example: "/blog/[...slugs]" -> ["slugs"] (catch-all)
    /// </summary>
    public static List<string> ExtractParameters(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return new List<string>();

        var parameters = new List<string>();
        
        // Pattern for ASP.NET Core format: {slug} or {*slugs}
        var aspNetPattern = @"\{(\*?)([^}]+)\}";
        var aspNetMatches = Regex.Matches(route, aspNetPattern);
        
        foreach (Match match in aspNetMatches)
        {
            if (match.Groups.Count >= 3)
            {
                var isCatchAll = match.Groups[1].Value == "*";
                var paramName = match.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(paramName))
                {
                    parameters.Add(paramName);
                }
            }
        }
        
        // Also check for bracket notation in the route (for consistency)
        var bracketPattern = @"\[(\.\.\.)?([^\]]+)\]";
        var bracketMatches = Regex.Matches(route, bracketPattern);
        
        foreach (Match match in bracketMatches)
        {
            if (match.Groups.Count >= 3)
            {
                var paramName = match.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(paramName))
                {
                    parameters.Add(paramName);
                }
            }
        }
        
        return parameters;
    }

    /// <summary>
    /// Checks if a route contains parameters.
    /// </summary>
    public static bool HasParameters(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return false;
            
        return (route.Contains('{') && route.Contains('}')) ||
               (route.Contains('[') && route.Contains(']'));
    }
}

