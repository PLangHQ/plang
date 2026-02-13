using System.Collections.Concurrent;
using PLang.Runtime2.Core;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;
using PLang.Runtime2.Serialization;

namespace PLang.Runtime2.IO;

/// <summary>
/// Manages named channels for I/O operations in Runtime2.
/// </summary>
public sealed class IO : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Channel> _channels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Engine _engine;

    // Standard channel names
    public const string Default = "default";
    public const string StdIn = "stdin";
    public const string StdOut = "stdout";
    public const string StdErr = "stderr";

    public IO(Engine engine)
    {
        _engine = engine;
        Register(new Channel(Default, Console.OpenStandardOutput(), ChannelDirection.Output, ownsStream: false)
            { ContentType = "text/plain" });
    }

    /// <summary>
    /// Routes a write to the correct actor's IO by actor name.
    /// Engine.IO is the router — actor IOs own the actual channels.
    /// </summary>
    public async Task<Data> WriteAsync(string actorName, string channelName, object? data, CancellationToken ct = default)
    {
        var (actor, error) = _engine.GetActor(actorName);
        if (error != null) return Data.FromError(error);
        return await actor!.IO.WriteAsync(channelName, data, cancellationToken: ct);
    }

    /// <summary>
    /// Reads a file and deserializes its content to the specified type.
    /// </summary>
    public async Task<T?> ReadAsync<T>(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await _engine.FileSystem.File.ReadAllTextAsync(filePath, cancellationToken);
        var ext = System.IO.Path.GetExtension(filePath);
        return _engine.Serializers.Deserialize<T>(new DeserializeOptions { Value = content, Extension = ext });
    }

    /// <summary>
    /// Gets or creates a channel by name.
    /// </summary>
    public Channel GetOrCreate(string name, Func<Channel> factory)
    {
        return _channels.GetOrAdd(name, _ => factory());
    }

    /// <summary>
    /// Gets a channel by name.
    /// </summary>
    public Channel? Get(string name)
    {
        return _channels.TryGetValue(name, out var channel) ? channel : null;
    }

    /// <summary>
    /// Gets a channel and validates existence and permissions.
    /// </summary>
    private (Channel? Channel, Data? Error) GetChannel(string name, bool? requireRead = null, bool? requireWrite = null)
    {
        var channel = Get(name);
        if (channel == null)
            return (null, Data.FromError(new ServiceError($"Channel '{name}' not found", "ChannelNotFound", 404)));

        if (requireRead == true && !channel.CanRead)
            return (null, Data.FromError(new ServiceError($"Channel '{name}' does not support reading", "ChannelWriteOnly", 400)));

        if (requireWrite == true && !channel.CanWrite)
            return (null, Data.FromError(new ServiceError($"Channel '{name}' does not support writing", "ChannelReadOnly", 400)));

        return (channel, null);
    }

    /// <summary>
    /// Registers a channel.
    /// </summary>
    public void Register(Channel channel)
    {
        _channels[channel.Name] = channel;
    }

    /// <summary>
    /// Removes and disposes a channel.
    /// </summary>
    public async Task<bool> RemoveAsync(string name)
    {
        if (_channels.TryRemove(name, out var channel))
        {
            await channel.DisposeAsync();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if a channel exists.
    /// </summary>
    public bool Contains(string name) => _channels.ContainsKey(name);

    /// <summary>
    /// Gets all channel names.
    /// </summary>
    public IEnumerable<string> ChannelNames => _channels.Keys;

    /// <summary>
    /// Writes data to a channel.
    /// </summary>
    public async Task<Data> WriteAsync(string channelName, object? data, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireWrite: true);
        if (error != null) return error;

        try
        {
            await _engine.Serializers.SerializeAsync(new SerializeOptions
            {
                Stream = channel!.Stream,
                Data = data,
                ContentType = contentType ?? channel.ContentType ?? "application/json",
                CancellationToken = cancellationToken
            });
            return Data.Ok();
        }
        catch (Exception ex)
        {
            return Data.FromError(new ServiceError($"Failed to write to channel '{channelName}': {ex.Message}", "WriteError") { Exception = ex });
        }
    }

    /// <summary>
    /// Reads data from a channel.
    /// </summary>
    public async Task<Data> ReadChannelAsync<T>(string channelName, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireRead: true);
        if (error != null) return error;

        try
        {
            var result = await _engine.Serializers.DeserializeAsync<T>(new DeserializeOptions
            {
                Stream = channel!.Stream,
                ContentType = channel.ContentType ?? "application/json",
                CancellationToken = cancellationToken
            });
            return Data.Ok(result);
        }
        catch (Exception ex)
        {
            return Data.FromError(new ServiceError($"Failed to read from channel '{channelName}': {ex.Message}", "ReadError") { Exception = ex });
        }
    }

    /// <summary>
    /// Writes text to a channel.
    /// </summary>
    public async Task<Data> WriteTextAsync(string channelName, string text, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireWrite: true);
        if (error != null) return error;

        try
        {
            await channel!.WriteTextAsync(text, cancellationToken);
            return Data.Ok();
        }
        catch (Exception ex)
        {
            return Data.FromError(new ServiceError($"Failed to write text to channel '{channelName}': {ex.Message}", "WriteError") { Exception = ex });
        }
    }

    /// <summary>
    /// Reads text from a channel.
    /// </summary>
    public async Task<Data> ReadTextAsync(string channelName, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireRead: true);
        if (error != null) return error;

        try
        {
            var text = await channel!.ReadAllTextAsync(cancellationToken);
            return Data.Ok(text);
        }
        catch (Exception ex)
        {
            return Data.FromError(new ServiceError($"Failed to read text from channel '{channelName}': {ex.Message}", "ReadError") { Exception = ex });
        }
    }

    /// <summary>
    /// Creates a memory channel.
    /// </summary>
    public Channel CreateMemoryChannel(string name, ChannelDirection direction = ChannelDirection.Bidirectional)
    {
        var channel = Channel.Memory(name, direction);
        Register(channel);
        return channel;
    }

    /// <summary>
    /// Creates a file channel.
    /// </summary>
    public Channel CreateFileChannel(string name, string path, FileMode mode = FileMode.OpenOrCreate)
    {
        var channel = Channel.File(name, path, mode);
        Register(channel);
        return channel;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var channel in _channels.Values)
        {
            await channel.DisposeAsync();
        }
        _channels.Clear();
    }
}
