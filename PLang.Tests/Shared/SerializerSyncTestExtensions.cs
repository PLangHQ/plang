using System.IO;
using System.Text;
using app.channel.serializer;

namespace PLang.Tests.Shared;

/// <summary>
/// Sync convenience wrappers over the (async, stream-only) <see cref="ISerializer"/> — TESTS
/// ONLY. Production stopped exposing sync string Serialize/Deserialize/Store/Load; the serializer
/// is stream-native and the string↔stream bridge belongs to whoever stores text. Tests just want
/// a quick round-trip string, so these extensions restore the old sync names over the async
/// stream API (sync-over-async is fine in a test harness). Out = transport, Store = persistence.
/// </summary>
public static class SerializerSyncTestExtensions
{
    public static global::app.data.@this<global::app.type.item.text.@this> Serialize(this ISerializer s, global::app.data.@this d)
        => SerializeTo(s, d, global::app.View.Out);

    public static global::app.data.@this<global::app.type.item.text.@this> Store(this ISerializer s, global::app.data.@this d)
        => SerializeTo(s, d, global::app.View.Store);

    public static global::app.data.@this Deserialize(this ISerializer s, string str)
        => DeserializeFrom(s, str, global::app.View.Out);

    public static global::app.data.@this Load(this ISerializer s, string str)
        => DeserializeFrom(s, str, global::app.View.Store);

    public static global::app.data.@this<T> Deserialize<T>(this ISerializer s, string str)
        where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        if (string.IsNullOrEmpty(str)) return global::app.data.@this<T>.Ok(default!);
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(str));
        return s.DeserializeAsync<T>(ms).GetAwaiter().GetResult();
    }

    private static global::app.data.@this<global::app.type.item.text.@this> SerializeTo(ISerializer s, global::app.data.@this d, global::app.View view)
    {
        using var ms = new MemoryStream();
        var r = s.SerializeAsync(ms, d, view).GetAwaiter().GetResult();
        return r.Error != null
            ? global::app.data.@this<global::app.type.item.text.@this>.FromError(r.Error)
            : global::app.data.@this<global::app.type.item.text.@this>.Ok(Encoding.UTF8.GetString(ms.ToArray()));
    }

    private static global::app.data.@this DeserializeFrom(ISerializer s, string str, global::app.View view)
    {
        if (string.IsNullOrEmpty(str)) return global::app.data.@this.Ok(null);
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(str));
        return s.DeserializeAsync(ms, view).GetAwaiter().GetResult();
    }
}
