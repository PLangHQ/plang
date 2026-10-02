namespace PLang.Tests.App.Serialization;

// Template stamping at read — the trust rides the reader's mode, not the content.
// The SAME %ref% bytes born a live template under the authored mode ("plang") and
// a literal under runtime-ingest (null). The type owns the holes-decision, so a
// holeless string never carries the stamp (HasVariable stays correct).
public class TemplateStampOnReadTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.type.item.text.@this ReadText(string json, global::app.type.item.template.kind.@this? mode)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
        utf8.Read();
        var jr = new global::app.type.item.kind.json.Reader(utf8);
        return (global::app.type.item.text.@this)new global::app.type.item.text.serializer.Reader()
            .Read(ref jr, null, new global::app.type.reader.ReadContext(app.actor.list.User.Context, mode));
    }

    [Test] public async Task AuthoredMode_StampsRefText()
        => await Assert.That(ReadText("\"hi %name%\"", new global::app.type.item.template.kind.plang.@this()).Template?.Name).IsEqualTo("plang");

    [Test] public async Task RuntimeMode_DoesNotStampRefText()
        => await Assert.That(ReadText("\"hi %name%\"", null).Template).IsNull();

    [Test] public async Task HolelessText_NeverStamps_EvenAuthored()
        => await Assert.That(ReadText("\"hello\"", new global::app.type.item.template.kind.plang.@this()).Template).IsNull();

    private global::app.type.item.list.@this ReadList(string json, global::app.type.item.template.kind.@this? mode)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
        utf8.Read();
        var jr = new global::app.type.item.kind.json.Reader(utf8);
        return (global::app.type.item.list.@this)new global::app.type.item.list.serializer.Reader()
            .Read(ref jr, null, new global::app.type.reader.ReadContext(app.actor.list.User.Context, mode));
    }

    // A templated string slot in an authored container rides as a stamped item;
    // a literal slot stays raw. The stamp survives the container's fresh-per-read.
    [Test] public async Task AuthoredContainer_StampsRefSlot_LeavesLiteral()
    {
        var list = ReadList("[\"hi %name%\", \"literal\"]", new global::app.type.item.template.kind.plang.@this());
        await Assert.That(list.Items(app.actor.list.User.Context).ElementAt(0).HasVariable).IsTrue();
        await Assert.That(list.Items(app.actor.list.User.Context).ElementAt(1).HasVariable).IsFalse();
    }

    [Test] public async Task RuntimeContainer_DoesNotStampRefSlot()
    {
        var list = ReadList("[\"hi %name%\"]", null);
        await Assert.That(list.Items(app.actor.list.User.Context).ElementAt(0).HasVariable).IsFalse();
    }
}
