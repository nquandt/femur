using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Femur.DependencyInjection.Tests;

// ═══════════════════════════════════════════════════════════════════════
// Test Types - must be top-level for dynamic proxy generation
// ═══════════════════════════════════════════════════════════════════════

// Custom open generic for testing
public interface IRepository<T> where T : class
{
    void Add(T entity);
    T? GetById(int id);
    IEnumerable<T> GetAll();
}

public class InMemoryRepository<T> : IRepository<T> where T : class
{
    private readonly Dictionary<int, T> _store = new();

    public void Add(T entity)
    {
        var id = (int)(entity.GetType().GetProperty("Id")?.GetValue(entity) ?? 0);
        _store[id] = entity;
    }

    public T? GetById(int id) => _store.GetValueOrDefault(id);

    public IEnumerable<T> GetAll() => _store.Values;
}

// Multiple type parameter generic
public interface IConverter<TIn, TOut>
{
    TOut Convert(TIn input);
}

public class DefaultConverter<TIn, TOut> : IConverter<TIn, TOut>
{
    public TOut Convert(TIn input)
    {
        return (TOut)System.Convert.ChangeType(input, typeof(TOut))!;
    }
}

// Scoped service for testing
public interface IScopedService
{
    Guid InstanceId { get; }
}

public class ScopedService : IScopedService
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

// Non-logging service for filtering test
public interface INonLoggingService { }

// Test entity for repository tests
public class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// Tests verifying that open generic types are properly proxied,
/// not just copied to the target collection.
/// </summary>
public class OpenGenericProxyTests
{
    [Fact]
    public void ILogger_UsesSharedLoggerFactory()
    {
        // Arrange
        var testProvider = new CountingLoggerProvider();

        var sourceServices = new ServiceCollection();
        sourceServices.AddLogging(b => b.AddProvider(testProvider));
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();
        targetServices.AddProxiedServices(sourceServices, sourceProvider);
        var targetProvider = targetServices.BuildServiceProvider();

        // Act
        var sourceFactory = sourceProvider.GetRequiredService<ILoggerFactory>();
        var targetFactory = targetProvider.GetRequiredService<ILoggerFactory>();

        // Get loggers from both providers
        var loggerFromSource = sourceProvider.GetRequiredService<ILogger<TestClass1>>();
        var loggerFromTarget = targetProvider.GetRequiredService<ILogger<TestClass1>>();

        loggerFromSource.LogInformation("From source");
        loggerFromTarget.LogInformation("From target");

        // Assert
        Assert.Same(sourceFactory, targetFactory);
        Assert.Equal(2, testProvider.LogCount);
    }

    [Fact]
    public void ILogger_DifferentCategories_SameFactory()
    {
        // Arrange
        var testProvider = new CountingLoggerProvider();

        var sourceServices = new ServiceCollection();
        sourceServices.AddLogging(b => b.AddProvider(testProvider));
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();
        targetServices.AddProxiedServices(sourceServices, sourceProvider);
        var targetProvider = targetServices.BuildServiceProvider();

        // Act - get different logger categories from target
        var logger1 = targetProvider.GetRequiredService<ILogger<TestClass1>>();
        var logger2 = targetProvider.GetRequiredService<ILogger<TestClass2>>();
        var logger3 = targetProvider.GetRequiredService<ILogger<TestClass3>>();

        logger1.LogInformation("From TestClass1");
        logger2.LogInformation("From TestClass2");
        logger3.LogInformation("From TestClass3");

        // Assert - all should go to the same provider
        Assert.Equal(3, testProvider.LogCount);
    }

    [Fact]
    public void IOptions_ProxiedCorrectly()
    {
        // Arrange
        var sourceServices = new ServiceCollection();
        sourceServices.Configure<TestOptions>(opt => opt.Value = "SourceValue");
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();
        targetServices.AddProxiedServices(sourceServices, sourceProvider);
        var targetProvider = targetServices.BuildServiceProvider();

        // Act
        var sourceOptions = sourceProvider.GetRequiredService<IOptions<TestOptions>>();
        var targetOptions = targetProvider.GetRequiredService<IOptions<TestOptions>>();

        // Assert - should have same value (proxied from source)
        Assert.Equal("SourceValue", sourceOptions.Value.Value);
        Assert.Equal("SourceValue", targetOptions.Value.Value);
    }

    [Fact]
    public void IOptionsSnapshot_ScopedCorrectly()
    {
        // Arrange
        var sourceServices = new ServiceCollection();
        sourceServices.Configure<TestOptions>(opt => opt.Value = "SnapshotValue");
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();
        targetServices.AddProxiedServices(sourceServices, sourceProvider);
        var targetProvider = targetServices.BuildServiceProvider();

        // Act - IOptionsSnapshot requires a scope
        using var scope = targetProvider.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestOptions>>();

        // Assert
        Assert.Equal("SnapshotValue", snapshot.Value.Value);
    }

    [Fact]
    public void IOptionsMonitor_ProxiedCorrectly()
    {
        // Arrange
        var sourceServices = new ServiceCollection();
        sourceServices.Configure<TestOptions>(opt => opt.Value = "MonitorValue");
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();
        targetServices.AddProxiedServices(sourceServices, sourceProvider);
        var targetProvider = targetServices.BuildServiceProvider();

        // Act
        var monitor = targetProvider.GetRequiredService<IOptionsMonitor<TestOptions>>();

        // Assert
        Assert.Equal("MonitorValue", monitor.CurrentValue.Value);
    }

