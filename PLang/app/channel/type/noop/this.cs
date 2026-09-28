namespace app.channel.type.noop;

/// <summary>
/// The sentinel <see cref="app.channel.list.@this.Channel"/> returns when no channel
/// is registered under the requested name. Every operation surfaces a
/// <c>ChannelNotFound</c> error — addressing a channel that does not exist is a bug at
/// the call site, not a silent sink. <see cref="app.channel.list.@this.Channel"/> never
/// returns null so callers don't null-check; they get a typed error Data instead.
///
/// <para>Born with the channel-list's context so <see cref="Read"/> (which has no
/// incoming Data/action to borrow one from) can still produce a context-ful error.</para>
/// </summary>
public sealed class @this : global::app.channel.@this
{
    private readonly global::app.actor.context.@this _context;

    public @this(string name, global::app.actor.context.@this context)
    {
        Name = name;
        Direction = ChannelDirection.Bidirectional;
        _context = context;
    }

    private global::app.data.@this Missing()
        => _context.Error(new global::app.error.ServiceError(
            $"Channel '{Name}' not found", "ChannelNotFound", 404));

    public override System.Threading.Tasks.Task<global::app.data.@this> Write(
        global::app.data.@this data, System.Threading.CancellationToken ct = default)
        => System.Threading.Tasks.Task.FromResult(Missing());

    public override System.Threading.Tasks.Task<global::app.data.@this> Read(
        System.Threading.CancellationToken ct = default)
        => System.Threading.Tasks.Task.FromResult(Missing());

    public override System.Threading.Tasks.Task<global::app.data.@this> Ask(
        global::app.module.output.ask action, System.Threading.CancellationToken ct = default)
        => System.Threading.Tasks.Task.FromResult(Missing());
}
