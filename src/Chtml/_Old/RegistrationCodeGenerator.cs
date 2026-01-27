using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChtmlCompiler;

/// <summary>
/// Generates code for route registration.
/// </summary>
public static class RegistrationCodeGenerator
{
    /// <summary>
    /// Generates the RouteRegistration class code.
    /// </summary>
    public static string GenerateRouteRegistration(List<(string className, string route, string ns, string? inputPropsType, string globalPropsType, List<string> routeParameters)> pages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.AspNetCore.Builder;");
        sb.AppendLine("using Shared;");
        sb.AppendLine("using Shared.Generated;");
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine();
        sb.AppendLine("namespace Templates.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class RouteRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    public static void RegisterRoutes(WebApplication app)");
        sb.AppendLine("    {");
        
        foreach (var (className, route, ns, inputPropsType, globalPropsType, routeParameters) in pages)
        {
            var fullClassName = $"{ns}.{className}";
            if (inputPropsType == null)
            {
                // Page with no input props - use EmptyPropsInstance
                // Note: This still works for pages with computed props, as they use EmptyPropsInstance as TInputProps
                sb.AppendLine($"        app.RegisterPage<{fullClassName}, {globalPropsType}>();");
            }
            else
            {
                // Page with input props - use generic overload with input props type
                // Pass route parameters so they can be extracted from the route
                var routeParamsList = routeParameters.Count > 0 
                    ? $"new List<string> {{ {string.Join(", ", routeParameters.Select(p => $"\"{p}\""))} }}" 
                    : "new List<string>()";
                sb.AppendLine($"        app.RegisterPage<{fullClassName}, {ns}.{inputPropsType}, {globalPropsType}>({routeParamsList});");
            }
        }
        
        var pageCount = pages.Count;
        sb.AppendLine();
        sb.AppendLine($"        Console.WriteLine($\"✅ Registered {pageCount} template page routes\");");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
}

