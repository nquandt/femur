# Femur.Serialization

Femur.Serialization is a flexible and extensible serialization library for .NET, designed to support asynchronous serialization and deserialization of objects across various content types. With built-in support for JSON, it provides a pattern and guide for standardizing serializers based on web and REST content types. 

**Note:** This project is not a complete serialization library but rather a framework to establish a consistent approach to serialization and deserialization for various content types, emphasizing extensibility and adherence to web standards.

## Features

- Asynchronous support for serialization and deserialization.
- Extensible architecture allowing additional serializers for different content types.
- Built-in JSON serialization using `System.Text.Json`.
- Strongly typed interfaces for better type safety.

## Getting Started

### Installation

You can install the Femur.Serialization library via NuGet Package Manager:

```bash
dotnet add package Femur.Serialization
```

## Usage

To start using the Femur.Serialization library in your project, follow these steps:

1. **Set Up Dependency Injection:**
   Use a `ServiceCollection` to register the serializers for your application. You can use the provided extension methods to add serializers easily.

2. **Register Serializers:**
   Register your desired serializers using the extension method `AddSerializer<T>()`. You can also add a default JSON serializer, using the extension method `AddDefaultJsonSerializer(JsonSerializerOptions? options)`.

3. **Resolve the Serializer Factory:**
   Retrieve the `IAsyncSerializerFactory` from the service provider to perform serialization or deserialization.

### Example

Here’s a sample implementation demonstrating these steps:

```csharp
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using Femur.Serialization;

public class Example
{
    public async Task RunExample()
    {
        // Set up the service collection and configure the serializer
        var services = new ServiceCollection();
        services.AddDefaultJsonSerializer(); // Register the default JSON serializer
        var serviceProvider = services.BuildServiceProvider();

        // Resolve the IAsyncSerializerFactory
        var factory = serviceProvider.GetRequiredService<IAsyncSerializerFactory>();

        var myObject = new TestClass { Name = "Test" };
        using var stream = new MemoryStream();

        // Serialize to JSON
        await factory.SerializeAsync(stream, myObject, "application/json");

        // Reset stream position for reading
        stream.Position = 0; 

        // Deserialize from JSON
        var result = await factory.DeserializeAsync<TestClass>(stream, "application/json");
        Console.WriteLine(result?.Name);  // Output: Test
    }

    private class TestClass
    {
        public string Name { get; set; } = default!;
    }
}
```

In this example, the `ServiceCollection` is configured to register the default JSON serializer. You can then resolve the `IAsyncSerializerFactory` from the service provider to handle the serialization and deserialization tasks easily.

### Running Tests

The library includes a comprehensive set of unit tests to ensure reliability. To run the tests, utilize the following command:

```bash
dotnet test
```

## Contributing

Contributions are welcome! If you have suggestions or improvements, feel free to open an issue or submit a pull request.