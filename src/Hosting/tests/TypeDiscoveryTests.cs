using Femur.Hosting;

namespace HostingTests;

public class TypeDiscoveryTests
{
    [Fact]
    public void DiscoverProgramType_InTestContext_ReturnsNonNullType()
    {
        // In test context, we expect fallback behavior since there's no "Program" class
        var discoveredType = TypeDiscovery.GetDiscoveredProgramType();

        Assert.NotNull(discoveredType);
    }

    [Fact]
    public void DiscoverProgramType_IsCached_ReturnsSameInstanceOnMultipleCalls()
    {
        var first = TypeDiscovery.GetDiscoveredProgramType();
        var second = TypeDiscovery.GetDiscoveredProgramType();

        Assert.Same(first, second);
    }

    [Fact]
    public void GetAutoDiscoveredLoggerCategoryName_InTestContext_ReturnsNonEmptyString()
    {
        var categoryName = TypeDiscovery.GetAutoDiscoveredLoggerCategoryName();

        Assert.False(string.IsNullOrEmpty(categoryName));
    }

    [Fact]
    public void GetAutoDiscoveredLoggerCategoryName_IsCached_ReturnsSameInstanceOnMultipleCalls()
    {
        var first = TypeDiscovery.GetAutoDiscoveredLoggerCategoryName();
        var second = TypeDiscovery.GetAutoDiscoveredLoggerCategoryName();

        Assert.Same(first, second);
    }

    [Theory]
    [InlineData(typeof(System.String), "System")]
    [InlineData(typeof(System.Collections.Generic.List<>), "System.Collections.Generic")]
    [InlineData(typeof(Microsoft.Extensions.Logging.ILogger), "Microsoft.Extensions.Logging")]
    public void GetLoggerCategoryName_WithNamespace_ReturnsNamespace(Type type, string expected)
    {
        var result = TypeDiscovery.GetLoggerCategoryName(type);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetLoggerCategoryName_WithTypeInTestNamespace_ReturnsNamespace()
    {
        // TypeInGlobalNamespace is actually in HostingTests namespace
        var type = typeof(TypeInGlobalNamespace);
        var result = TypeDiscovery.GetLoggerCategoryName(type);

        Assert.Equal("HostingTests", result);
    }

    [Fact]
    public void GetLoggerCategoryName_WithNestedType_ReturnsOuterNamespace()
    {
        // For nested types like OuterClass.InnerClass in namespace MyApp,
        // we expect "MyApp" not "MyApp.OuterClass.InnerClass"
        var nestedType = typeof(OuterClassForTesting.InnerClassForTesting);
        var result = TypeDiscovery.GetLoggerCategoryName(nestedType);

        Assert.Equal("HostingTests", result);
    }

    [Fact]
    public void GetLoggerCategoryName_WithGenericType_ReturnsNamespaceNotGenericPart()
    {
        var genericType = typeof(System.Collections.Generic.List<string>);
        var result = TypeDiscovery.GetLoggerCategoryName(genericType);

        Assert.Equal("System.Collections.Generic", result);
    }

    [Fact]
    public void GetLoggerCategoryName_WithObjectType_ReturnsFallbackValue()
    {
        // typeof(object) is our ultimate fallback in DiscoverProgramType
        var result = TypeDiscovery.GetLoggerCategoryName(typeof(object));

        // object is in System namespace
        Assert.Equal("System", result);
    }
}

// Test helper classes

// Class in global namespace for testing
public class TypeInGlobalNamespace { }

// Outer class for nested type testing
public class OuterClassForTesting
{
    public class InnerClassForTesting { }
}
