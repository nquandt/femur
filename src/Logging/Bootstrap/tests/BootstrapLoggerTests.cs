using Femur.Logging.Bootstrap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LoggingBootstrapTests;

public class BootstrapLoggerTests
{
    [Fact]
    public void Create_ShouldCreateBootstrapLogger()
    {
        // Arrange & Act
        using var logger = BootstrapLogger.Create<BootstrapLoggerTests>(builder =>
        {
            builder.AddConsole();
        });

        // Assert
        Assert.NotNull(logger);
        Assert.True(logger.IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void Log_ShouldNotThrowException()
    {
        // Arrange
        using var logger = BootstrapLogger.Create<BootstrapLoggerTests>(builder =>
        {
            builder.AddConsole();
        });

        // Act & Assert
        var exception = Record.Exception(() => logger.LogInformation("Test message"));
        Assert.Null(exception);
    }

    [Fact]
    public void AddBootstrappedLogging_ShouldRegisterLogger()
    {
        // Arrange
        using var bootstrapLogger = BootstrapLogger.Create<BootstrapLoggerTests>(builder =>
        {
            builder.AddConsole();
        });

        var services = new ServiceCollection();

        // Act
        services.AddBootstrappedLogging(bootstrapLogger);
        var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        // Assert
        Assert.NotNull(loggerFactory);
    }

    [Fact]
    public void AddBootstrappedLogging_ShouldAllowLoggingInServices()
    {
        // Arrange
        using var bootstrapLogger = BootstrapLogger.Create<BootstrapLoggerTests>(builder =>
        {
            builder.AddConsole();
        });

        var services = new ServiceCollection();
        services.AddBootstrappedLogging(bootstrapLogger);

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetService<ILogger<BootstrapLoggerTests>>();

        // Assert
        Assert.NotNull(logger);
        Assert.True(logger.IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void BootstrapLogger_ShouldDisposeCleanly()
    {
        // Arrange
        var logger = BootstrapLogger.Create<BootstrapLoggerTests>(builder =>
        {
            builder.AddConsole();
        });

        // Act & Assert
        var exception = Record.Exception(() => logger.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public async Task BootstrapLogger_ShouldDisposeAsyncCleanly()
    {
        // Arrange
        var logger = BootstrapLogger.Create<BootstrapLoggerTests>(builder =>
        {
            builder.AddConsole();
        });

        // Act & Assert
        var exception = await Record.ExceptionAsync(async () => await logger.DisposeAsync());
        Assert.Null(exception);
    }

    [Fact]
    public void BeginScope_ShouldNotThrowException()
    {
        // Arrange
        using var logger = BootstrapLogger.Create<BootstrapLoggerTests>(builder =>
        {
            builder.AddConsole();
        });

        // Act & Assert
        var exception = Record.Exception(() =>
        {
            using var scope = logger.BeginScope("Test scope");
            logger.LogInformation("Message in scope");
        });
        Assert.Null(exception);
    }

    [Fact]
    public void Create_WithSharedServices_ShouldRegisterServices()
    {
        // Arrange & Act
        using var logger = BootstrapLogger.Create<BootstrapLoggerTests>(
            builder => builder.AddConsole(),
            services => services.AddSingleton<TestService>());

        // Assert
        Assert.NotNull(logger);
    }

    [Fact]
    public void Create_WithNullSharedServices_ShouldNotThrowException()
    {
        // Arrange & Act
        using var logger = BootstrapLogger.Create<BootstrapLoggerTests>(
            builder => builder.AddConsole(),
            null);

        // Assert
        Assert.NotNull(logger);
    }

    [Fact]
    public void AddBootstrappedLogging_ShouldTransferSharedServices()
    {
        // Arrange
        using var bootstrapLogger = BootstrapLogger.Create<BootstrapLoggerTests>(
            builder => builder.AddConsole(),
            services => services.AddSingleton<TestService>());

        var mainServices = new ServiceCollection();

        // Act
        mainServices.AddBootstrappedLogging(bootstrapLogger);
        var serviceProvider = mainServices.BuildServiceProvider();
        var testService = serviceProvider.GetService<TestService>();

        // Assert
        Assert.NotNull(testService);
    }

    [Fact]
    public void AddBootstrappedLogging_ShouldShareSingletonInstances()
    {
        // Arrange
        var sharedInstance = new TestService();
        using var bootstrapLogger = BootstrapLogger.Create<BootstrapLoggerTests>(
            builder => builder.AddConsole(),
            services => services.AddSingleton(sharedInstance));

        var mainServices = new ServiceCollection();

        // Act
        mainServices.AddBootstrappedLogging(bootstrapLogger);
        var serviceProvider = mainServices.BuildServiceProvider();
        var retrievedService = serviceProvider.GetRequiredService<TestService>();

        // Assert
        Assert.Same(sharedInstance, retrievedService);
    }

    [Fact]
    public void AddBootstrappedLogging_ShouldTransferMultipleSharedServices()
    {
        // Arrange
        using var bootstrapLogger = BootstrapLogger.Create<BootstrapLoggerTests>(
            builder => builder.AddConsole(),
            services =>
            {
                services.AddSingleton<TestService>();
                services.AddSingleton<AnotherTestService>();
            });

        var mainServices = new ServiceCollection();

        // Act
        mainServices.AddBootstrappedLogging(bootstrapLogger);
        var serviceProvider = mainServices.BuildServiceProvider();
        var testService = serviceProvider.GetService<TestService>();
        var anotherService = serviceProvider.GetService<AnotherTestService>();

        // Assert
        Assert.NotNull(testService);
        Assert.NotNull(anotherService);
    }

    [Fact]
    public void AddBootstrappedLogging_WithSharedServices_ShouldAllowDependencyInjection()
    {
        // Arrange
        using var bootstrapLogger = BootstrapLogger.Create<BootstrapLoggerTests>(
            builder => builder.AddConsole(),
            services => services.AddSingleton<TestService>());

        var mainServices = new ServiceCollection();
        mainServices.AddBootstrappedLogging(bootstrapLogger);
        mainServices.AddSingleton<ServiceThatDependsOnTestService>();

        // Act
        var serviceProvider = mainServices.BuildServiceProvider();
        var dependentService = serviceProvider.GetService<ServiceThatDependsOnTestService>();

        // Assert
        Assert.NotNull(dependentService);
        Assert.NotNull(dependentService.TestService);
    }
}

// Test helper classes
public class TestService
{
    public Guid Id { get; } = Guid.NewGuid();
}

public class AnotherTestService
{
    public Guid Id { get; } = Guid.NewGuid();
}

public class ServiceThatDependsOnTestService(TestService testService)
{
    public TestService TestService { get; } = testService;
}
