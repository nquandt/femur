
    // public static WebApplication MapFemurEndpoints(this WebApplication app)
    // {
    //     var endpointCollection = app.Services.GetRequiredService<FemurServiceCollection>();

    //     foreach (var endpointRegistration in endpointCollection.Registrations)
    //     {
    //         var del = DelegateGenerator.CreateStaticDelegate(endpointRegistration.Type, "HandleAsync");

    //         app.MapMethods(endpointRegistration.Template, endpointRegistration.SupportedHttpMethods, del);
    //     }

    //     return app;
    // }



    
    public static IServiceCollection AddFemurEndpoints(this IServiceCollection services, Type[]? types = null)
    {
        var _mapping = new Dictionary<Type, string>()
        {
            [typeof(HttpGetAttribute)] = "GET",
            [typeof(HttpPostAttribute)] = "POST",
            [typeof(HttpPutAttribute)] = "PUT",
            [typeof(HttpDeleteAttribute)] = "DELETE"
        };

        IEnumerable<Assembly> assemblies = types != null
                ? types.Select(x => x.Assembly).Distinct()
                : AppDomain.CurrentDomain.GetAssemblies();

        var endpointTypes = GetImplementationsFromAssemblies<FemurEndpoint>(assemblies);

        var registrations = new List<EndpointRegistration>();
        foreach (var endpointType in endpointTypes)
        {
            var atts = endpointType.GetCustomAttributes(typeof(RouteAttribute), true)
                    .Select(x => (RouteAttribute)x);
            if (!atts.Any()) { continue; }
            var routeAtt = atts.First()!;

            var handleAsyncMethodDef = endpointType.GetMethod("HandleAsync")!;

            var httpMethodsAtts = handleAsyncMethodDef.GetCustomAttributes(typeof(HttpMethodAttribute), true)
                    .Select(x => x.GetType());


            var finalMethods = new List<string>();
            foreach (var att in httpMethodsAtts)
            {
                if (_mapping.ContainsKey(att))
                {
                    finalMethods.Add(_mapping[att]);
                }
            }

            registrations.Add(new EndpointRegistration()
            {
                Type = endpointType,
                Template = routeAtt.Template,
                SupportedHttpMethods = finalMethods,
                Method = handleAsyncMethodDef
            });

            services.AddTransient(endpointType);
        }

        services.AddSingleton(new FemurServiceCollection()
        {
            Registrations = registrations
        });

        return services;
    }

 

 internal class FemurServiceCollection
{
    public IEnumerable<EndpointRegistration> Registrations { get; set; } = Enumerable.Empty<EndpointRegistration>();
}

internal class EndpointRegistration
{
    public required string Template { get; set; }
    public required List<string> SupportedHttpMethods { get; set; }
    public required Type Type { get; set; }
    public required MethodInfo Method { get; set; }
}

   private static IEnumerable<Type> GetImplementationsFromAssemblies<T>(IEnumerable<Assembly> assemblies)
    {
        return assemblies.Select(x => x.GetTypes().Where(x => typeof(T).IsAssignableFrom(x) && x.IsClass && !x.IsAbstract)).Aggregate((x, y) => x.Concat(y)).Distinct();
    }



using Microsoft.AspNetCore.Http;

namespace Femur;

public abstract class FemurEndpoint
{
    public abstract Task HandleAsync(HttpContext context, CancellationToken cancellationToken);
}