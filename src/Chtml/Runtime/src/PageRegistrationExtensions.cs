

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Femur.Chtml.Runtime;


public static class PageRegistrationExtensions
{
    /// <summary>
    /// Registers a page that implements IRenderablePage&lt;T&gt;.
    /// </summary>
    public static WebApplication RegisterPage<T, TGlobalProps>(this WebApplication app) where T : IRenderablePage<EmptyPropsInstance, TGlobalProps> where TGlobalProps : class
    {
        var route = T.Route;
        // Generate unique endpoint name using route path to avoid conflicts
        // Replace slashes and special chars with underscores, remove leading slash
        var routeName = $"Get{SanitizeRouteForName(route)}";

        app.MapGet(
            route,
            async (HttpContext ctx) =>
            {
                ctx.Response.ContentType = "text/html; charset=utf-8";
                var globalProps = ctx.Features.Get<TGlobalProps>() ?? throw new InvalidOperationException($"GlobalProps of type {typeof(TGlobalProps).Name} not found in request features. Ensure GlobalProps are set in middleware.");

                var renderContext = RenderContext<TGlobalProps>.Create(ctx.Response.BodyWriter, globalProps);
                await T.RenderAsync(renderContext, EmptyProps.Instance);
                await ctx.Response.BodyWriter.FlushAsync();
            }
        ).WithName(routeName);

        return app;
    }

    /// <summary>
    /// Registers a page that implements IRenderablePage&lt;TProps&gt; with props.
    /// Pages with props need to extract them from request/route/query parameters.
    /// </summary>
    public static WebApplication RegisterPage<T, TProps, TGlobalProps>(this WebApplication app, List<string>? routeParameters = null)
        where T : IRenderablePage<TProps, TGlobalProps>
        where TProps : class
        where TGlobalProps : class
    {
        var route = T.Route;
        // Generate unique endpoint name using route path to avoid conflicts
        // Replace slashes and special chars with underscores, remove leading slash
        var routeName = $"Get{SanitizeRouteForName(route)}";

        app.MapGet(
            route,
            async (HttpContext ctx) =>
            {
                ctx.Response.ContentType = "text/html; charset=utf-8";
                var globalProps = ctx.Features.Get<TGlobalProps>() ?? throw new InvalidOperationException($"GlobalProps of type {typeof(TGlobalProps).Name} not found in request features. Ensure GlobalProps are set in middleware.");

                var renderContext = RenderContext<TGlobalProps>.Create(ctx.Response.BodyWriter, globalProps);
                // For EmptyPropsInstance, use the singleton
                if (typeof(TProps) == typeof(EmptyPropsInstance))
                {
                    await T.RenderAsync(renderContext, EmptyProps.Instance as TProps ?? throw new InvalidOperationException());
                }
                else
                {
                    // Extract props from route parameters and query parameters
                    var props = ExtractPropsFromRequest<TProps>(ctx, routeParameters ?? new List<string>());
                    await T.RenderAsync(renderContext, props);
                }

                await ctx.Response.BodyWriter.FlushAsync();
            }
        ).WithName(routeName);

        return app;
    }

    /// <summary>
    /// Extracts props from HTTP request (route parameters and query parameters).
    /// </summary>
    private static TProps ExtractPropsFromRequest<TProps>(HttpContext ctx, List<string> routeParameters) where TProps : class
    {
        var propsType = typeof(TProps);
        var props = Activator.CreateInstance<TProps>();

        // Extract route parameters
        foreach (var paramName in routeParameters)
        {
            var routeValue = ctx.Request.RouteValues[paramName]?.ToString();
            if (routeValue != null)
            {
                SetPropertyValue(props, propsType, paramName, routeValue);
            }
        }

        // Extract query parameters (for any props not already set from route)
        foreach (var queryParam in ctx.Request.Query)
        {
            var propName = queryParam.Key;
            var propInfo = propsType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (propInfo != null && propInfo.CanWrite)
            {
                var value = queryParam.Value.FirstOrDefault();
                if (value != null)
                {
                    SetPropertyValue(props, propsType, propName, value);
                }
            }
        }

        return props;
    }

    /// <summary>
    /// Sets a property value on an object, converting from string if needed.
    /// </summary>
    private static void SetPropertyValue(object obj, Type objType, string propertyName, string value)
    {
        var propInfo = objType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (propInfo == null || !propInfo.CanWrite)
        {
            return;
        }

        var propType = propInfo.PropertyType;
        object? convertedValue;

        // Handle common types
        if (propType == typeof(string))
        {
            convertedValue = value;
        }
        else if (propType == typeof(int) && int.TryParse(value, out var intVal))
        {
            convertedValue = intVal;
        }
        else if (propType == typeof(long) && long.TryParse(value, out var longVal))
        {
            convertedValue = longVal;
        }
        else if (propType == typeof(bool) && bool.TryParse(value, out var boolVal))
        {
            convertedValue = boolVal;
        }
        else if (propType == typeof(Guid) && Guid.TryParse(value, out var guidVal))
        {
            convertedValue = guidVal;
        }
        else if (propType.IsEnum && Enum.TryParse(propType, value, true, out var enumVal))
        {
            convertedValue = enumVal;
        }
        else
        {
            // Try to use Convert.ChangeType as a fallback
            try
            {
                convertedValue = Convert.ChangeType(value, propType);
            }
            catch
            {
                // If conversion fails, skip this property
                return;
            }
        }

        propInfo.SetValue(obj, convertedValue);
    }

    /// <summary>
    /// Sanitizes a route path to create a valid endpoint name.
    /// Examples: "/" -> "Root", "/experience" -> "Experience", "/blog/post" -> "BlogPost"
    /// </summary>
    private static string SanitizeRouteForName(string route)
    {
        if (string.IsNullOrEmpty(route) || route == "/")
        {
            return "Root";
        }

        // Remove leading/trailing slashes
        var sanitized = route.Trim('/');

        if (string.IsNullOrEmpty(sanitized))
        {
            return "Root";
        }

        // Split by slashes and convert each segment to PascalCase
        var segments = sanitized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var result = new System.Text.StringBuilder();

        foreach (var segment in segments)
        {
            if (segment.Length > 0)
            {
                // Convert to PascalCase
                result.Append(char.ToUpperInvariant(segment[0]));
                if (segment.Length > 1)
                {
                    result.Append(segment.AsSpan(1));
                }
            }
        }

        return result.Length > 0 ? result.ToString() : "Root";
    }
}