    [Fact]
    public void CustomOpenGeneric_ProxiedViaDynamicType()
    {
        // Arrange
        var sourceServices = new ServiceCollection();
        sourceServices.AddSingleton(typeof(IRepository<>), typeof(InMemoryRepository<>));
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();
        targetServices.AddProxiedServices(sourceServices, sourceProvider);
        var targetProvider = targetServices.BuildServiceProvider();

        // Act
        var sourceRepo = sourceProvider.GetRequiredService<IRepository<TestEntity>>();
        var targetRepo = targetProvider.GetRequiredService<IRepository<TestEntity>>();

        // Add via source
        sourceRepo.Add(new TestEntity { Id = 1, Name = "Test" });

        // Read via target - should see the same data if properly proxied
        var fromTarget = targetRepo.GetById(1);

        // Assert
        Assert.NotNull(fromTarget);
        Assert.Equal("Test", fromTarget!.Name);

        // Proxies delegate to source, so data is shared
        // But the instances are not the same (one is proxy, one is concrete)
        Assert.NotSame(sourceRepo, targetRepo);
        Assert.IsType<InMemoryRepository<TestEntity>>(sourceRepo);
        Assert.IsNotType<InMemoryRepository<TestEntity>>(targetRepo); // This is a proxy
    }

    [Fact]
    public void MultipleTypeParameters_ProxiedCorrectly()
    {
        // Arrange
        var sourceServices = new ServiceCollection();
        sourceServices.AddSingleton(typeof(IConverter<,>), typeof(DefaultConverter<,>));
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();
        targetServices.AddProxiedServices(sourceServices, sourceProvider);
        var targetProvider = targetServices.BuildServiceProvider();

        // Act
        var sourceConverter = sourceProvider.GetRequiredService<IConverter<string, int>>();
        var targetConverter = targetProvider.GetRequiredService<IConverter<string, int>>();

        var result = targetConverter.Convert("42");

        // Assert
        Assert.Equal(42, result);
        
        // Proxies delegate to source, so conversion works
        // But the instances are not the same (one is proxy, one is concrete)
        Assert.NotSame(sourceConverter, targetConverter);
    }

    [Fact]
    public void ScopedServices_CreatesPairedScopes()
    {
        // Arrange
        var sourceServices = new ServiceCollection();
        sourceServices.AddScoped<IScopedService, ScopedService>();
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();
        targetServices.AddProxiedServices(sourceServices, sourceProvider);
        var targetProvider = targetServices.BuildServiceProvider();

        // Act
        using var scope1 = targetProvider.CreateScope();
        using var scope2 = targetProvider.CreateScope();

        var service1a = scope1.ServiceProvider.GetRequiredService<IScopedService>();
        var service1b = scope1.ServiceProvider.GetRequiredService<IScopedService>();
        var service2 = scope2.ServiceProvider.GetRequiredService<IScopedService>();

        // Assert
        Assert.Same(service1a, service1b); // Same within scope
        Assert.NotSame(service1a, service2); // Different across scopes
    }

    [Fact]
    public void ResolvingScopedServiceFromRoot_Throws()
    {
        // Arrange
        var sourceServices = new ServiceCollection();
        sourceServices.AddScoped<IScopedService, ScopedService>();
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();
        targetServices.AddProxiedServices(sourceServices, sourceProvider);
        var targetProvider = targetServices.BuildServiceProvider();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            targetProvider.GetRequiredService<IScopedService>());

        Assert.Contains("Cannot resolve scoped service", ex.Message);
    }

    [Fact]
    public void AddProxiedServices_SkipsNonLoggingServices()
    {
        // Arrange
        var sourceServices = new ServiceCollection();
        sourceServices.AddLogging();  // No console provider needed for this test
        sourceServices.AddSingleton<INonLoggingService, NonLoggingService>();
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();

        // Filter to only logging services using options
        targetServices.AddProxiedServices(sourceServices, sourceProvider, new ProxyOptions
        {
            ShouldSkipService = sd =>
                sd.ServiceType.Namespace?.StartsWith("Microsoft.Extensions.Logging") != true &&
                sd.ServiceType != typeof(ILoggerFactory) &&
                !(sd.ServiceType.IsGenericType && sd.ServiceType.GetGenericTypeDefinition() == typeof(ILogger<>))
        });

        var targetProvider = targetServices.BuildServiceProvider();

        // Act
        var logger = targetProvider.GetService<ILogger<OpenGenericProxyTests>>();
        var nonLoggingService = targetProvider.GetService<INonLoggingService>();

        // Assert
        Assert.NotNull(logger); // Logging was proxied
        Assert.Null(nonLoggingService); // Non-logging service was not proxied
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Supporting Test Types (nested types not accessed by proxy generator)
    // ═══════════════════════════════════════════════════════════════════════

    private class TestClass1 { }
    private class TestClass2 { }
    private class TestClass3 { }

    private class TestOptions
    {
        public string Value { get; set; } = "";
    }

    private class NonLoggingService : INonLoggingService { }
    // Test logger provider
    private class CountingLoggerProvider : ILoggerProvider
    {
        public int LogCount { get; private set; }

        public ILogger CreateLogger(string categoryName) => new CountingLogger(this);

        public void Dispose() { }

        private class CountingLogger : ILogger
        {
            private readonly CountingLoggerProvider _provider;

            public CountingLogger(CountingLoggerProvider provider) => this._provider = provider;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                this._provider.LogCount++;
            }
        }
    }
}