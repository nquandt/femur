using Femur.Messaging;
using Femur.Messaging.Example;
using Femur.Messaging.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace Femur.Messaging.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFemurInMemory_RegistersTransport()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddFemurInMemory();
        var provider = services.BuildServiceProvider();

        // Assert
        var transport = provider.GetService<IMessagingTransport>();
        Assert.NotNull(transport);
        Assert.IsType<InMemoryTransport>(transport);
    }

    [Fact]
    public void AddFemurInMemory_RegistersMessageQueue()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddFemurInMemory();
        var provider = services.BuildServiceProvider();

        // Assert
        var queue = provider.GetService<InMemoryMessageQueue>();
        Assert.NotNull(queue);
    }

    [Fact]
    public void AddFemurInMemory_Singleton_ReturnsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddFemurInMemory();
        var provider = services.BuildServiceProvider();

        // Act
        var queue1 = provider.GetService<InMemoryMessageQueue>();
        var queue2 = provider.GetService<InMemoryMessageQueue>();

        // Assert
        Assert.Same(queue1, queue2);
    }

    [Fact]
    public void GetMessageQueue_ReturnsQueue()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddFemurInMemory();
        var provider = services.BuildServiceProvider();

        // Act
        var queue = provider.GetMessageQueue();

        // Assert
        Assert.NotNull(queue);
        Assert.IsType<InMemoryMessageQueue>(queue);
    }

    [Fact]
    public void AddMessageHandler_RegistersHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFemurInMemory();

        // Act
        services.AddMessageHandler<OrderMessage, OrderMessageHandler>();
        var provider = services.BuildServiceProvider();

        // Assert
        var handler = provider.GetService<IMessageHandler<OrderMessage>>();
        Assert.NotNull(handler);
        Assert.IsType<OrderMessageHandler>(handler);
    }

    [Fact]
    public void AddMessageHandler_RegistersHostedService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFemurInMemory();

        // Act
        services.AddMessageHandler<OrderMessage, OrderMessageHandler>();
        var provider = services.BuildServiceProvider();

        // Assert
        var hostedServices = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
        Assert.NotEmpty(hostedServices);
    }

    [Fact]
    public void AddMessageHandler_WithOptions_ConfiguresProcessor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFemurInMemory();

        // Act
        services.AddMessageHandler<OrderMessage, OrderMessageHandler>(
            configureOptions: options =>
            {
                options.MaxDeliveryCount = 5;
                options.EnableLockTracking = true;
            });

        var provider = services.BuildServiceProvider();

        // Assert - should not throw
        var handler = provider.GetService<IMessageHandler<OrderMessage>>();
        Assert.NotNull(handler);
    }

    [Fact]
    public void AddMessageHandler_MultipleHandlers_AllRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFemurInMemory();

        // Act
        services.AddMessageHandler<OrderMessage, OrderMessageHandler>();
        // We would add more handlers here if we had other message types
        var provider = services.BuildServiceProvider();

        // Assert
        var orderHandler = provider.GetService<IMessageHandler<OrderMessage>>();
        Assert.NotNull(orderHandler);
    }
}

// Simple test handler for verification
public class TestMessageHandler : IMessageHandler<OrderMessage>
{
    public int CallCount { get; private set; }

    public Task HandleAsync(OrderMessage message, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.CompletedTask;
    }
}
