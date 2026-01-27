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
    /// <returns>A builder for further configuration.</returns>
    /// <example>
    /// <code>
    /// services.AddMessageHandler&lt;OrderMessage, OrderMessageHandler&gt;()
    ///     .WithSerializer(customSerializer)
    ///     .Configure(options => options.MaxDeliveryCount = 5)
    ///     .UseTransport("primary");
    /// </code>
    /// </example>
    public static MessageHandlerBuilder<TMessage> AddMessageHandler<TMessage, THandler>(
        this IServiceCollection services)
        where TMessage : class, IMessage
        where THandler : class, IMessageHandler<TMessage>
    {
        var builder = new MessageHandlerBuilder<TMessage>(services);

        // Register the handler as scoped (one instance per message)
        services.AddScoped<IMessageHandler<TMessage>, THandler>();

        // Complete registration
        CompleteRegistration(builder);

        return builder;
    }

    private static void CompleteRegistration<TMessage>(MessageHandlerBuilder<TMessage> builder)
        where TMessage : class, IMessage
    {
        var services = builder.Services;
        var optionsName = typeof(TMessage).FullName!;

        // Configure options for this message type
        if (builder.ConfigureOptions != null)
        {
            services.Configure(optionsName, builder.ConfigureOptions);
        }

        // Register client using shared method
        services.AddMessageClient<TMessage>(builder.Serializer, builder.TransportKey);

        // Register processor
        services.AddSingleton(sp =>
        {
            var client = sp.GetRequiredService<IMessageClient<TMessage>>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var logger = sp.GetRequiredService<ILogger<MessageProcessor<TMessage>>>();
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<MessageProcessorOptions>>();
            var options = Options.Create(optionsMonitor.Get(optionsName));

            return new MessageProcessor<TMessage>(client, scopeFactory, logger, options);
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
