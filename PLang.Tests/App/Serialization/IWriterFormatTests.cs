using System.Text.Json;

namespace PLang.Tests.App.Serialization;

// plang-types — Stage 2
// IWriter grows a `string Format { get; }` property. Each writer returns its short token
// ("json"/"plang"/"text"/…). The TypedValueNode case calls TypeSerializers.Get(typeName, Format).

public class IWriterFormatTests
{
    private static global::app.channel.serializer.json.Writer MakeJsonWriter(
        System.IO.Stream stream, global::app.types.renderers.@this? renderers = null)
    {
        var utf = new Utf8JsonWriter(stream);
        return new global::app.channel.serializer.json.Writer(utf, options: null,
            view: global::app.View.Out, renderers: renderers);
    }

    [Test]
    public async Task JsonWriter_Format_IsJsonToken()
    {
        using var ms = new System.IO.MemoryStream();
        var w = MakeJsonWriter(ms);
        await Assert.That(w.Format).IsEqualTo("json");
    }

    // Placeholders for PlangWriter / TextWriter Format-token tests were removed;
    // their deferral is captured in Documentation/v0.2/todos.md
    // "Ship PlangWriter / TextWriter". Real tests land when the writers do.

    [Test]
    public async Task Writer_TypedValueNodeCase_CallsLookup_WithOwnFormatToken()
    {
        // Pass a renderer that fires only when the writer's Format matches "json".
        var r = new global::app.types.renderers.@this();
        string? capturedFormat = null;
        r.Register("fmtcheck", "json", (v, w) => { capturedFormat = w.Format; w.String("via-json"); });

        using var ms = new System.IO.MemoryStream();
        var utf = new Utf8JsonWriter(ms);
        var w = new global::app.channel.serializer.json.Writer(utf, options: null,
            view: global::app.View.Out, renderers: r);
        w.Value(new global::app.data.TypedValueNode(new object(), "fmtcheck"));
        utf.Flush();
        await Assert.That(capturedFormat).IsEqualTo("json");
    }

    [Test]
    public async Task Writer_TypedValueNodeCase_FallsBackToStar_WhenSpecificMissing()
    {
        var r = new global::app.types.renderers.@this();
        bool fired = false;
        // Only the wildcard registered — no "json"-specific. Lookup must fall through.
        r.Register("fallback-fixture", global::app.types.renderers.@this.AnyFormat,
            (v, w) => { fired = true; w.String("via-star"); });

        using var ms = new System.IO.MemoryStream();
        var utf = new Utf8JsonWriter(ms);
        var w = new global::app.channel.serializer.json.Writer(utf, options: null,
            view: global::app.View.Out, renderers: r);
        w.Value(new global::app.data.TypedValueNode(new object(), "fallback-fixture"));
        utf.Flush();
        await Assert.That(fired).IsTrue();
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json).IsEqualTo("\"via-star\"");
    }
}
