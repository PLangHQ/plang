using app.error;

namespace app.channel.type.file;

/// <summary>
/// A file as a channel. A value written to it is opened at the last moment, encoded in the format the file's
/// extension names (<c>.pr</c> → goal's, <c>.json</c> → json, <c>.txt</c> → text), and streams into the file as it
/// is written, never held whole; it lands only when the write succeeds, so a failed encode leaves the file as it
/// was. The value says what it writes (a file of the same format pours its bytes); when nothing can, the write
/// fails with the format's reason. Write-only.
/// </summary>
public sealed class @this : global::app.channel.@this
{
    /// <summary>The file written — it owns the gate and the bytes on disk.</summary>
    [global::app.Debug] public global::app.type.item.path.file.@this Path { get; }

    public @this(global::app.type.item.path.file.@this path, global::app.actor.context.@this context)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Name = path.ToString();
        Direction = ChannelDirection.Output;
        Mime = path.Mime(context).ToString();
    }

    public override async Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
    {
        var context = data.Context;
        try
        {
            // what was written, as it reads now: a reference the Data it names, unread — so a file not yet read
            // can pour itself — a template rendered
            var value = await data.Settle();
            if (!value.Success) return value;
            var format = Path.Kind(context).kind;
            var written = await Path.Write(into => value.Peek().Save(value, into, format, context), context);
            return written.Success && !written.Exits ? context.Ok<global::app.type.item.path.@this>(Path) : written;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return context.Error(new ServiceError(ex.Message, "IOError", 500));
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            return context.Error(new ServiceError(ex.Message, "SerializationError", 500));
        }
    }

    public override Task<global::app.data.@this> Read(CancellationToken ct = default)
        => Task.FromResult(global::app.data.@this.FromError(new ServiceError(
            $"Channel '{Name}' does not support reading", "ChannelWriteOnly", 400), Context!));

    public override Task<global::app.data.@this> Ask(module.output.ask action, CancellationToken ct = default)
        => Task.FromResult(action.Context.Error(new ServiceError(
            $"Channel '{Name}' does not support asking", "ChannelWriteOnly", 400)));
}
