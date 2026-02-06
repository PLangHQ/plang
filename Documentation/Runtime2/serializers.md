# Serializers

Content-type based serialization for data format handling. Stream-based API for efficiency.

## ISerializer Interface

### API Surface

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

### Design Philosophy

- Stream-based methods are the primary API for efficiency
- String-based methods are convenience wrappers
- Content type identifies the format (e.g., `"application/json"`)
- File extension for file-based operations (e.g., `".json"`)

## SerializerRegistry

Manages serializers by content type and file extension.

### API Surface

```csharp
public sealed class SerializerRegistry
{
    // Registration
    public void Register(ISerializer serializer)

    // Lookup
    public ISerializer? GetByContentType(string contentType)
    public ISerializer? GetByExtension(string extension)
    public ISerializer GetOrDefault(string? contentType)

    // Built-in accessors
    public ISerializer Default { get; set; }
    public ISerializer Json { get; }
    public ISerializer Text { get; }

    // Enumeration
    public IEnumerable<string> ContentTypes { get; }
    public IEnumerable<string> Extensions { get; }
}
```

### Behavior & Rules

- Constructor registers `JsonStreamSerializer` and `TextStreamSerializer` by default
- `Default` is `JsonStreamSerializer` by default
- `GetByContentType` strips charset suffix (e.g., `"application/json; charset=utf-8"` → `"application/json"`)
- `GetByExtension` accepts with or without leading dot
- `GetOrDefault` returns `Default` if content type not found

### Code Examples

```csharp
var registry = new SerializerRegistry();

// Lookup
var json = registry.GetByContentType("application/json");
var text = registry.GetByExtension(".txt");
var fallback = registry.GetOrDefault("unknown/type");  // returns Default (JSON)

// Register custom serializer
registry.Register(new XmlSerializer());
```

## JsonStreamSerializer

JSON serialization using `System.Text.Json`.

### API Surface

```csharp
public sealed class JsonStreamSerializer : ISerializer
{
    public string ContentType => "application/json";
    public string FileExtension => ".json";

    public JsonStreamSerializer(JsonSerializerOptions? options = null)

    public JsonStreamSerializer WithIndentation()
}
```

### Behavior & Rules

- Uses `System.Text.Json` internally
- Default options:
  - `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
  - `PropertyNameCaseInsensitive = true`
  - `WriteIndented = false`
  - `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`
  - Includes `JsonStringEnumConverter`
- `WithIndentation()` returns a new instance with pretty-printing enabled

### Code Examples

```csharp
var json = new JsonStreamSerializer();

// Serialize to stream
await using var stream = new MemoryStream();
await json.SerializeAsync(stream, new { name = "John", age = 30 });
// stream contains: {"name":"John","age":30}

// Deserialize from stream
stream.Position = 0;
var obj = await json.DeserializeAsync<Person>(stream);

// String convenience methods
var str = json.Serialize(new { name = "John" });  // {"name":"John"}
var person = json.Deserialize<Person>(str);

// Pretty-printed output
var prettyJson = json.WithIndentation();
var formatted = prettyJson.Serialize(data);
```

## TextStreamSerializer

Plain text serialization using `ToString()`.

### API Surface

```csharp
public sealed class TextStreamSerializer : ISerializer
{
    public string ContentType => "text/plain";
    public string FileExtension => ".txt";

    public TextStreamSerializer(Encoding? encoding = null)
}
```

### Behavior & Rules

- Serializes by calling `ToString()` on the value
- Deserializes by parsing based on target type
- Supports basic types: `int`, `long`, `double`, `decimal`, `bool`, `DateTime`, `Guid`, `byte[]`
- Unknown types return the raw string
- Default encoding is UTF-8

### Code Examples

```csharp
var text = new TextStreamSerializer();

// Serialize
var str = text.Serialize(123);      // "123"
var str2 = text.Serialize(true);    // "True"

// Deserialize
var num = text.Deserialize<int>("123");       // 123
var flag = text.Deserialize<bool>("true");    // true
var date = text.Deserialize<DateTime>("2024-01-15");  // DateTime
```

## Custom Serializer Example

```csharp
public class XmlSerializer : ISerializer
{
    public string ContentType => "application/xml";
    public string FileExtension => ".xml";

    public async Task SerializeAsync(Stream stream, object? value, Type? type = null, CancellationToken cancellationToken = default)
    {
        if (value == null)
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes("<null/>"), cancellationToken);
            return;
        }

        type ??= value.GetType();
        var serializer = new System.Xml.Serialization.XmlSerializer(type);
        serializer.Serialize(stream, value);
    }

    public async Task<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default)
    {
        var serializer = new System.Xml.Serialization.XmlSerializer(type);
        return serializer.Deserialize(stream);
    }

    public async Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        var result = await DeserializeAsync(stream, typeof(T), cancellationToken);
        return result is T typed ? typed : default;
    }

    public string Serialize(object? value, Type? type = null)
    {
        using var stream = new MemoryStream();
        SerializeAsync(stream, value, type).Wait();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public object? Deserialize(string data, Type type)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
        return DeserializeAsync(stream, type).Result;
    }

    public T? Deserialize<T>(string data)
    {
        var result = Deserialize(data, typeof(T));
        return result is T typed ? typed : default;
    }
}

// Registration
var registry = new SerializerRegistry();
registry.Register(new XmlSerializer());
```

## Relationships

- Stored in [Engine](engine.md) as `Serializers` property
- Used by [IO](io-channels.md) for channel read/write operations
- `SerializerRegistry` is passed to `IO` constructor
- Content type on [Channel](io-channels.md) determines which serializer is used
