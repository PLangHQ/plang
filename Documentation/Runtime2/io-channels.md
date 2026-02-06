# IO & Channels

Stream-based IO with named channels for input/output operations.

## IO Class

Manages a collection of named channels.

### API Surface

```csharp
public sealed class IO : IAsyncDisposable
{
    // Constants
    public const string StdIn = "stdin";
    public const string StdOut = "stdout";
    public const string StdErr = "stderr";

    // Constructor
    public IO(SerializerRegistry? serializers = null)

    // Channel management
    public Channel? Get(string name)
    public Channel GetOrCreate(string name, Func<Channel> factory)
    public void Register(Channel channel)
    public Task<bool> RemoveAsync(string name)
    public bool Contains(string name)
    public IEnumerable<string> ChannelNames { get; }

    // Channel factories
    public Channel CreateMemoryChannel(string name, ChannelDirection direction = ChannelDirection.Bidirectional)
    public Channel CreateFileChannel(string name, string path, FileMode mode = FileMode.OpenOrCreate)

    // Read/Write operations
    public Task<GoalResult> WriteAsync(string channelName, object? data, string? contentType = null, CancellationToken cancellationToken = default)
    public Task<GoalResult> ReadAsync<T>(string channelName, CancellationToken cancellationToken = default)
    public Task<GoalResult> WriteTextAsync(string channelName, string text, CancellationToken cancellationToken = default)
    public Task<GoalResult> ReadTextAsync(string channelName, CancellationToken cancellationToken = default)

    // Disposal
    public ValueTask DisposeAsync()
}
```

### Behavior & Rules

- Channel names are case-insensitive
- `WriteAsync` uses channel's `ContentType` or defaults to `"application/json"`
- `ReadAsync` returns `GoalResult.Fail("ChannelNotFound")` if channel doesn't exist
- `WriteAsync` returns `GoalResult.Fail("ChannelReadOnly")` if channel is input-only
- `ReadAsync` returns `GoalResult.Fail("ChannelWriteOnly")` if channel is output-only
- `DisposeAsync` disposes all registered channels

### Code Examples

```csharp
await using var io = new IO();

// Create channels
var debugChannel = io.CreateMemoryChannel("debug");
var logChannel = io.CreateFileChannel("log", "/var/log/app.log");

// Write data
await io.WriteAsync("debug", new { level = "info", message = "Started" });
await io.WriteTextAsync("log", "Application started\n");

// Read data
var result = await io.ReadAsync<LogEntry>("debug");
if (result.Success)
{
    var entry = result.GetValue<LogEntry>();
}
```

## Channel Class

Represents a named I/O channel backed by a Stream.

### API Surface

```csharp
public enum ChannelDirection
{
    Input,
    Output,
    Bidirectional
}

public sealed class Channel : IAsyncDisposable, IDisposable
{
    // Properties
    public string Name { get; }
    public Stream Stream { get; }
    public ChannelDirection Direction { get; }
    public string? ContentType { get; set; }
    public bool IsOpen { get; }
    public DateTime Created { get; }
    public IDictionary<string, object> Metadata { get; }
    public bool CanRead { get; }
    public bool CanWrite { get; }

    // Constructor
    public Channel(string name, Stream stream, ChannelDirection direction = ChannelDirection.Bidirectional, bool ownsStream = true)

    // Static factories
    public static Channel Input(string name, Stream stream)
    public static Channel Output(string name, Stream stream)
    public static Channel Memory(string name, ChannelDirection direction = ChannelDirection.Bidirectional)
    public static Channel File(string name, string path, FileMode mode = FileMode.OpenOrCreate)

    // Read operations
    public Task<byte[]> ReadAllBytesAsync(CancellationToken cancellationToken = default)
    public Task<string> ReadAllTextAsync(CancellationToken cancellationToken = default)

    // Write operations
    public Task WriteBytesAsync(byte[] data, CancellationToken cancellationToken = default)
    public Task WriteTextAsync(string text, CancellationToken cancellationToken = default)

    // Lifecycle
    public void Close()
    public void Dispose()
    public ValueTask DisposeAsync()
}
```

### Behavior & Rules

- `CanRead` returns `true` if `IsOpen && Direction != Output && Stream.CanRead`
- `CanWrite` returns `true` if `IsOpen && Direction != Input && Stream.CanWrite`
- `ownsStream` parameter controls whether the channel disposes the stream
- `Metadata` dictionary is case-insensitive
- `ReadAllBytesAsync` on `MemoryStream` uses `ToArray()` for efficiency
- `Close()` and `Dispose()` are idempotent

### Static Factories

| Factory | Direction | Stream Type |
|---------|-----------|-------------|
| `Input(name, stream)` | Input | Provided stream |
| `Output(name, stream)` | Output | Provided stream |
| `Memory(name)` | Bidirectional | MemoryStream |
| `File(name, path, mode)` | Based on mode | FileStream |

### Code Examples

```csharp
// Memory channel
var memChannel = Channel.Memory("buffer");
await memChannel.WriteTextAsync("Hello, World!");
memChannel.Stream.Position = 0;
var text = await memChannel.ReadAllTextAsync();

// File channel
var fileChannel = Channel.File("output", "/tmp/data.txt", FileMode.Create);
await fileChannel.WriteTextAsync("Data content");
await fileChannel.DisposeAsync();

// Input-only from existing stream
var inputChannel = Channel.Input("request", httpRequest.Body);
var body = await inputChannel.ReadAllTextAsync();
```

## ChannelData Class

Represents data that can be sent/received through a channel with metadata.

### API Surface

```csharp
public sealed class ChannelData
{
    // Properties
    public object? Value { get; }
    public string? ContentType { get; }
    public IDictionary<string, string>? Metadata { get; }
    public DateTime Timestamp { get; }
    public bool IsEmpty { get; }

    // Constructor
    public ChannelData(object? value, string? contentType = null, IDictionary<string, string>? metadata = null)

    // Static factories
    public static ChannelData Json(object? value)
    public static ChannelData Text(string? value)
    public static ChannelData Binary(byte[]? value)

    // Value access
    public T? GetValue<T>()
}
```

### Code Examples

```csharp
// Create channel data
var jsonData = ChannelData.Json(new { name = "John", age = 30 });
var textData = ChannelData.Text("Hello, World!");
var binaryData = ChannelData.Binary(fileBytes);

// Access value
var person = jsonData.GetValue<Person>();
```

## Relationships

- `IO` uses [SerializerRegistry](serializers.md) for content-type based serialization
- `IO.WriteAsync` and `IO.ReadAsync` return [GoalResult](goal-result.md)
- `Channel` wraps standard .NET `Stream`
- Standard channel names (`stdin`, `stdout`, `stderr`) align with console conventions
