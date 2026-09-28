using System.Collections.Concurrent;
using app;
using app.error;
namespace app.channel.list;

/// <summary>
/// Per-actor channel registry. Pure registry — Register / Remove, and selection: <c>this[name]</c> (a miss
/// throws — the defaults, which <see cref="Verify"/> guarantees) and <see cref="Get"/> (a miss is null — a
/// user-named channel). Choreography (writes, reads, serializer routing) lives on <see cref="channel.@this"/>.
///
/// Standard role-channels ("output", "error", "input") are NOT auto-registered here —
/// the entry point (PlangConsole, future PlangWeb) registers them via navigation
/// (Stage 6). App.Run enforces the invariant that every actor that performs I/O has
/// all three before user code runs.
/// </summary>
public sealed class @this : global::app.type.item.@this, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, channel.@this> _channels = new(StringComparer.OrdinalIgnoreCase);
    private readonly app.@this _app;

    /// <summary>
    /// The owning App. Child channels navigate via <c>Channel.Channels.App</c>
    /// to reach app-level surfaces without going through Actor — Service-owned
    /// Channels have no Actor.
    /// </summary>
    public app.@this App => _app;

    /// <summary>
    /// The actor this collection belongs to, born with it. <see cref="Register"/> stamps it onto each
    /// registered channel, whose events fire in its context. Null for Service-owned Channels (Service is not
    /// an Actor).
    /// </summary>
    internal global::app.actor.@this? Actor { get; }

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

    /// <summary>The channels of <paramref name="actor"/> — or of a Service, which is no actor (null).</summary>
    public @this(app.@this app, global::app.actor.@this? actor)
    {
        _app = app;
        Actor = actor;
    }

    /// <summary>
    /// Boot invariant: every name in <see cref="Defaults"/> must be registered.
    /// Returns Ok or a Data error with key <c>MissingRequiredChannelAtBoot</c>.
    /// Replaces the role-channel enforcement that lived in App.EnsureRoleChannels.
    /// </summary>
    public data.@this Verify()
    {
        // the result is born in the owning actor's context, or the system's for a Service's channels
        var context = Actor?.Context ?? _app.System.Context;
        foreach (var name in Defaults)
        {
            if (!_channels.ContainsKey(name))
                return context.Error(new ServiceError(
                    $"Channel '{name}' not registered. Default channels ({string.Join(", ", Defaults)}) must be wired before goals run.",
                    "MissingRequiredChannelAtBoot", 500));
        }
        return context.Ok();
    }

    /// <summary>
    /// The channel registered under <paramref name="name"/> that takes writes now (<see cref="channel.@this.Available"/>),
    /// or null — a user-named channel's miss is the caller's result to make. Sibling and late-registered
    /// channels stay visible.
    /// </summary>
    public channel.@this? Get(string name)
        => _channels.TryGetValue(name, out var channel) && channel.Available ? channel : null;

    public void Register(channel.@this channel)
    {
        channel.Channels = this;
        if (channel.Actor == null) channel.Actor = Actor!;
        _channels[channel.Name] = channel;
    }

    public async Task<bool> RemoveAsync(string name)
    {
        if (!_channels.TryRemove(name, out var channel)) return false;
        await channel.DisposeAsync();
        return true;
    }

    public bool Contains(string name) => _channels.ContainsKey(name);

    // --- Stage 3 accessor surface ---

    /// <summary>Index by name. Throws on miss — index-miss is a hard error.</summary>
    public channel.@this this[string name]
        => Get(name) ?? throw new KeyNotFoundException($"No channel named '{name}'.");

    /// <summary>The open channel of the C# class <paramref name="clrType"/>, whatever name it is under;
    /// null when there is none. What a channel IS, beside where it writes (its name).</summary>
    public channel.@this? this[System.Type clrType]
        => _channels.Values.FirstOrDefault(c => c.IsOpen && clrType.IsInstanceOfType(c));

    /// <summary>Enumerate registered channels.</summary>
    public IEnumerable<channel.@this> list => _channels.Values;

    public IEnumerable<string> ChannelNames => _channels.Keys;

    /// <summary>One step down: the list's own members first, then a channel by name —
    /// <c>%!app.actor.user.channel.output%</c> is the channel a program binds its events on.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
    {
        var member = await base.Get(parent, key);
        if (member.IsInitialized) return member;
        return Get(key) is { } channel ? new global::app.data.@this(key, channel, parent: parent) : member;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var channel in _channels.Values)
            await channel.DisposeAsync();
        _channels.Clear();
    }
}
