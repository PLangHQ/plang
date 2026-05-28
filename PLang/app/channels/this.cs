using System.Collections.Concurrent;
using app;
using app.errors;
using app.variables;
namespace app.channels;

/// <summary>
/// Per-actor channel registry. Pure registry — Register / Remove / Get / Resolve.
/// Choreography (writes, reads, serializer routing) lives on <see cref="channel.@this"/>.
///
/// Standard role-channels ("output", "error", "input") are NOT auto-registered here —
/// the entry point (PlangConsole, future PlangWeb) registers them via navigation
/// (Stage 6). App.Run enforces the invariant that every actor that performs I/O has
/// all three before user code runs.
/// </summary>
public sealed class @this : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, channel.@this> _channels = new(StringComparer.OrdinalIgnoreCase);
    private readonly app.@this _app;

    /// <summary>
    /// The owning App. Child channels navigate via <c>Channel.Channels.App</c>
    /// to reach app-level surfaces (e.g. <c>app.Events</c> bindings) without
    /// going through Actor — Service-owned Channels have no Actor.
    /// </summary>
    public app.@this App => _app;

    /// <summary>
    /// The actor this collection belongs to. Set by <see cref="Actor.@this"/> right
    /// after construction. <see cref="Register"/> stamps it onto each registered
    /// channel so channel-event firing can read from <c>Actor.Context.Events</c>.
    /// Null for Service-owned Channels (Service is not an Actor).
    /// </summary>
    internal global::app.actor.@this? Actor { get; set; }

    /// <summary>
    /// The serializer registry — content-type routing for I/O. Per-actor:
    /// each actor's Channels owns its own registry. Boot-time defaults register
    /// identically per actor; runtime extensions apply to the registering actor.
    /// </summary>
    public Serializers Serializers { get; }

    public const string Output = "output";
    public const string Error = "error";
    public const string Input = "input";
    public const string Debug = "debug";

    /// <summary>
    /// The three pre-registered channel names. Removal is refused for these;
    /// boot enforces all three are present via <see cref="Verify"/>. Just names —
    /// no separate "role channel" type. <c>write</c> with no channel argument
    /// resolves to the channel named <c>"output"</c>; error writes to
    /// <c>"error"</c>; reads from <c>"input"</c>.
    /// </summary>
    public static readonly string[] Defaults = [Output, Error, Input];

    public @this(app.@this app, Serializers? serializers = null)
    {
        _app = app;
        Serializers = serializers ?? new Serializers();
        // Stage 1: ctor no longer opens console streams. Entry point wires (Stage 6).
    }

    /// <summary>
    /// Resolves a channel by name. Empty/null falls back to the channel named
    /// <c>"output"</c>. Returns null when nothing is registered under the requested
    /// name — caller decides how to surface that (see e.g. source-generator-emitted
    /// IChannel resolution which returns a <c>ChannelNotFound</c> Data error).
    /// </summary>
    public channel.@this? Resolve(string? name)
        => string.IsNullOrEmpty(name) ? Get(Output) : Get(name);

    /// <summary>
    /// Boot invariant: every name in <see cref="Defaults"/> must be registered.
    /// Returns Ok or a Data error with key <c>MissingRequiredChannelAtBoot</c>.
    /// Replaces the role-channel enforcement that lived in App.EnsureRoleChannels.
    /// </summary>
    public data.@this Verify()
    {
        foreach (var name in Defaults)
        {
            if (!_channels.ContainsKey(name))
                return global::app.data.@this.FromError(new ServiceError(
                    $"Channel '{name}' not registered. Default channels ({string.Join(", ", Defaults)}) must be wired before goals run.",
                    "MissingRequiredChannelAtBoot", 500));
        }
        return global::app.data.@this.Ok();
    }

    public channel.@this GetOrCreate(string name, Func<channel.@this> factory)
        => _channels.GetOrAdd(name, _ => factory());

    /// <summary>
    /// Resolves a registered channel by name. Treats a goal-channel that is
    /// currently executing on this async context as not-found, so a goal body
    /// that writes to its own name can't loop back into itself. Sibling and
    /// late-registered channels stay visible.
    /// </summary>
    public channel.@this? Get(string name)
    {
        if (!_channels.TryGetValue(name, out var channel)) return null;
        if (channel is channel.goal.@this g && g.IsExecuting) return null;
        return channel;
    }

    /// <summary>
    /// Named-channel lookup with no-op fallback. Returns the registered channel
    /// when one exists under <paramref name="name"/>; otherwise a process-wide
    /// no-op sink that accepts writes silently. Use this when the caller wants
    /// to write opportunistically without null-checking — e.g.
    /// <c>IClass.Build()</c> writing a <c>builder.warning.@this</c> to
    /// <c>"builder"</c> regardless of whether a build is currently active.
    ///
    /// <para>
    /// Distinct from <see cref="Resolve"/>, which returns null on miss and
    /// expects the caller to handle that (used by stream-write paths that
    /// surface <c>ChannelNotFound</c>). <see cref="Channel"/> never returns
    /// null.
    /// </para>
    /// </summary>
    public channel.@this Channel(string name)
        => _channels.TryGetValue(name, out var channel) ? channel : NoOp;

    private static readonly channel.noop.@this NoOp = new("__noop__");

    public void Register(channel.@this channel)
    {
        channel.Channels = this;
        if (channel.Actor == null) channel.Actor = Actor;
        _channels[channel.Name] = channel;
    }

    public async Task<bool> RemoveAsync(string name)
    {
        if (!_channels.TryRemove(name, out var channel)) return false;
        await channel.DisposeAsync();
        return true;
    }

    public bool Contains(string name) => _channels.ContainsKey(name);

    public IEnumerable<string> ChannelNames => _channels.Keys;

    /// <summary>All registered channels.</summary>
    public IEnumerable<channel.@this> All => _channels.Values;

    private (channel.@this? Channel, data.@this? Error) GetChannel(string name, bool? requireRead = null, bool? requireWrite = null)
    {
        var channel = Get(name);
        if (channel == null)
            return (null, global::app.data.@this.FromError(new ServiceError($"Channel '{name}' not found", "ChannelNotFound", 404)));

        if (requireRead == true && !channel.CanRead)
            return (null, global::app.data.@this.FromError(new ServiceError($"Channel '{name}' does not support reading", "ChannelWriteOnly", 400)));

        if (requireWrite == true && !channel.CanWrite)
            return (null, global::app.data.@this.FromError(new ServiceError($"Channel '{name}' does not support writing", "ChannelReadOnly", 400)));

        return (channel, null);
    }

    /// <summary>
    /// Convenience write — resolves the channel by name, wraps the data in
    /// a Data if needed, and delegates to the channel's own WriteAsync (which
    /// fires events and routes through Write + the per-actor Serializers).
    /// </summary>
    public async Task<data.@this> WriteAsync(string channelName, object? data, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireWrite: true);
        if (error != null) return error;

        var wrapped = data is global::app.data.@this d ? d : global::app.data.@this.Ok(data);
        return await channel!.WriteAsync(wrapped, cancellationToken);
    }

    /// <summary>Reads typed data from a channel.</summary>
    public async Task<data.@this> ReadChannelAsync<T>(string channelName, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireRead: true);
        if (error != null) return error;

        if (channel is channel.stream.@this sc)
        {
            // Serializer returns Data<T> already with its own Success/Error —
            // forward as-is; no extra try/catch needed because parse failures
            // travel through Data.Error now instead of throwing.
            return await Serializers.DeserializeAsync<T>(new DeserializeOptions
            {
                Stream = sc.Stream,
                Type = sc.Mime,
                CancellationToken = cancellationToken
            });
        }

        return await channel!.ReadAsync(cancellationToken);
    }

    /// <summary>Convenience text write.</summary>
    public async Task<data.@this> WriteTextAsync(string channelName, string text, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireWrite: true);
        if (error != null) return error;

        try
        {
            if (channel is channel.stream.@this sc)
                await sc.WriteTextAsync(text, cancellationToken);
            else
                await channel!.WriteAsync(global::app.data.@this.Ok(text), cancellationToken);
            return global::app.data.@this.Ok();
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            return global::app.data.@this.FromError(new ServiceError($"Failed to write text to channel '{channelName}': {ex.Message}", "WriteError") { Exception = ex });
        }
    }

    /// <summary>Convenience text read.</summary>
    public async Task<data.@this> ReadTextAsync(string channelName, CancellationToken cancellationToken = default)
    {
        var (channel, error) = GetChannel(channelName, requireRead: true);
        if (error != null) return error;

        try
        {
            if (channel is channel.stream.@this sc)
            {
                var text = await sc.ReadAllTextAsync(cancellationToken);
                return global::app.data.@this.Ok(text);
            }
            var read = await channel!.ReadAsync(cancellationToken);
            return read;
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            return global::app.data.@this.FromError(new ServiceError($"Failed to read text from channel '{channelName}': {ex.Message}", "ReadError") { Exception = ex });
        }
    }

    /// <summary>Creates and registers an in-memory channel. Convenience for tests.</summary>
    public channel.@this CreateMemoryChannel(string name, ChannelDirection direction = ChannelDirection.Bidirectional)
    {
        var ch = channel.stream.@this.Memory(name, direction);
        Register(ch);
        return ch;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var channel in _channels.Values)
            await channel.DisposeAsync();
        _channels.Clear();
    }
}
