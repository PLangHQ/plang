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
    private readonly SerializerRegistry _serializers;

    // Standard channel names
    public const string StdIn = "stdin";
    public const string StdOut = "stdout";
    public const string StdErr = "stderr";

    public IO(SerializerRegistry? serializers = null)
    {
        _serializers = serializers ?? new SerializerRegistry();
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
    public async Task<GoalResult> WriteAsync(string channelName, object? data, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var channel = Get(channelName);
        if (channel == null)
            return GoalResult.Fail($"Channel '{channelName}' not found", "ChannelNotFound", 404);

        if (!channel.CanWrite)
            return GoalResult.Fail($"Channel '{channelName}' does not support writing", "ChannelReadOnly", 400);

        try
        {
            contentType ??= channel.ContentType ?? "application/json";
            var serializer = _serializers.GetOrDefault(contentType);
            await serializer.SerializeAsync(channel.Stream, data, cancellationToken: cancellationToken);
            return GoalResult.Ok();
        }
        catch (Exception ex)
        {
            return GoalResult.Fail(new ErrorInfo($"Failed to write to channel '{channelName}': {ex.Message}", "WriteError")
            {
                Exception = ex
            });
        }
    }

    /// <summary>
    /// Reads data from a channel.
    /// </summary>
    public async Task<GoalResult> ReadAsync<T>(string channelName, CancellationToken cancellationToken = default)
    {
        var channel = Get(channelName);
        if (channel == null)
            return GoalResult.Fail($"Channel '{channelName}' not found", "ChannelNotFound", 404);

        if (!channel.CanRead)
            return GoalResult.Fail($"Channel '{channelName}' does not support reading", "ChannelWriteOnly", 400);

        try
        {
            var contentType = channel.ContentType ?? "application/json";
            var serializer = _serializers.GetOrDefault(contentType);
            var result = await serializer.DeserializeAsync<T>(channel.Stream, cancellationToken);
            return GoalResult.Ok(result);
        }
        catch (Exception ex)
        {
            return GoalResult.Fail(new ErrorInfo($"Failed to read from channel '{channelName}': {ex.Message}", "ReadError")
            {
                Exception = ex
            });
        }
    }

    /// <summary>
    /// Writes text to a channel.
    /// </summary>
    public async Task<GoalResult> WriteTextAsync(string channelName, string text, CancellationToken cancellationToken = default)
    {
        var channel = Get(channelName);
        if (channel == null)
            return GoalResult.Fail($"Channel '{channelName}' not found", "ChannelNotFound", 404);

        try
        {
            await channel.WriteTextAsync(text, cancellationToken);
            return GoalResult.Ok();
        }
        catch (Exception ex)
        {
            return GoalResult.Fail(new ErrorInfo($"Failed to write text to channel '{channelName}': {ex.Message}", "WriteError")
            {
                Exception = ex
            });
        }
    }

    /// <summary>
    /// Reads text from a channel.
    /// </summary>
    public async Task<GoalResult> ReadTextAsync(string channelName, CancellationToken cancellationToken = default)
    {
        var channel = Get(channelName);
        if (channel == null)
            return GoalResult.Fail($"Channel '{channelName}' not found", "ChannelNotFound", 404);

        try
        {
            var text = await channel.ReadAllTextAsync(cancellationToken);
            return GoalResult.Ok(text);
        }
        catch (Exception ex)
        {
            return GoalResult.Fail(new ErrorInfo($"Failed to read text from channel '{channelName}': {ex.Message}", "ReadError")
            {
                Exception = ex
            });
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
