using System.Collections.Concurrent;
using PLang.Runtime2.Core;
using PLang.Runtime2.Errors;
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
    public const string StdIn = "stdin";
    public const string StdOut = "stdout";
    public const string StdErr = "stderr";

    public IO(Engine engine)
    {
        _engine = engine;
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
    public async Task<Return> WriteAsync(string channelName, object? data, string? contentType = null, CancellationToken cancellationToken = default)
    {
		// check: this should just return error, it should already check if it is null, maby even Get should take parameter if it should check on CanWrite, 
		// then we can move both error into Get
        var channel = Get(channelName);
        if (channel == null)
            return new Return { Error = new ServiceError($"Channel '{channelName}' not found", "ChannelNotFound", 404) };

        if (!channel.CanWrite)
            return new Return { Error = new ServiceError($"Channel '{channelName}' does not support writing", "ChannelReadOnly", 400) };

        try
        {
            contentType ??= channel.ContentType ?? "application/json";
			// check: why is it that IO is doing this??? just engine.Seriazlier.Serialize({stream:stream, data:object, contentType:string, extension:string(for the files), ct:ct});
			// object based pattern!!!!! 
            var serializer = _engine.Serializers.GetOrDefault(contentType);
            await serializer.SerializeAsync(channel.Stream, data, cancellationToken: cancellationToken);
            return new Return();
        }
        catch (Exception ex)
        {
            return new Return { Error = new ServiceError($"Failed to write to channel '{channelName}': {ex.Message}", "WriteError") { Exception = ex } };
        }
    }

    /// <summary>
    /// Reads data from a channel.
    /// </summary>
    public async Task<Return> ReadChannelAsync<T>(string channelName, CancellationToken cancellationToken = default)
    {
		// check: this hole method like Read
        var channel = Get(channelName);
        if (channel == null)
            return new Return { Error = new ServiceError($"Channel '{channelName}' not found", "ChannelNotFound", 404) };

        if (!channel.CanRead)
            return new Return { Error = new ServiceError($"Channel '{channelName}' does not support reading", "ChannelWriteOnly", 400) };

        try
        {
            var contentType = channel.ContentType ?? "application/json";
            var serializer = _engine.Serializers.GetOrDefault(contentType);
            var result = await serializer.DeserializeAsync<T>(channel.Stream, cancellationToken);
            return new Return { Value = result };
        }
        catch (Exception ex)
        {
            return new Return { Error = new ServiceError($"Failed to read from channel '{channelName}': {ex.Message}", "ReadError") { Exception = ex } };
        }
    }

    /// <summary>
    /// Writes text to a channel.
    /// </summary>
    public async Task<Return> WriteTextAsync(string channelName, string text, CancellationToken cancellationToken = default)
    {
		// check: you know what, just fix in all class
        var channel = Get(channelName);
        if (channel == null)
            return new Return { Error = new ServiceError($"Channel '{channelName}' not found", "ChannelNotFound", 404) };

        try
        {
            await channel.WriteTextAsync(text, cancellationToken);
            return new Return();
        }
        catch (Exception ex)
        {
            return new Return { Error = new ServiceError($"Failed to write text to channel '{channelName}': {ex.Message}", "WriteError") { Exception = ex } };
        }
    }

    /// <summary>
    /// Reads text from a channel.
    /// </summary>
    public async Task<Return> ReadTextAsync(string channelName, CancellationToken cancellationToken = default)
    {
        var channel = Get(channelName);
        if (channel == null)
            return new Return { Error = new ServiceError($"Channel '{channelName}' not found", "ChannelNotFound", 404) };

        try
        {
            var text = await channel.ReadAllTextAsync(cancellationToken);
            return new Return { Value = text };
        }
        catch (Exception ex)
        {
            return new Return { Error = new ServiceError($"Failed to read text from channel '{channelName}': {ex.Message}", "ReadError") { Exception = ex } };
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
