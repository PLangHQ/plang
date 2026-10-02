using app.error;
using Verb = global::app.type.item.permission.Verb;

namespace app.channel.type.file;

/// <summary>
/// A file as a channel. A value written to it is opened at the last moment, encoded in the format the file's
/// extension names (<c>.pr</c> → goal's, <c>.json</c> → json, <c>.txt</c> → text), and lands in one write, so
/// a failed encode leaves the file as it was. The file's format writes the value, or asks its type to be born
/// from it; when nothing can, the write fails with the format's reason. Write-only.
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
        Mime = path.MimeType(context);
    }

    public override async Task<global::app.data.@this> Write(global::app.data.@this data, CancellationToken ct = default)
    {
        var context = data.Context;
        var allowed = await Path.Authorize(Verb.write, context);
        if (!allowed.Success || allowed.Exits) return allowed;
        try
        {
            // Open question (with Ingi): a binary value lands as its raw bytes whatever the extension says, or
            // the extension's format writes it (json → base64). Today: raw bytes.
            if (data.Type.Is("binary") && await data.Value() is global::app.type.item.binary.@this bytes)
                return await Path.WriteBytes(bytes.Value, context);

            var opened = await data.Peek().Open(context);
            if (opened != null) return context.Error(opened);
            using var encoded = new MemoryStream();
            var result = await Path.Kind(context).kind.Encode(encoded, data, context, ct: ct);
            if (!result.Success) return result;
            return await Path.WriteBytes(encoded.ToArray(), context);
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
            $"Channel '{Name}' does not support reading", "ChannelWriteOnly", 400)));

    public override Task<global::app.data.@this> Ask(module.output.ask action, CancellationToken ct = default)
        => Task.FromResult(action.Context.Error(new ServiceError(
            $"Channel '{Name}' does not support asking", "ChannelWriteOnly", 400)));
}
