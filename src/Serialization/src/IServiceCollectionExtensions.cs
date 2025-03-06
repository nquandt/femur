

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Femur.Serialization;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddSerializer<T>(this IServiceCollection services) where T : class, IAsyncSerializer
    {
        services.TryAddEnumerable(new ServiceDescriptor(typeof(IAsyncSerializer), typeof(T), ServiceLifetime.Singleton));
        services.TryAddSingleton<IAsyncSerializerFactory, DefaultAsyncSerializerFactory>();

        return services;
    }

    public static IServiceCollection AddDefaultJsonSerializer(this IServiceCollection services, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        services.TryAddSingleton(jsonSerializerOptions ?? JsonSerializerOptions.Default);
        return services.AddSerializer<DefaultJsonSerializer>();
    }
}