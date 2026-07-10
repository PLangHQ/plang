using app.error;

namespace app.channel.type.file;

/// <summary>
/// Filesystem-backed channel kind. A file is not a peer of channel — it <em>is</em>
/// a channel: <c>channel.read</c> on it stamps <c>{type, kind}</c> from the file's
/// <see cref="global::app.channel.@this.Mime"/> (derived from the extension) and
/// produces lazy Data, exactly like every other channel kind.
///
/// <para>The channel does no <c>System.IO</c> of its own — it reads bytes through
/// <see cref="global::app.type.item.path.@this.ReadBytes"/>, which carries the
/// <c>AuthGate</c> (the actor permission model). So the file boundary inherits the
/// same gate the rest of the path surface enforces; PLNG002 stays clean.</para>
/// </summary>
public sealed class @this : global::app.channel.@this
{
    private readonly global::app.type.item.path.@this _path;

    public @this(global::app.type.item.path.@this path)
    {
        _path = path;
        Name = path.Raw;
        Direction = ChannelDirection.Input;
        // Mime from the file extension — the file path already derives it through
        // the format registry, the same map the boundary stamps from.
        Mime = path.MimeType;

        // Reach Format + actor context through the path's own context so the
        // base boundary can stamp without the channel being registered.
        var ctx = path.Context;
        if (ctx != null)
        {
            Actor = ctx.Actor;
            Channels = ctx.Actor.Channel;
        }
    }

    public override bool CanWrite => false;

    public override async Task<global::app.data.@this> Read(CancellationToken ct = default)
    {
        // ReadBytes carries the AuthGate AND surfaces missing-file / IO failures
        // as an error Data (it owns the System.IO), so the channel stays clean.
        var bytes = await _path.ReadBytes();
        if (!bytes.Success) return bytes;
        return await StampReadAsync((await bytes.Value())!.Value, ct);
    }

    /// <summary>
    /// Stamp + parse content the caller already holds — the file value samples
    /// its bytes ONCE (through its own gate) and hands them here so the
    /// boundary's mime stamping applies without a second disk read.
    /// </summary>
    public Task<global::app.data.@this> Read(byte[] raw, CancellationToken ct = default)
        => StampReadAsync(raw, ct);

    public override Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
        => Task.FromResult(data.Context.Error(new ServiceError(
            $"File channel '{Name}' is read-only; write through path.WriteText/WriteBytes", "ChannelReadOnly", 400)));

    public override Task<global::app.data.@this> Ask(module.output.ask action, CancellationToken ct = default)
        => Task.FromResult(action.Context.Error(new ServiceError(
            $"File channel '{Name}' does not support ask", "ChannelNoAsk", 400)));
}
