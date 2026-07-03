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
            .Read(ref jr, null, new global::app.type.reader.ReadContext(global::PLang.Tests.TestApp.SharedContext, mode));
    }

    [Test] public async Task AuthoredMode_StampsRefText()
        => await Assert.That(ReadText("\"hi %name%\"", "plang").Template).IsEqualTo("plang");

    [Test] public async Task RuntimeMode_DoesNotStampRefText()
        => await Assert.That(ReadText("\"hi %name%\"", null).Template).IsNull();

    [Test] public async Task HolelessText_NeverStamps_EvenAuthored()
        => await Assert.That(ReadText("\"hello\"", "plang").Template).IsNull();

    private static global::app.type.list.@this ReadList(string json, string? mode)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
        utf8.Read();
        var jr = new global::app.channel.serializer.json.Reader(utf8);
        return (global::app.type.list.@this)new global::app.type.list.serializer.Reader()
            .Read(ref jr, null, new global::app.type.reader.ReadContext(global::PLang.Tests.TestApp.SharedContext, mode));
    }

    // A templated string slot in an authored container rides as a stamped item;
    // a literal slot stays raw. The stamp survives the container's fresh-per-read.
    [Test] public async Task AuthoredContainer_StampsRefSlot_LeavesLiteral()
    {
        var list = ReadList("[\"hi %name%\", \"literal\"]", "plang");
        await Assert.That(list.Items[0].HasVariableReference).IsTrue();
        await Assert.That(list.Items[1].HasVariableReference).IsFalse();
    }

    [Test] public async Task RuntimeContainer_DoesNotStampRefSlot()
    {
        var list = ReadList("[\"hi %name%\"]", null);
        await Assert.That(list.Items[0].HasVariableReference).IsFalse();
    }
}
