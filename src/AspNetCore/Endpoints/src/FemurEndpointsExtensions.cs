
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Femur;

public static class FemurEndpointsExtensions
{
    public static MethodInfo GetMethodInfoOfEndpoint<T>(Expression<Func<T, Delegate>> expression) where T : class
    {
        // TODO add validation to assert that a top level method of an instance is being obtained
        var y = (LambdaExpression)expression;
        var u = (UnaryExpression)y.Body;
        var m = (MethodCallExpression)u.Operand;
        var z = (ConstantExpression)m.Object!;
        var l = (MethodInfo)z.Value!;

        return l;
    }
    public static IEndpointConventionBuilder  MapEndpoint<T>(this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string routePattern,
        IEnumerable<string> httpMethods,
        Expression<Func<T, Delegate>> expression)
            where T : class
    {
        var methodInfo = GetMethodInfoOfEndpoint(expression);

        var del = DelegateGenerator.CreateStaticDelegate(typeof(T), methodInfo);

        return endpoints.MapMethods(routePattern, httpMethods, del);        
    }

    public static IEndpointConventionBuilder  MapEndpoint<T>(this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string routePattern,
        IEnumerable<HttpMethod> httpMethods,
        Expression<Func<T, Delegate>> expression)
            where T : class
    {
        return endpoints.MapEndpoint<T>(routePattern, GetVerbStrings(httpMethods), expression);        
    }

    private static IEnumerable<string> GetVerbStrings(IEnumerable<HttpMethod> httpMethods)
    {
        var _mapping = new Dictionary<HttpMethod, string>()
        {
            [HttpMethod.Trace] = "TRACE",
            [HttpMethod.Head] = "HEAD",
            [HttpMethod.Options] = "OPTIONS",
            [HttpMethod.Connect] = "CONNECT",
            [HttpMethod.Get] = "GET",
            [HttpMethod.Post] = "POST",
            [HttpMethod.Patch] = "PATCH",
            [HttpMethod.Put] = "PUT",
            [HttpMethod.Delete] = "DELETE"
        };

        var finalMethods = new List<string>();
        foreach (var att in httpMethods)
        {
            if (!_mapping.ContainsKey(att))
            {
                //TODO probably do something if we dont support verb?
                continue;
            }

            finalMethods.Add(_mapping[att]);
        }

        return finalMethods;
    }
}