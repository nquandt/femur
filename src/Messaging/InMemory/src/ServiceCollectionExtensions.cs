using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Femur.Messaging.InMemory;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds an in-memory messaging transport for testing.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="transportKey">Optional key to register this transport with. Use when you have multiple transports.</param>
    public static IServiceCollection AddFemurInMemory(
        this IServiceCollection services,
        string? transportKey = null)
    {
        services.TryAddSingleton<InMemoryMessageQueue>();

        if (transportKey != null)
        {
            // Register as keyed service
            services.AddKeyedSingleton<IMessagingTransport, InMemoryTransport>(transportKey);
        }
        else
        {
            // Register as default
            services.TryAddSingleton<IMessagingTransport, InMemoryTransport>();
        }

        return services;
    }

    /// <summary>
    /// Gets the in-memory message queue for publishing test messages and inspecting results.
    /// </summary>
    public static InMemoryMessageQueue GetMessageQueue(this IServiceProvider services)
    {
        return services.GetRequiredService<InMemoryMessageQueue>();
    }
}
