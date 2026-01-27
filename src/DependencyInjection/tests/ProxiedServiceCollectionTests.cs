using Femur.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DependencyInjectionTests;

// Test services - must be top-level for dynamic proxy generation to access them
public interface ITestService { string GetValue(); }
public class TestService : ITestService
{
    private readonly string _value;
    public TestService(string value) => _value = value;
    public string GetValue() => _value;
}

public interface IGenericService<T> { T GetValue(); }
public class GenericService<T> : IGenericService<T>
{
    private readonly T _value;
    public GenericService(T value) => _value = value;
    public T GetValue() => _value;
}

public class ProxiedServiceCollectionTests
{
    [Fact]
    public void AddProxiedServices_ResolvesFromSourceProvider()
    {
        // Arrange
        var sourceServices = new ServiceCollection();
        sourceServices.AddSingleton<ITestService>(new TestService("source-value"));
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();

        // Act
        targetServices.AddProxiedServices(sourceServices, sourceProvider);
        var targetProvider = targetServices.BuildServiceProvider();

        // Assert
        var service = targetProvider.GetRequiredService<ITestService>();
        Assert.Equal("source-value", service.GetValue());
    }

    [Fact]
    public void AddProxiedServices_PreservesImplementationInstance()
    {
        // Arrange
        var instance = new TestService("singleton-instance");
        var sourceServices = new ServiceCollection();
        sourceServices.AddSingleton<ITestService>(instance);
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();

        // Act
        targetServices.AddProxiedServices(sourceServices, sourceProvider);
        var targetProvider = targetServices.BuildServiceProvider();

        // Assert
        var service = targetProvider.GetRequiredService<ITestService>();
        Assert.Same(instance, service); // Should be the exact same instance
    }

    [Fact]
    public void AddProxiedServices_HandlesOpenGenericTypes()
    {
        // Arrange
        var sourceServices = new ServiceCollection();
        sourceServices.AddSingleton(typeof(IGenericService<>), typeof(GenericService<>));
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();

        // Act
        targetServices.AddProxiedServices(sourceServices, sourceProvider);
        var targetProvider = targetServices.BuildServiceProvider();

        // Assert - This would throw if open generics weren't handled correctly
        var descriptor = targetServices.FirstOrDefault(d => d.ServiceType == typeof(IGenericService<>));
        Assert.NotNull(descriptor);
        
        // Dynamic proxies are generated for open generic interfaces
        Assert.NotNull(descriptor.ImplementationType);
        Assert.StartsWith("DynamicProxy_", descriptor.ImplementationType!.Name);
    }

    [Fact]
    public void AddProxiedServices_PreservesExistingFactory_WhenConfigured()
    {
        // Arrange
        var factoryCalled = false;
        var sourceServices = new ServiceCollection();
        sourceServices.AddSingleton<ITestService>(_ =>
        {
            factoryCalled = true;
            return new TestService("factory-value");
        });
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();

        // Act
        var options = new ProxyOptions { PreserveExistingFactories = true };
        targetServices.AddProxiedServices(sourceServices, sourceProvider, options);

        // Assert - Factory should be preserved in descriptor, not called yet
        Assert.False(factoryCalled);
    }

    [Fact]
    public void AddProxiedServices_ProxiesExistingFactory_ByDefault()
    {
        // Arrange
        var sourceServices = new ServiceCollection();
        sourceServices.AddSingleton<ITestService>(_ => new TestService("factory-value"));
        var sourceProvider = sourceServices.BuildServiceProvider();

        // Pre-resolve to ensure it's in the source provider
        sourceProvider.GetRequiredService<ITestService>();

        var targetServices = new ServiceCollection();

        // Act - Use default options (PreserveExistingFactories = false)
        targetServices.AddProxiedServices(sourceServices, sourceProvider);
        var targetProvider = targetServices.BuildServiceProvider();

        // Assert - Should resolve the same instance from source provider
        var sourceService = sourceProvider.GetRequiredService<ITestService>();
        var targetService = targetProvider.GetRequiredService<ITestService>();
        Assert.Same(sourceService, targetService);
    }

    [Fact]
    public void AddProxiedServices_RespectsServiceFilter()
    {
        // Arrange
        var sourceServices = new ServiceCollection();
        sourceServices.AddSingleton<ITestService>(new TestService("value1"));
        sourceServices.AddSingleton<IGenericService<string>>(new GenericService<string>("value2"));
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();

        // Act - Skip ITestService
        var options = new ProxyOptions
        {
            ShouldSkipService = descriptor => descriptor.ServiceType == typeof(ITestService)
        };
        targetServices.AddProxiedServices(sourceServices, sourceProvider, options);

        // Assert
        Assert.DoesNotContain(targetServices, d => d.ServiceType == typeof(ITestService));
        Assert.Contains(targetServices, d => d.ServiceType == typeof(IGenericService<string>));
    }

    [Fact]
    public void AddProxiedServices_PreservesLifetime()
    {
        // Arrange
        var sourceServices = new ServiceCollection();
        sourceServices.AddTransient<ITestService>(_ => new TestService("transient"));
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();

        // Act
        targetServices.AddProxiedServices(sourceServices, sourceProvider);

        // Assert
        var descriptor = targetServices.First(d => d.ServiceType == typeof(ITestService));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void AddProxiedServices_SharesSingletonInstances()
    {
        // Arrange
        var sourceServices = new ServiceCollection();
        sourceServices.AddSingleton<ITestService>(_ => new TestService("singleton"));
        var sourceProvider = sourceServices.BuildServiceProvider();

        // Pre-resolve to create the singleton in source
        var sourceInstance = sourceProvider.GetRequiredService<ITestService>();

        var targetServices = new ServiceCollection();

        // Act
        targetServices.AddProxiedServices(sourceServices, sourceProvider);
        var targetProvider = targetServices.BuildServiceProvider();

        // Assert - Both providers should return the same singleton instance
        var targetInstance = targetProvider.GetRequiredService<ITestService>();
        Assert.Same(sourceInstance, targetInstance);
    }

    [Fact]
    public void AddProxiedServices_HandlesMultipleServicesOfSameType()
    {
        // Arrange
        var sourceServices = new ServiceCollection();
        sourceServices.AddSingleton<ITestService>(new TestService("first"));
        sourceServices.AddSingleton<ITestService>(new TestService("second"));
        var sourceProvider = sourceServices.BuildServiceProvider();

        var targetServices = new ServiceCollection();

        // Act
        targetServices.AddProxiedServices(sourceServices, sourceProvider);
        var targetProvider = targetServices.BuildServiceProvider();

        // Assert
        var services = targetProvider.GetServices<ITestService>().ToList();
        Assert.Equal(2, services.Count);
    }
}
