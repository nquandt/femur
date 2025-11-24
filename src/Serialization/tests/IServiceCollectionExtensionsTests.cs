// ```test/Serialization.Tests/IServiceCollectionExtensionsTests.cs
using Microsoft.Extensions.DependencyInjection;

namespace Femur.Serialization.Tests
{
    public class IServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddDefaultJsonSerializer_RegistersServices()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            _ = services.AddDefaultJsonSerializer();
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var factory = serviceProvider.GetService<IAsyncSerializerFactory>();
            Assert.NotNull(factory);
            Assert.True(factory.SupportsContentType("application/json"));
        }
    }
}