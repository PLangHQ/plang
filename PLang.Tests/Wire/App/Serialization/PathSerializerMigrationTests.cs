using System.Text.Json;

namespace PLang.Tests.App.Serialization;

// plang-types — Stage 2 (path as first mover)
// app/type/path/serializer/Default.cs ships the new dispatch entry for path.
// The wire shape (portable Relative / Raw / Absolute fallback) is preserved
// exactly — byte-for-byte parity against the legacy JsonConverter.
//
// A path's kind is its scheme, read off the built value (App.Type["path"].Create) — no
// build-time hook. https stays https (scheme-accurate).

public class PathSerializerMigrationTests
{
    private static global::app.@this NewApp()
        => global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-path-mig-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task PathFile_Wire_RendersAsRelativeString_ViaDefaultSerializer()
    {
        await using var app = NewApp();
        var context = app.User.Context;
        var p = global::app.type.item.path.@this.Resolve("/some/file.json", context);
        var data = new global::app.data.@this("x", p, context: context);

        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");
        using var ms = new System.IO.MemoryStream();
        await plang.SerializeAsync(ms, data, global::app.View.Out);
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        // The wire emits the value as a string. The exact content depends on
        // Relative computation against App root; we just assert it's a string
        // (not a property bag) and contains the file segment.
        await Assert.That(json.Contains("\"file.json\"") || json.Contains("file.json")).IsTrue();
        await Assert.That(json.Contains("\"absolute\":")).IsFalse();   // no reflection bag
        await Assert.That(json.Contains("\"scheme\":")).IsFalse();
    }

    [Test]
    public async Task PathHttp_Wire_RendersAsAbsoluteString_ViaDefaultSerializer()
    {
        await using var app = NewApp();
        var context = app.User.Context;
        var p = global::app.type.item.path.@this.Resolve("https://example.test/a/b", context);
        var data = new global::app.data.@this("x", p, context: context);

        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");
        using var ms = new System.IO.MemoryStream();
        await plang.SerializeAsync(ms, data, global::app.View.Out);
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json.Contains("example.test")).IsTrue();
        await Assert.That(json.Contains("\"scheme\":")).IsFalse();
    }

    // Kind now derives by building through the family's eager door and reading it off the
    // built value (KindHooks + path.Build deleted) — https→http, bare→file, unchanged.
    private static string? KindVia(global::app.@this app, string typeName, object? raw)
    {
        var ctx = app.User.Context;
        var carrier = new global::app.data.@this("", new global::app.type.item.@null.@this(typeName), context: ctx);
        return ctx.App.Type[typeName].Create(raw, carrier)?.Type.Kind?.Name;
    }

    [Test]
    public async Task PathKind_HttpsScheme_ViaCreate_IsHttps()
    {
        // Scheme-accurate (W8 A): a built HttpPath reports its real scheme — https stays https
        // (the deleted path.Build hook collapsed http+https → "http").
        await using var app = NewApp();
        await Assert.That(KindVia(app, "path", "https://example.test/a")).IsEqualTo("https");
    }

    [Test]
    public async Task PathKind_FileScheme_ViaCreate_IsFile()
    {
        await using var app = NewApp();
        await Assert.That(KindVia(app, "path", "/srv/myapp/a.txt")).IsEqualTo("file");
    }

    // Placeholder removed; converter deletion is tracked in
    // Documentation/v0.2/todos.md "Delete app/type/path/this.JsonConverter.cs"
    // (structural — requires ITypeRenderer.Read or a record-schema redesign).
    // The real "JsonConverter is absent" assertion lands with the migration.

    [Test]
    public async Task Path_Wire_ByteForByteParity_BeforeAndAfter_Migration()
    {
        // The Default.cs renderer mirrors JsonConverter.Write exactly
        // (Relative ?? Raw ?? Absolute). Drive the same path through both
        // paths and compare the produced JSON string.
        await using var app = NewApp();
        var context = app.User.Context;
        var p = global::app.type.item.path.@this.Resolve("/srv/myapp/r.json", context);

        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.channel.serializer.json.Writer(utf,
                view: global::app.View.Out, renderers: app.Type.Renderer);
            w.Value(p);
        }
        var fromRenderer = System.Text.Encoding.UTF8.GetString(ms.ToArray());

        // The legacy path.JsonConverter is gone; its Write logic now lives in the
        // single json Converter (which routes path the same way). Drive the path
        // through it and compare.
        var opts = new JsonSerializerOptions
        {
            Converters = { new global::app.channel.serializer.json.Converter(context) }
        };
        var fromConverter = JsonSerializer.Serialize<global::app.type.item.path.@this>(p, opts);

        await Assert.That(fromRenderer).IsEqualTo(fromConverter);
    }
}
