namespace Femur.Messaging;

/// <summary>
/// Handles serialization and deserialization of message bodies.
/// Implement this interface to support different formats (JSON, XML, Protobuf, MessagePack, etc.)
/// </summary>
public interface IMessageSerializer
{
    /// <summary>
    /// Serialize a message object to binary data.
    /// </summary>
    ReadOnlyMemory<byte> Serialize<T>(T message) where T : class;

    /// <summary>
    /// Deserialize binary data to a message object of type T.
    /// </summary>
    T Deserialize<T>(ReadOnlyMemory<byte> data) where T : class;
}
