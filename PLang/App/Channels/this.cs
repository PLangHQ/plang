using System.Collections.Concurrent;
using App;
using App.Errors;
using App.Variables;
namespace App.Channels;

/// <summary>
/// Manages named channels for stream-based I/O in App.
/// </summary>
public sealed class @this : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Channel.@this> _channels = new(StringComparer.OrdinalIgnoreCase);
    private readonly App.@this _app;

    /// <summary>
    /// The serializer registry — content-type routing for I/O.
    /// </summary>
    public Serializers.@this Serializers { get; }

    // Standard channel names
    public const string Default = "default";
    public const string StdIn = "stdin";
    public const string StdOut = "stdout";
    public const string StdErr = "stderr";

    /// <summary>Standard channel for diagnostic output. Pre-registered to stderr; gated by app.Debug.IsEnabled at the call site.</summary>
    public const string Debug = "debug";

    public @this(App.@this app, Serializers.@this? serializers = null)
    {
        _app = app;
        Serializers = serializers ?? new Serializers.@this();
        Register(new Channel.@this(Default, Console.OpenStandardOutput(), ChannelDirection.Output, ownsStream: false)
            { ContentType = "text/plain" });
        Register(new Channel.@this(StdOut, Console.OpenStandardOutput(), ChannelDirection.Output, ownsStream: false)
            { ContentType = "text/plain" });
        Register(new Channel.@this(StdErr, Console.OpenStandardError(), ChannelDirection.Output, ownsStream: false)
            { ContentType = "text/plain" });
        // Diagnostic-only sink; default points at stderr so unconfigured installs see output.
        // Users can swap by re-Register-ing "debug" with their own stream / goal-backed channel.
        Register(new Channel.@this(Debug, Console.OpenStandardError(), ChannelDirection.Output, ownsStream: false)
            { ContentType = "text/plain" });
    }

    /// <summary>
    /// Routes a write to the correct actor's channels by actor name.
    /// App.Channels is the router — actor Channels own the actual channels.
    /// </summary>
    public async Task<Data.@this> WriteAsync(string actorName, string channelName, object? data, CancellationToken ct = default)
    {
        var (actor, error) = _app.GetActor(actorName);
        if (error != null) return App.Data.@this.FromError(error);
        return await actor!.Channels.WriteAsync(channelName, data, cancellationToken: ct);
    }

    /// <summary>
    /// Reads a file and deserializes its content to the specified type.
    /// </summary>
    public async Task<T?> ReadAsync<T>(string filePath, CancellationToken cancellationToken = default)
    {
        var fs = _app.FileSystem;
        var content = await fs.File.ReadAllTextAsync(filePath, cancellationToken);
        var ext = fs.Path.GetExtension(filePath);
        return Serializers.Deserialize<T>(new DeserializeOptions { Value = content, Extension = ext });
    }

    /// <summary>
    /// Gets or creates a channel by name.
    /// </summary>
    public Channel.@this GetOrCreate(string name, Func<Channel.@this> factory)
    {
        return _channels.GetOrAdd(name, _ => factory());
    }

    /// <summary>
    /// Gets a channel by name.
    /// </summary>
    public Channel.@this? Get(string name)
    {
        return _channels.TryGetValue(name, out var channel) ? channel : null;
    }

    /// <summary>
    /// Gets a channel and validates existence and permissions.
    /// </summary>
    private (Channel.@this? Channel, Data.@this? Error) GetChannel(string name, bool? requireRead = null, bool? requireWrite = null)
    {
        var channel = Get(name);
        if (channel == null)
            return (null, App.Data.@this.FromError(new ServiceError($"Channel '{name}' not found", "ChannelNotFound", 404)));

        if (requireRead == true && !channel.CanRead)
            return (null, App.Data.@this.FromError(new ServiceError($"Channel '{name}' does not support reading", "ChannelWriteOnly", 400)));

        if (requireWrite == true && !channel.CanWrite)
            return (null, App.Data.@this.FromError(new ServiceError($"Channel '{name}' does not support writing", "ChannelReadOnly", 400)));

        return (channel, null);
    }

    /// <summary>
    /// Registers a channel.
    /// </summary>
    public void Register(Channel.@this channel)
    {
        _channels[channel.Name] = channel;
    }

    /// <summary>
    /// Removes and disposes a channel.
    /// </summary>
    public async Task<bool> RemoveAsync(string name)
    {
        if (!_channels.TryRemove(name, out var channel)) return false;

        await channel.DisposeAsync();
        return true;
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
    /// Writes data to a channel. Navigates the IChannel action for channel name and content.
    /// </summary>
    public async Task<Data.@this> WriteAsync(modules.output.Write action)
    {
        var channel = action.Data?.Properties?.Get<string>("channel") ?? "default";
        var content = action.Data?.Value;

        // Resolve %var% references from the context's Variables
        if (content is string str && str.Contains('%'))
            content = action.Context.Variables.Resolve(str, skipInfrastructure: true);

        return await WriteAsync(channel, content);
    }

    /// <summary>
    /// Writes data to a channel.
    /// </summary>
    public async Task<Data.@this> WriteAsync(string channelName, object? data, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireWrite: true);
        if (error != null) return error;

        try
        {
            await Serializers.SerializeAsync(new SerializeOptions
            {
                Stream = channel!.Stream,
                Data = data,
                ContentType = contentType ?? channel.ContentType ?? "application/json",
                CancellationToken = cancellationToken
            });
            return App.Data.@this.Ok();
        }
        catch (Exception ex)
        {
            return App.Data.@this.FromError(new ServiceError($"Failed to write to channel '{channelName}': {ex.Message}", "WriteError") { Exception = ex });
        }
    }

    /// <summary>
    /// Reads data from a channel.
    /// </summary>
    public async Task<Data.@this> ReadChannelAsync<T>(string channelName, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireRead: true);
        if (error != null) return error;

        try
        {
            var result = await Serializers.DeserializeAsync<T>(new DeserializeOptions
            {
                Stream = channel!.Stream,
                ContentType = channel.ContentType ?? "application/json",
                CancellationToken = cancellationToken
            });
            return App.Data.@this.Ok(result);
        }
        catch (Exception ex)
        {
            return App.Data.@this.FromError(new ServiceError($"Failed to read from channel '{channelName}': {ex.Message}", "ReadError") { Exception = ex });
        }
    }

    /// <summary>
    /// Writes text to a channel.
    /// </summary>
    public async Task<Data.@this> WriteTextAsync(string channelName, string text, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireWrite: true);
        if (error != null) return error;

        try
        {
            await channel!.WriteTextAsync(text, cancellationToken);
            return App.Data.@this.Ok();
        }
        catch (Exception ex)
        {
            return App.Data.@this.FromError(new ServiceError($"Failed to write text to channel '{channelName}': {ex.Message}", "WriteError") { Exception = ex });
        }
    }

    /// <summary>
    /// Reads text from a channel.
    /// </summary>
    public async Task<Data.@this> ReadTextAsync(string channelName, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireRead: true);
        if (error != null) return error;

        try
        {
            var text = await channel!.ReadAllTextAsync(cancellationToken);
            return App.Data.@this.Ok(text);
        }
        catch (Exception ex)
        {
            return App.Data.@this.FromError(new ServiceError($"Failed to read text from channel '{channelName}': {ex.Message}", "ReadError") { Exception = ex });
        }
    }

    /// <summary>
    /// Creates a memory channel.
    /// </summary>
    public Channel.@this CreateMemoryChannel(string name, ChannelDirection direction = ChannelDirection.Bidirectional)
    {
        var channel = Channel.@this.Memory(name, direction);
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
