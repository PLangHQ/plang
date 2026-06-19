namespace PLang.Tests.App.Serialization;

// Template stamping at read — the trust rides the reader's mode, not the content.
// The SAME %ref% bytes born a live template under the authored mode ("plang") and
// a literal under runtime-ingest (null). The type owns the holes-decision, so a
// holeless string never carries the stamp (HasVariableReference stays correct).
public class TemplateStampOnReadTests
{
    private static global::app.type.text.@this ReadText(string json, string? mode)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
        utf8.Read();
        var jr = new global::app.channel.serializer.json.Reader(utf8);
        return (global::app.type.text.@this)new global::app.type.text.serializer.Reader()
            .Read(ref jr, null, new global::app.type.reader.ReadContext(null, mode));
    }

    [Test] public async Task AuthoredMode_StampsRefText()
        => await Assert.That(ReadText("\"hi %name%\"", "plang").Template).IsEqualTo("plang");

    [Test] public async Task RuntimeMode_DoesNotStampRefText()
        => await Assert.That(ReadText("\"hi %name%\"", null).Template).IsNull();

    [Test] public async Task HolelessText_NeverStamps_EvenAuthored()
        => await Assert.That(ReadText("\"hello\"", "plang").Template).IsNull();
}
