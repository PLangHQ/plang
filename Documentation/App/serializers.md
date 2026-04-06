# Serializers

Content-type based serialization for data format handling. Stream-based API for efficiency.

## ISerializer Interface

```csharp
public interface ISerializer
{
    // Identity
    string ContentType { get; }
    string FileExtension { get; }

    // Stream-based (primary API)
    Task SerializeAsync(Stream stream, object? value, Type? type = null, CancellationToken cancellationToken = default);
    Task<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default);
    Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default);

    // String-based (convenience)
    string Serialize(object? value, Type? type = null);
    object? Deserialize(string data, Type type);
    T? Deserialize<T>(string data);
}
```

## SerializerRegistry

Manages serializers by content type and file extension.

```csharp
public sealed class SerializerRegistry
{
    // Registration
    void Register(ISerializer serializer)

    // Lookup
    ISerializer? GetByContentType(string contentType)
    ISerializer? GetByExtension(string extension)
    ISerializer GetOrDefault(string? contentType)

    // Built-in accessors
    ISerializer Default { get; set; }
    ISerializer Json { get; }
    ISerializer Text { get; }

    // Enumeration
    IEnumerable<string> ContentTypes { get; }
    IEnumerable<string> Extensions { get; }

    // Options
    SerializeOptions SerializeOptions { get; set; }
    DeserializeOptions DeserializeOptions { get; set; }

    // Convenience methods
    T? Deserialize<T>(string data, string? contentType = null)
    Task SerializeAsync(Stream stream, object? value, string? contentType = null, CancellationToken ct = default)
    Task<T?> DeserializeAsync<T>(Stream stream, string? contentType = null, CancellationToken ct = default)
}
```

### Behavior & Rules

- Constructor registers JSON and text serializers by default
- `Default` is the JSON serializer by default
- `GetByContentType` strips charset suffix (e.g., `"application/json; charset=utf-8"` → `"application/json"`)
- `GetByExtension` accepts with or without leading dot
- `GetOrDefault` returns `Default` if content type not found
- `SerializeOptions` / `DeserializeOptions` provide global serialization settings

### Code Examples

```csharp
var registry = new SerializerRegistry();

// Lookup
var json = registry.GetByContentType("application/json");
var text = registry.GetByExtension(".txt");
var fallback = registry.GetOrDefault("unknown/type");  // returns Default (JSON)

// Register custom serializer
registry.Register(new XmlSerializer());

// Convenience deserialization
var goal = registry.Deserialize<Goal>(jsonString);
```

## Built-in Serializers

### JsonStreamSerializer

- Content type: `application/json`
- Extension: `.json`
- Uses `System.Text.Json`
- Default options: camelCase, case-insensitive, null ignored, enum as string

### TextStreamSerializer

- Content type: `text/plain`
- Extension: `.txt`
- Serializes via `ToString()`, deserializes by parsing target type
- Default encoding: UTF-8

## Custom Serializer Example

```csharp
public class XmlSerializer : ISerializer
{
    public string ContentType => "application/xml";
    public string FileExtension => ".xml";

    // Implement stream and string APIs...
}

// Registration
var registry = new SerializerRegistry();
registry.Register(new XmlSerializer());
```

## Relationships

- Stored in [Engine](engine.md) as `Serializers` property
- Also stored in [PLangAppContext](contexts.md) as `Serializers`
- Used by [IO](io-channels.md) for channel read/write operations
- Used by [PrParser](pr-file-format.md) for `.pr` file deserialization
- Content type on [Channel](io-channels.md) determines which serializer is used
