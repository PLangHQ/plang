using System.Text.Json;

namespace PLang.Tests.App.Serialization;

// plang-types — Stage 2
// IWriter grows a `string Format { get; }` property. Each writer returns its short token
// ("json"/"plang"/"text"/…). The TypedValueNode case calls (await TypeSerializers.Get(typeName, Format)).

public class IWriterFormatTests
{
    private static global::app.channel.serializer.json.Writer MakeJsonWriter(
        System.IO.Stream stream, global::app.type.renderer.@this? renderers = null)
    {
        var utf = new Utf8JsonWriter(stream);
        return new global::app.channel.serializer.json.Writer(utf,
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
    //
    // The TypedValueNode-dispatch tests were removed with TypedValueNode itself:
    // a value now renders itself via item.Write, the writer no longer routes
    // through a per-(type, format) renderer marker.
}
