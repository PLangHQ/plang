using System.Collections.Concurrent;
using App;
using App.Errors;
using App.Variables;
namespace App.Channels;

/// <summary>
/// Per-actor channel registry. Pure registry — Register / Remove / Get / Resolve.
/// Choreography (writes, reads, serializer routing) lives on <see cref="Channel.@this"/>.
///
/// Standard role-channels ("output", "error", "input") are NOT auto-registered here —
/// the entry point (PlangConsole, future PlangWeb) registers them via navigation
/// (Stage 6). App.Run enforces the invariant that every actor that performs I/O has
/// all three before user code runs.
/// </summary>
public sealed class @this : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Channel.@this> _channels = new(StringComparer.OrdinalIgnoreCase);
    private readonly App.@this _app;

    /// <summary>
    /// The serializer registry — content-type routing for I/O.
    /// Stage 6 promotes this to <c>App.Serializers</c> (app-wide, not per-actor).
    /// </summary>
    public Serializers.@this Serializers { get; }

    // Standard channel names. New names (output/error/input) are role-aligned;
    // Default/StdOut/StdErr/StdIn aliases retained as constants for v1 callers
    // until they migrate.
    public const string Output = "output";
    public const string Error = "error";
    public const string Input = "input";
    public const string Debug = "debug";

    [Obsolete("Use Output (role-aligned name).")]
    public const string Default = Output;
    [Obsolete("Use Output (role-aligned name).")]
    public const string StdOut = Output;
    [Obsolete("Use Error (role-aligned name).")]
    public const string StdErr = Error;
    [Obsolete("Use Input (role-aligned name).")]
    public const string StdIn = Input;

    public @this(App.@this app, Serializers.@this? serializers = null)
    {
        _app = app;
        Serializers = serializers ?? new Serializers.@this();
        // Stage 1: ctor no longer opens console streams. Entry point wires (Stage 6).
    }

    /// <summary>
    /// App-level routing helper kept for v1 callers (DefaultHttpProvider etc.).
    /// Stage 4 replaces with direct Channel navigation; for now this resolves the
    /// requested channel on the requested actor and writes through it.
    /// </summary>
    public async Task<Data.@this> WriteAsync(string actorName, string channelName, object? data, CancellationToken ct = default)
    {
        var (actor, error) = _app.GetActor(actorName);
        if (error != null) return App.Data.@this.FromError(error);
        return await actor!.Channels.WriteAsync(channelName, data, cancellationToken: ct);
    }

    /// <summary>
    /// Reads a file and deserializes its content via the serializer registry.
    /// </summary>
    public async Task<T?> ReadAsync<T>(string filePath, CancellationToken cancellationToken = default)
    {
        var fs = _app.FileSystem;
        var content = await fs.File.ReadAllTextAsync(filePath, cancellationToken);
        var ext = fs.Path.GetExtension(filePath);
        return Serializers.Deserialize<T>(new DeserializeOptions { Value = content, Extension = ext });
    }

    /// <summary>
    /// Resolves a channel name to a concrete channel.
    /// - null / "" → the Output role channel (or null if no actor has wired one).
    /// - known name → that channel.
    /// - unknown name → null (caller surfaces a typed ChannelNotFound).
    /// </summary>
    public Channel.@this? Resolve(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return GetByRole(Channel.Role.@this.Output)
                ?? Get(Output)
                ?? Get("default");
        return Get(name);
    }

    /// <summary>Returns the channel registered for the given role, or null.</summary>
    public Channel.@this? GetByRole(Channel.Role.@this role)
    {
        foreach (var ch in _channels.Values)
            if (ch.Role == role) return ch;
        return null;
    }

    public Channel.@this GetOrCreate(string name, Func<Channel.@this> factory)
        => _channels.GetOrAdd(name, _ => factory());

    public Channel.@this? Get(string name)
        => _channels.TryGetValue(name, out var channel) ? channel : null;

    public void Register(Channel.@this channel)
        => _channels[channel.Name] = channel;

    public async Task<bool> RemoveAsync(string name)
    {
        if (!_channels.TryRemove(name, out var channel)) return false;
        await channel.DisposeAsync();
        return true;
    }

    public bool Contains(string name) => _channels.ContainsKey(name);

    public IEnumerable<string> ChannelNames => _channels.Keys;

    /// <summary>All registered channels.</summary>
    public IEnumerable<Channel.@this> All => _channels.Values;

    /// <summary>
    /// Shallow snapshot — new registry, same channel instances. Mutations to either
    /// side after the call do not affect the other. Used to capture the foundational
    /// set for goal-channel recursion isolation.
    /// </summary>
    public @this Snapshot()
    {
        var copy = new @this(_app, Serializers);
        foreach (var ch in _channels.Values)
            copy.Register(ch);
        return copy;
    }

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
    /// Convenience write — resolves the channel and writes via its serializer.
    /// Stage 4 moves this responsibility entirely onto the resolved Channel; this
    /// overload remains for v1 callers (DefaultHttpProvider, file/save fall-back).
    /// </summary>
    public async Task<Data.@this> WriteAsync(string channelName, object? data, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireWrite: true);
        if (error != null) return error;

        var envelope = data is Data.@this d ? d : Data.@this.Ok(data);
        // Mime override on a per-call basis — temporarily set on the channel for routing.
        // Stage 4 cleans this up by passing options through to WriteCore directly.
        if (!string.IsNullOrEmpty(contentType) && channel is Channel.Stream.@this sc)
        {
            try
            {
                await sc.Serializers.SerializeAsync(new SerializeOptions
                {
                    Stream = sc.Stream,
                    Data = envelope.Value,
                    ContentType = contentType,
                    CancellationToken = cancellationToken
                });
                return App.Data.@this.Ok();
            }
            catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
            {
                return App.Data.@this.FromError(new ServiceError($"Failed to write to channel '{channelName}': {ex.Message}", "WriteError") { Exception = ex });
            }
        }

        return await channel!.WriteAsync(envelope, cancellationToken);
    }

    /// <summary>Reads typed data from a channel.</summary>
    public async Task<Data.@this> ReadChannelAsync<T>(string channelName, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireRead: true);
        if (error != null) return error;

        if (channel is Channel.Stream.@this sc)
        {
            try
            {
                var result = await sc.Serializers.DeserializeAsync<T>(new DeserializeOptions
                {
                    Stream = sc.Stream,
                    ContentType = sc.Mime,
                    CancellationToken = cancellationToken
                });
                return App.Data.@this.Ok(result);
            }
            catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
            {
                return App.Data.@this.FromError(new ServiceError($"Failed to read from channel '{channelName}': {ex.Message}", "ReadError") { Exception = ex });
            }
        }

        return await channel!.ReadAsync(cancellationToken);
    }

    /// <summary>Convenience text write.</summary>
    public async Task<Data.@this> WriteTextAsync(string channelName, string text, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireWrite: true);
        if (error != null) return error;

        try
        {
            if (channel is Channel.Stream.@this sc)
                await sc.WriteTextAsync(text, cancellationToken);
            else
                await channel!.WriteAsync(App.Data.@this.Ok(text), cancellationToken);
            return App.Data.@this.Ok();
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            return App.Data.@this.FromError(new ServiceError($"Failed to write text to channel '{channelName}': {ex.Message}", "WriteError") { Exception = ex });
        }
    }

    /// <summary>Convenience text read.</summary>
    public async Task<Data.@this> ReadTextAsync(string channelName, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireRead: true);
        if (error != null) return error;

        try
        {
            if (channel is Channel.Stream.@this sc)
            {
                var text = await sc.ReadAllTextAsync(cancellationToken);
                return App.Data.@this.Ok(text);
            }
            var read = await channel!.ReadAsync(cancellationToken);
            return read;
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            return App.Data.@this.FromError(new ServiceError($"Failed to read text from channel '{channelName}': {ex.Message}", "ReadError") { Exception = ex });
        }
    }

    /// <summary>Creates and registers an in-memory channel. Convenience for tests.</summary>
    public Channel.@this CreateMemoryChannel(string name, ChannelDirection direction = ChannelDirection.Bidirectional)
    {
        var channel = Channel.Stream.@this.Memory(name, direction);
        Register(channel);
        return channel;
    }

    /// <summary>
    /// Stage 4 entry point: write via an action's resolved Channel slot.
    /// Until source-gen emits the Channel slot directly, Write.Run routes here.
    /// </summary>
    public async Task<Data.@this> WriteAsync(modules.output.Write action)
    {
        var channelName = action.Data?.Properties?.Get<string>("channel");
        var channel = Resolve(channelName);
        if (channel == null)
            return App.Data.@this.FromError(new ServiceError(
                $"Channel '{channelName ?? "<output>"}' not found", "ChannelNotFound", 404));

        var content = action.Data?.Value;
        if (content is string str && str.Contains('%'))
            content = action.Context.Variables.Resolve(str, skipInfrastructure: true);

        var envelope = content is Data.@this d ? d : Data.@this.Ok(content);
        return await channel.WriteAsync(envelope);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var channel in _channels.Values)
            await channel.DisposeAsync();
        _channels.Clear();
    }
}
