using Microsoft.Extensions.DependencyInjection;

namespace Femur.Messaging;

/// <summary>
/// Builder for configuring message handler registration.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
public sealed class MessageHandlerBuilder<TMessage> where TMessage : class, IMessage
{
    internal IServiceCollection Services { get; }
    internal string? TransportKey { get; private set; }
    internal Action<MessageProcessorOptions>? ConfigureOptions { get; private set; }
    internal IMessageSerializer? Serializer { get; set; }

    internal MessageHandlerBuilder(IServiceCollection services)
    {
        this.Services = services;
    }

    /// <summary>
    /// Specifies which transport to use for this message handler.
    /// Use this when you have multiple transports registered (e.g., Service Bus and RabbitMQ).
    /// </summary>
    /// <param name="transportKey">The key of the transport to use.</param>
    /// <returns>The builder for chaining.</returns>
    public MessageHandlerBuilder<TMessage> UseTransport(string transportKey)
    {
        this.TransportKey = transportKey;
        return this;
    }

    /// <summary>
    /// Configures options for the message processor.
    /// </summary>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public MessageHandlerBuilder<TMessage> Configure(Action<MessageProcessorOptions> configure)
    {
        this.ConfigureOptions = configure;
        return this;
    }
}
