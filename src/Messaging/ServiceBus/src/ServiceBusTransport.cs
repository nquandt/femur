using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Femur.Messaging.ServiceBus;

/// <summary>
/// Service Bus implementation of IMessagingTransport.
/// </summary>
internal sealed class ServiceBusTransport : IMessagingTransport
{
    private readonly ServiceBusClient _client;
    private readonly IOptionsMonitor<ServiceBusMessageOptions> _messageOptionsMonitor;
    private readonly IOptions<ServiceBusTransportOptions> _transportOptions;
    private readonly ILoggerFactory _loggerFactory;

    public ServiceBusTransport(
        ServiceBusClient client,
        IOptionsMonitor<ServiceBusMessageOptions> messageOptionsMonitor,
        IOptions<ServiceBusTransportOptions> transportOptions,
        ILoggerFactory loggerFactory)
    {
        this._client = client;
        this._messageOptionsMonitor = messageOptionsMonitor;
        this._transportOptions = transportOptions;
        this._loggerFactory = loggerFactory;
    }

    public IMessageClient<T> CreateClient<T>(IMessageSerializer serializer) where T : class, IMessage
    {
        var messageOptions = Options.Create(this._messageOptionsMonitor.Get(typeof(T).FullName!));
        var logger = this._loggerFactory.CreateLogger<ServiceBusMessageClient<T>>();

        return new ServiceBusMessageClient<T>(this._client, messageOptions, this._transportOptions, serializer, logger);
    }
}
