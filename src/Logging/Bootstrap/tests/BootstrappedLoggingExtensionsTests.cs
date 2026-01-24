using Femur.Logging.Bootstrap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LoggingBootstrapTests;

public class BootstrappedLoggingExtensionsTests
{
    [Fact]
    public void AddBootstrappedLogging_ShouldRemoveExistingLoggerProviders()
    {
        // Arrange
        using var bootstrapLogger = BootstrapLogger.Create<BootstrappedLoggingExtensionsTests>(builder =>
        {
            builder.AddConsole();
        });

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // Act
        services.AddBootstrappedLogging(bootstrapLogger);

        // Assert
        var loggerProviderDescriptors = services.Where(d => d.ServiceType == typeof(ILoggerProvider)).ToList();
        // The only ILoggerProvider should be from the bootstrapped logger
        Assert.True(loggerProviderDescriptors.All(d => d.ImplementationFactory != null));
    }

    [Fact]
    public void AddBootstrappedLogging_ShouldTransferLoggingServices()
    {
        // Arrange
        using var bootstrapLogger = BootstrapLogger.Create<BootstrappedLoggingExtensionsTests>(builder =>
        {
            builder.AddConsole();
        });

        var services = new ServiceCollection();

        // Act
        services.AddBootstrappedLogging(bootstrapLogger);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        Assert.NotNull(loggerFactory);

        var logger = loggerFactory.CreateLogger<BootstrappedLoggingExtensionsTests>();
        Assert.NotNull(logger);
        Assert.True(logger.IsEnabled(LogLevel.Information));
    }
}
