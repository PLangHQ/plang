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
/// the entry point (PlangConsole) registers them via navigation.
/// App.Run enforces the invariant that every actor that performs I/O has
/// all three before user code runs.
/// </summary>
public sealed class @this : global::app.type.item.@this, IAsyncDisposable
{
    // The channels by name, made on the list's first use — with the console's standard ones when the list was born
    // standard (an actor that never writes makes none).
    private ConcurrentDictionary<string, channel.@this>? _table;
    private readonly object _gate = new();
    // whether the list's first rows are the console's standard channels: the app's choice, made at its birth
    private readonly bool _standard;
    private readonly app.@this _app;

    // The channels by name, as they are now: made, wired, on the first use — the one door every other reads through.
    private ConcurrentDictionary<string, channel.@this> Table
    {
        get
        {
            if (System.Threading.Volatile.Read(ref _table) is { } table) return table;
            lock (_gate)
            {
                if (_table is { } made) return made;
                var channels = new ConcurrentDictionary<string, channel.@this>(StringComparer.OrdinalIgnoreCase);
                if (_standard) Wire(channels, interactiveInput: false);
                System.Threading.Volatile.Write(ref _table, channels);
                return channels;
            }
        }
    }

    /// <summary>Wires the console's standard channels now, under the names not yet registered: output and error to the
    /// process's streams; input to real stdin when <paramref name="interactiveInput"/> (the one terminal owner, the CLI),
    /// else an empty stream that reads as EOF, so a prompt nothing answers fails fast instead of blocking.</summary>
    internal void Wire(bool interactiveInput) => Wire(Table, interactiveInput);

    // The console's standard channels into <paramref name="channels"/>, under the names not yet there. The console
    // writes and reads in the channel setting's format (%!app.channel.setting.format%): text for a person at a terminal,
    // plang's own for a program that runs this plang (--channel.setting.format=plang).
    private void Wire(ConcurrentDictionary<string, channel.@this> channels, bool interactiveInput)
    {
        if (!channels.ContainsKey(Output))
            channels.TryAdd(Output, Adopted(new type.console.@this(Output, Console.OpenStandardOutput(), ChannelDirection.Output)));
        if (!channels.ContainsKey(Error))
            channels.TryAdd(Error, Adopted(new type.console.@this(Error, Console.OpenStandardError(), ChannelDirection.Output)));
        if (!channels.ContainsKey(Input))
            channels.TryAdd(Input, Adopted(interactiveInput
                ? new type.console.@this(Input, Console.OpenStandardInput(), ChannelDirection.Input)
                : type.stream.@this.Memory(Input, ChannelDirection.Input)));
    }

    // A channel adopted, answered.
    private channel.@this Adopted(channel.@this channel)
    {
        Adopt(channel);
        return channel;
    }

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
    public @this(app.@this app, global::app.actor.@this? actor) : this(app, actor, standard: false) { }

    /// <summary>The channels of <paramref name="actor"/>, whose first rows, when <paramref name="standard"/>, are the
    /// console's standard channels, wired at the list's first use with an input that reads as EOF (no shared stdin).</summary>
    internal @this(app.@this app, global::app.actor.@this? actor, bool standard) : base(app.type.list[typeof(@this)])
    {
        _app = app;
        Actor = actor;
        _standard = standard;
    }

    /// <summary>
    /// Boot invariant: every name in <see cref="Defaults"/> must be registered.
    /// Returns Ok or a Data error with key <c>MissingRequiredChannelAtBoot</c>.
    /// Replaces the role-channel enforcement that lived in App.EnsureRoleChannels.
    /// </summary>
    public data.@this Verify()
    {
        // the result is born in the owning actor's context, or the system's for a Service's channels
        var context = Actor?.Context ?? _app.actor.list.System.Context;
        foreach (var name in Defaults)
        {
            if (!Table.ContainsKey(name))
                return context.Error(new ServiceError(
                    $"Channel '{name}' not registered. Default channels ({string.Join(", ", Defaults)}) must be wired before goals run.",
                    "MissingRequiredChannelAtBoot", 500));
        }
        return context.Ok();
    }

