# Custom Serializer Example

This example demonstrates how to use custom serializers with the Femur messaging framework.

## Overview

By default, the framework uses JSON serialization (`JsonMessageSerializer`). However, you can provide your own serializer implementation to use XML, Protobuf, MessagePack, or any other format.

## Key Concepts

### IMessageSerializer Interface

```csharp
public interface IMessageSerializer
{
    ReadOnlyMemory<byte> Serialize<T>(T message) where T : class;
    T Deserialize<T>(ReadOnlyMemory<byte> data) where T : class;
}
```

### Usage Patterns

**Default JSON (implicit):**
```csharp
services.AddMessageHandler<OrderMessage, OrderHandler>();
// Uses JsonMessageSerializer by default
```

**Custom JSON options:**
```csharp
var options = new JsonSerializerOptions { WriteIndented = true };
services.AddMessageHandler<OrderMessage, OrderHandler>(
    new JsonMessageSerializer(options));
```

**Custom serializer:**
```csharp
services.AddMessageHandler<OrderMessage, OrderHandler>(
    new XmlMessageSerializer());
```

## Why Custom Serializers?

- **XML**: Legacy systems or enterprise requirements
- **Protobuf/MessagePack**: High-performance binary formats
- **Custom formats**: Domain-specific serialization needs
- **Raw data**: Pass binary data without serialization

## Running the Example

```bash
dotnet run --project src/Messaging/examples/CustomSerializer
```

The example shows:
1. Default JSON serialization
2. Customized JSON options (snake_case, indented)
3. XML serializer (demonstration pattern)

## Important Notes

- **Same serializer required**: Handler and client must use the same serializer
- **Type safety**: Serializer handles `ReadOnlyMemory<byte>` for cross-platform compatibility
- **Per-message configuration**: Different messages can use different serializers
