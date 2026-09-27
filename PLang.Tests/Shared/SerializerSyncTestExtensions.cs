using System.IO;
using System.Text;

namespace PLang.Tests.Shared;

/// <summary>
/// Sync convenience over a format's (async, stream) doors — TESTS ONLY. A format is a kind: it writes a Data
/// (<c>Encode</c>) and reads one back (<c>Decode</c>), with the caller's context. Tests want a quick
/// round-trip string, so these give the sync string names over the async stream API (sync-over-async is fine
/// in a test harness). Out = transport, Store = persistence.
/// </summary>
public static class SerializerSyncTestExtensions
{
    /// <summary>The format a MIME names, as the asker sees it — plang's own for <c>application/plang</c>,
    /// item's json for <c>application/json</c>, text's for <c>text/plain</c>.</summary>
    public static global::app.type.kind.@this Format(this global::app.actor.context.@this ctx, string mime)
        => ctx.App.type.list.Mime(mime, ctx).kind;

    public static global::app.data.@this<global::app.type.item.text.@this> Serialize(this global::app.type.kind.@this format,
        global::app.data.@this d, global::app.actor.context.@this ctx)
        => SerializeTo(format, d, ctx, global::app.View.Out);

    public static global::app.data.@this<global::app.type.item.text.@this> Store(this global::app.type.kind.@this format,
        global::app.data.@this d, global::app.actor.context.@this ctx)
        => SerializeTo(format, d, ctx, global::app.View.Store);

    public static global::app.data.@this Deserialize(this global::app.type.kind.@this format, string str, global::app.actor.context.@this ctx)
        => DeserializeFrom(format, str, ctx, global::app.View.Out);

    public static global::app.data.@this<T> Deserialize<T>(this global::app.type.kind.@this format, string str, global::app.actor.context.@this ctx)
        where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        if (string.IsNullOrEmpty(str)) return global::app.data.@this<T>.Ok(default!);
        var read = DeserializeFrom(format, str, ctx, global::app.View.Out);
        return read.Success ? read.As<T>() : global::app.data.@this<T>.From(read);
    }

    /// <summary>A bare item written in goal's format — a <c>.pr</c>'s text (Store face by default).</summary>
    public static async System.Threading.Tasks.Task<string> Pr(this global::app.actor.context.@this ctx,
        global::app.type.item.@this item, global::app.View view = global::app.View.Store)
    {
        using var ms = new MemoryStream();
        var r = await ctx.App.type.list["goal"].kind.Encode(ms, ctx.Ok(item), ctx, view);
        if (r.Error != null) throw new System.InvalidOperationException($"goal format encode failed: {r.Error.Message}");
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>A format's decode over a stream — the bytes read off it, then the kind's one decode door.</summary>
    public static async System.Threading.Tasks.Task<global::app.data.@this> Decode(this global::app.type.kind.@this format,
        Stream stream, global::app.actor.context.@this ctx, global::app.View view = global::app.View.Out)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return await format.Decode(ms.ToArray(), ctx, view: view);
    }

    /// <summary>A format's decode over a stream, read as <typeparamref name="T"/>.</summary>
    public static async System.Threading.Tasks.Task<global::app.data.@this<T>> Decode<T>(this global::app.type.kind.@this format,
        Stream stream, global::app.actor.context.@this ctx, global::app.View view = global::app.View.Out)
        where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        var read = await format.Decode(stream, ctx, view);
        return read.Success ? read.As<T>() : global::app.data.@this<T>.From(read);
    }

    private static global::app.data.@this<global::app.type.item.text.@this> SerializeTo(global::app.type.kind.@this format,
        global::app.data.@this d, global::app.actor.context.@this ctx, global::app.View view)
    {
        using var ms = new MemoryStream();
        var r = format.Encode(ms, d, ctx, view).GetAwaiter().GetResult();
        return r.Error != null
            ? global::app.data.@this<global::app.type.item.text.@this>.FromError(r.Error)
            : global::app.data.@this<global::app.type.item.text.@this>.Ok(Encoding.UTF8.GetString(ms.ToArray()));
    }

    private static global::app.data.@this DeserializeFrom(global::app.type.kind.@this format, string str,
        global::app.actor.context.@this ctx, global::app.View view)
    {
        if (string.IsNullOrEmpty(str)) return global::app.data.@this.Ok(null);
        return format.Decode(Encoding.UTF8.GetBytes(str), ctx, view: view).GetAwaiter().GetResult();
    }
}
