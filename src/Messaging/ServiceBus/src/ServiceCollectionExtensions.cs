using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Femur.Messaging.ServiceBus;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Azure Service Bus as the messaging transport using a connection string.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The Service Bus connection string.</param>
    /// <param name="configure">Optional transport configuration.</param>
    /// <param name="transportKey">Optional key to register this transport with. Use when you have multiple transports.</param>
    public static IServiceCollection AddFemurServiceBus(
        this IServiceCollection services,
        string connectionString,
        Action<ServiceBusTransportOptions>? configure = null,
        string? transportKey = null)
    {
        services.TryAddSingleton(_ => new ServiceBusClient(connectionString));

        return services.AddFemurServiceBusCore(configure, transportKey);
    }

    /// <summary>
    /// Adds Azure Service Bus as the messaging transport using a connection string factory.
    /// Use this overload to resolve connection strings from IOptions, IConfiguration, or other DI services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionStringFactory">Factory to resolve the connection string from DI.</param>
    /// <param name="configure">Optional transport configuration.</param>
    /// <param name="transportKey">Optional key to register this transport with. Use when you have multiple transports.</param>
    /// <example>
    /// <code>
    /// // From IConfiguration
    /// services.AddFemurServiceBus(
    ///     sp => sp.GetRequiredService&lt;IConfiguration&gt;().GetConnectionString("ServiceBus")!);
    /// 
    /// // From IOptions
    /// services.AddFemurServiceBus(
    ///     sp => sp.GetRequiredService&lt;IOptions&lt;ServiceBusOptions&gt;&gt;().Value.ConnectionString);
    /// </code>
    /// </example>
    public static IServiceCollection AddFemurServiceBus(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory,
        Action<ServiceBusTransportOptions>? configure = null,
        string? transportKey = null)
    {
        services.TryAddSingleton(sp => new ServiceBusClient(connectionStringFactory(sp)));

        return services.AddFemurServiceBusCore(configure, transportKey);
    }

    /// <summary>
    /// Adds Azure Service Bus as the messaging transport using managed identity.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="fullyQualifiedNamespace">The fully qualified Service Bus namespace.</param>
    /// <param name="credential">Azure credential. Required for this overload to avoid ambiguity.</param>
    /// <param name="configure">Optional transport configuration.</param>
    /// <param name="transportKey">Optional key to register this transport with. Use when you have multiple transports.</param>
    public static IServiceCollection AddFemurServiceBusWithManagedIdentity(
        this IServiceCollection services,
        string fullyQualifiedNamespace,
        DefaultAzureCredential? credential = null,
        Action<ServiceBusTransportOptions>? configure = null,
        string? transportKey = null)
    {
        services.TryAddSingleton(_ => new ServiceBusClient(
            fullyQualifiedNamespace,
            credential ?? new DefaultAzureCredential()));

        return services.AddFemurServiceBusCore(options =>
        {
            options.FullyQualifiedNamespace = fullyQualifiedNamespace;
            configure?.Invoke(options);
        }, transportKey);
    }

    /// <summary>
    /// Adds Azure Service Bus as the messaging transport using a factory.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="clientFactory">Factory to create the ServiceBusClient.</param>
    /// <param name="configure">Optional transport configuration.</param>
    /// <param name="transportKey">Optional key to register this transport with. Use when you have multiple transports.</param>
    public static IServiceCollection AddFemurServiceBus(
        this IServiceCollection services,
        Func<IServiceProvider, ServiceBusClient> clientFactory,
        Action<ServiceBusTransportOptions>? configure = null,
        string? transportKey = null)
    {
        services.TryAddSingleton(clientFactory);

        return services.AddFemurServiceBusCore(configure, transportKey);
    }

    /// <summary>
    /// Configures Service Bus options for a specific message type.
    /// </summary>
    public static IServiceCollection ConfigureServiceBusMessage<T>(
        this IServiceCollection services,
        Action<ServiceBusMessageOptions> configure)
        where T : class, IMessage
    {
        services.Configure(typeof(T).FullName!, configure);
        return services;
    }

    private static IServiceCollection AddFemurServiceBusCore(
        this IServiceCollection services,
        Action<ServiceBusTransportOptions>? configure = null,
        string? transportKey = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        if (transportKey != null)
        {
            // Register as keyed service
            services.AddKeyedSingleton<IMessagingTransport, ServiceBusTransport>(transportKey);
        }
        else
        {
            // Register as default
            services.TryAddSingleton<IMessagingTransport, ServiceBusTransport>();
        }

        return services;
    }
}
