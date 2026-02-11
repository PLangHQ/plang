# IO & Channels

Stream-based IO with named channels for input/output operations. The `IO` class also provides file deserialization.

## IO Class

`PLang.Runtime2.IO.IO` — manages channels and provides file I/O.

### API Surface

```csharp
public sealed class IO : IAsyncDisposable
{
    // Constructor
    public IO(Engine engine)

    // File operations (convenience — uses Engine.Serializers)
    Task<T?> ReadAsync<T>(string filePath, CancellationToken ct = default)

    // Channel management
    Channel? Get(string name)
    Channel GetOrCreate(string name, Func<Channel> factory)
    void Register(Channel channel)
    Task<bool> RemoveAsync(string name)
    bool Contains(string name)
    IEnumerable<string> ChannelNames { get; }

    // Channel read/write (returns Data, not GoalResult)
    Task<Data> WriteAsync(string channelName, object? data, string? contentType = null, CancellationToken ct = default)
    Task<Data> ReadChannelAsync<T>(string channelName, CancellationToken ct = default)
    Task<Data> WriteTextAsync(string channelName, string text, CancellationToken ct = default)
    Task<Data> ReadTextAsync(string channelName, CancellationToken ct = default)

    // Disposal
    ValueTask DisposeAsync()
}
```

### Behavior & Rules

- `IO` takes `Engine` in its constructor (not just `SerializerRegistry`)
- `ReadAsync<T>` reads a file from disk and deserializes using the appropriate serializer
- Channel operations return `Data` (not `GoalResult`)
- `WriteAsync` returns `Data.Fail` if channel doesn't exist or is read-only
- `ReadChannelAsync` returns `Data.Fail` if channel doesn't exist or is write-only
- `DisposeAsync` disposes all registered channels

### Code Examples

```csharp
// IO is created per Actor (not standalone)
var io = actor.IO;

// Read a file
var goal = await io.ReadAsync<Goal>("path/to/.build/start.pr");

// Channel operations
var result = await io.WriteTextAsync("stdout", "Hello, World!");
if (!result.Success)
    Console.Error.WriteLine(result.Error?.Message);
```

## Channel Class

`PLang.Runtime2.IO.Channel` — represents a named I/O channel backed by a Stream.

### ChannelDirection

```csharp
public enum ChannelDirection
{
    Input,
    Output,
    Bidirectional
}
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Channel identifier |
| `Stream` | `Stream` | Backing stream |
| `Direction` | `ChannelDirection` | Read/write direction |
| `ContentType` | `string?` | MIME type for serialization |
| `IsOpen` | `bool` | Whether channel is open |
| `Created` | `DateTime` | When the channel was created |
| `Metadata` | `IDictionary<string, object>` | Arbitrary metadata |
| `CanRead` | `bool` | `IsOpen && Direction != Output && Stream.CanRead` |
| `CanWrite` | `bool` | `IsOpen && Direction != Input && Stream.CanWrite` |

### Static Factories

```csharp
Channel.Input(string name, Stream stream)        // Input-only
Channel.Output(string name, Stream stream)       // Output-only
Channel.Memory(string name, ChannelDirection dir) // Memory-backed
Channel.File(string name, string path, FileMode) // File-backed
```

### Read/Write Operations

```csharp
Task<byte[]> ReadAllBytesAsync(CancellationToken ct = default)
Task<string> ReadAllTextAsync(CancellationToken ct = default)
Task WriteBytesAsync(byte[] data, CancellationToken ct = default)
Task WriteTextAsync(string text, CancellationToken ct = default)
```

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

## Actor Ownership

Each `Actor` has its own `IO` instance. The engine creates three actors with separate I/O:

```
Engine.System.IO   → System actor's channels
Engine.Service.IO  → Service actor's channels
Engine.User.IO     → User actor's channels
```

## Relationships

- `IO` uses [SerializerRegistry](serializers.md) via Engine for content-type based serialization
- `IO.WriteAsync` and `IO.ReadChannelAsync` return [Data](goal-result.md)
- `IO.ReadAsync<T>` is used by [Goals](goals-steps.md) for loading `.pr` files
- `Channel` wraps standard .NET `Stream`
- Each [Actor](contexts.md) owns an `IO` instance