    /// <summary>
    /// A failure nothing waits for (a goal called without waiting, a callback run outside the step loop): shown on these
    /// channels' <c>error</c> — the actor's own, where it shows its errors. When that channel can't take it (its own
    /// write failed — a goal channel whose goal fails too), both go to the system actor's <c>error</c> channel: an error is
    /// never lost. The end of the line, so it answers nothing.
    /// </summary>
    public async Task Report(data.@this failed)
    {
        var shown = Get(Error) is { } error ? await error.WriteAsync(failed) : null;
        if (shown is { Success: true }) return;
        // the last line: the system's error channel — these are the system's, there is no further one
        var system = _app.actor.list.System.Channel;
        if (ReferenceEquals(system, this) || system.Get(Error) is not { } last) return;
        await last.WriteAsync(failed);
        if (shown != null) await last.WriteAsync(shown);
    }

    /// <summary>
    /// The channel registered under <paramref name="name"/> that takes writes now (<see cref="channel.@this.Available"/>),
    /// or null — a user-named channel's miss is the caller's result to make. Sibling and late-registered
    /// channels stay visible.
    /// </summary>
    public channel.@this? Get(string name)
        => Table.TryGetValue(name, out var channel) && channel.Available ? channel : null;

    public void Register(channel.@this channel)
    {
        Adopt(channel);
        Table[channel.Name] = channel;
    }

    /// <summary>Registers <paramref name="channel"/> in place of the one by its name, which is disposed after — one
    /// write, so the name always resolves (a default channel is replaced this way, never left out).</summary>
    public async Task Set(channel.@this channel)
    {
        Adopt(channel);
        channel.@this? old = null;
        Table.AddOrUpdate(channel.Name, channel, (_, previous) => { old = previous; return channel; });
        if (old != null && !ReferenceEquals(old, channel)) await old.DisposeAsync();
    }

    // A channel registered here belongs to this list and, unless it names another, to its actor.
    private void Adopt(channel.@this channel)
    {
        channel.Channels = this;
        if (channel.Actor == null) channel.Actor = Actor!;
    }

    /// <summary>Removes the channel named <paramref name="name"/>. A default channel can't be — the boot invariant
    /// needs it; its backing is replaced with <see cref="Set"/> — and one not registered is NotFound.</summary>
    public async Task<data.@this> Remove(string name, global::app.actor.context.@this context)
    {
        if (Defaults.Any(d => string.Equals(d, name, StringComparison.OrdinalIgnoreCase)))
            return context.Error(new ServiceError(
                $"Channel '{name}' is a default channel and cannot be removed (use channel.set to replace its backing).",
                "ChannelInvariantViolation", 400));
        if (!Table.TryRemove(name, out var channel))
            return context.Error(new ServiceError($"Channel '{name}' not found", "ChannelNotFound", 404));
        await channel.DisposeAsync();
        return context.Ok();
    }

    public bool Contains(string name) => Table.ContainsKey(name);

    /// <summary>Index by name. Throws on miss — index-miss is a hard error.</summary>
    public channel.@this this[string name]
        => Get(name) ?? throw new KeyNotFoundException($"No channel named '{name}'.");

    /// <summary>The open channel of the C# class <paramref name="clrType"/>, whatever name it is under;
    /// null when there is none. What a channel IS, beside where it writes (its name).</summary>
    public channel.@this? this[System.Type clrType]
        => Table.Values.FirstOrDefault(c => c.IsOpen && clrType.IsInstanceOfType(c));

    /// <summary>Enumerate registered channels.</summary>
    public IEnumerable<channel.@this> list => Table.Values;

    public IEnumerable<string> ChannelNames => Table.Keys;

    /// <summary>One step down: the list's own members first, then a channel by name —
    /// <c>%!app.actor["user"].channel.output%</c> is the channel a program binds its events on.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
    {
        var member = await base.Get(parent, key);
        if (member.IsInitialized) return member;
        return Get(key) is { } channel ? new global::app.data.@this(key, channel, parent: parent) : member;
    }

    public async ValueTask DisposeAsync()
    {
        // a list never used made no channel, so it has none to dispose
        if (System.Threading.Volatile.Read(ref _table) is not { } channels) return;
        foreach (var channel in channels.Values)
            await channel.DisposeAsync();
        channels.Clear();
    }
}
