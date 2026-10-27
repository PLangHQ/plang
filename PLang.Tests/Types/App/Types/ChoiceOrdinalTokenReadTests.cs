namespace PLang.Tests.App.Types;

// A choice member of a host read off tokens is read by choice's own reader: an enum member written by its value (a
// number) reads as that member, as one written by its name does.
public class ChoiceOrdinalTokenReadTests
{
    public sealed class Box
    {
        [global::app.Store]
        public global::app.type.item.choice.@this<global::app.module.debug.Level>? Level { get; set; }
    }

    private static Box Read(global::app.@this app, string json)
    {
        var context = app.actor.list.User.Context;
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
        utf8.Read();
        var reader = new global::app.type.item.kind.json.Reader(utf8, bytes);
        var reflection = (global::app.type.item.kind.reflection.@this)app.type.list["item"].kind["*"]!;
        return (Box)reflection.Read(ref reader, typeof(Box), new global::app.type.reader.ReadContext(context))!;
    }

    [Test]
    public async Task AChoiceWrittenByItsValue_ReadsAsItsMember()
    {
        await using var app = new global::app.@this(System.IO.Path.GetTempPath(), global::PLang.Tests.Parent.Actor).Testing();

        var box = Read(app, "{\"level\": 1}");

        await Assert.That(box.Level?.Value).IsEqualTo(global::app.module.debug.Level.Action);
    }

    [Test]
    public async Task AChoiceWrittenByItsName_ReadsAsItsMember()
    {
        await using var app = new global::app.@this(System.IO.Path.GetTempPath(), global::PLang.Tests.Parent.Actor).Testing();

        var box = Read(app, "{\"level\": \"Action\"}");

        await Assert.That(box.Level?.Value).IsEqualTo(global::app.module.debug.Level.Action);
    }
}
