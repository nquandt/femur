using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Femur.Messaging;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a message handler for the specified message type.
    /// The handler will be connected to the configured transport automatically.
    /// </summary>
    /// <typeparam name="TMessage">The message type to handle.</typeparam>
    /// <typeparam name="THandler">The handler implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="serializer">The serializer to use. If null, uses default JSON serializer.</param>
    /// <param name="configureOptions">Optional processor configuration.</param>
    /// <returns>A builder for further configuration.</returns>
    public static MessageHandlerBuilder<TMessage> AddMessageHandler<TMessage, THandler>(
        this IServiceCollection services,
        IMessageSerializer? serializer = null,
        Action<MessageProcessorOptions>? configureOptions = null)
        where TMessage : class, IMessage
        where THandler : class, IMessageHandler<TMessage>
    {
        var builder = new MessageHandlerBuilder<TMessage>(services);

        // Apply configuration if provided
        if (configureOptions != null)
        {
            builder.Configure(configureOptions);
        }

        // Register the handler
        services.AddSingleton<IMessageHandler<TMessage>, THandler>();

        // Store serializer for later use
        builder.Serializer = serializer;

        // We'll complete the registration in a separate internal method
        // so we can access the builder's configuration
        CompleteRegistration(builder);

        return builder;
    }

    private static void CompleteRegistration<TMessage>(MessageHandlerBuilder<TMessage> builder)
        where TMessage : class, IMessage
    {
        var services = builder.Services;
        var transportKey = builder.TransportKey;
        var optionsName = typeof(TMessage).FullName!;

        // Configure options for this message type
        if (builder.ConfigureOptions != null)
        {
            services.Configure(optionsName, builder.ConfigureOptions);
        }

        // Register client (created by transport)
        services.AddSingleton<IMessageClient<TMessage>>(sp =>
        {
            var transport = transportKey != null
                ? sp.GetRequiredKeyedService<IMessagingTransport>(transportKey)
                : sp.GetRequiredService<IMessagingTransport>();
            var serializer = builder.Serializer ?? new JsonMessageSerializer();
            return transport.CreateClient<TMessage>(serializer);
        });

        // Register processor
        services.AddSingleton(sp =>
        {
            var client = sp.GetRequiredService<IMessageClient<TMessage>>();
            var handler = sp.GetRequiredService<IMessageHandler<TMessage>>();
            var logger = sp.GetRequiredService<ILogger<MessageProcessor<TMessage>>>();
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<MessageProcessorOptions>>();
            var options = Options.Create(optionsMonitor.Get(optionsName));

            return new MessageProcessor<TMessage>(client, handler, logger, options);
        });

        // Register hosted service
        services.AddHostedService<MessageProcessorHostedService<TMessage>>();
    }

    /// <summary>
    /// Registers a message client for manual message consumption.
    /// Unlike AddMessageHandler, this does NOT start automatic background processing.
    /// Use this when you want to manually control message consumption.
    /// </summary>
    /// <typeparam name="TMessage">The message type to consume.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="serializer">The serializer to use. If null, uses default JSON serializer.</param>
    /// <param name="transportKey">Optional transport key when using multiple transports.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessageClient<TMessage>(
        this IServiceCollection services,
        IMessageSerializer? serializer = null,
        string? transportKey = null)
        where TMessage : class, IMessage
    {
        // Register client (created by transport)
        services.AddSingleton<IMessageClient<TMessage>>(sp =>
        {
            var transport = transportKey != null
                ? sp.GetRequiredKeyedService<IMessagingTransport>(transportKey)
                : sp.GetRequiredService<IMessagingTransport>();
            var actualSerializer = serializer ?? new JsonMessageSerializer();
            return transport.CreateClient<TMessage>(actualSerializer);
        });

        return services;
    }
}
