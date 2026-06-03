using System.Text.Json;

namespace PLang.Tests.App.Serialization;

// plang-types — Stage 2 (path as first mover)
// app/type/path/serializer/Default.cs ships the new dispatch entry for path.
// The wire shape (portable Relative / Raw / Absolute fallback) is preserved
// exactly — byte-for-byte parity against the legacy JsonConverter.
//
// path.Build("https://…") → "http" lives on path/this.Build.cs (Stage 1).
// The legacy JsonConverter is intentionally retained for STJ read-side parity
// pending a follow-up that migrates every STJ path-typed read to Resolve.

public class PathSerializerMigrationTests
{
    private static global::app.@this NewApp()
        => new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-path-mig-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test]
    public async Task PathFile_Wire_RendersAsRelativeString_ViaDefaultSerializer()
    {
        await using var app = NewApp();
        var context = app.User.Context;
        var p = global::app.type.path.@this.Resolve("/some/file.json", context);
        var data = new global::app.data.@this("x", p) { Context = context };

        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");
        var json = JsonSerializer.Serialize(data,
            (JsonSerializerOptions)typeof(global::app.channel.serializer.plang.@this)
                .GetField("_outbound", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(plang)!);
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
        var p = global::app.type.path.@this.Resolve("https://example.test/a/b", context);
        var data = new global::app.data.@this("x", p) { Context = context };

        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");
        var options = (JsonSerializerOptions)typeof(global::app.channel.serializer.plang.@this)
            .GetField("_outbound", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(plang)!;
        var json = JsonSerializer.Serialize(data, options);
        await Assert.That(json.Contains("example.test")).IsTrue();
        await Assert.That(json.Contains("\"scheme\":")).IsFalse();
    }

    [Test]
    public async Task PathBuild_HttpsScheme_ReturnsHttpKind()
    {
        await Assert.That(global::app.type.path.@this.Build("https://example.test/a")).IsEqualTo("http");
    }

    [Test]
    public async Task PathBuild_FileScheme_ReturnsFileKind()
    {
        await Assert.That(global::app.type.path.@this.Build("/srv/myapp/a.txt")).IsEqualTo("file");
    }

    [Test]
    public async Task PathBuild_Unknown_ReturnsNull_NoThrow()
    {
        await Assert.That(global::app.type.path.@this.Build(null)).IsNull();
        await Assert.That(global::app.type.path.@this.Build("")).IsNull();
        await Assert.That(global::app.type.path.@this.Build(42)).IsNull();
        await Assert.That(global::app.type.path.@this.Build("%var%")).IsNull();
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
        var p = global::app.type.path.@this.Resolve("/srv/myapp/r.json", context);

        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.channel.serializer.json.Writer(utf, options: null,
                view: global::app.View.Out, renderers: app.Type.Renderers);
            w.Value(new global::app.data.TypedValueNode(p, "path"));
        }
        var fromRenderer = System.Text.Encoding.UTF8.GetString(ms.ToArray());

        // The legacy path.JsonConverter is gone; its Write logic now lives in the
        // single json Converter (which routes path the same way). Drive the path
        // through it and compare.
        var opts = new JsonSerializerOptions
        {
            Converters = { new global::app.channel.serializer.json.Converter(context) }
        };
        var fromConverter = JsonSerializer.Serialize<global::app.type.path.@this>(p, opts);

        await Assert.That(fromRenderer).IsEqualTo(fromConverter);
    }
}
