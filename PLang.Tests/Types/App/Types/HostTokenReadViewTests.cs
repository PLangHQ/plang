namespace PLang.Tests.App.Types;

// A host read off tokens is read in its read's view: the members that view shows are the ones it takes.
public class HostTokenReadViewTests
{
    public sealed class Frame
    {
        [global::app.Snapshot]
        public string? Place { get; set; }
    }

    [Test]
    public async Task AHostReadInTheSnapshotView_TakesItsSnapshotMembers()
    {
        await using var app = new global::app.@this(System.IO.Path.GetTempPath(), global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var bytes = System.Text.Encoding.UTF8.GetBytes("{\"place\": \"here\"}");
        var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
        utf8.Read();
        var reader = new global::app.type.item.kind.json.Reader(utf8, bytes);
        var reflection = (global::app.type.item.kind.reflection.@this)app.type.list["item"].kind["*"]!;

        var frame = (Frame)reflection.Read(ref reader, typeof(Frame),
            new global::app.type.reader.ReadContext(context) { View = global::app.View.Snapshot })!;

        await Assert.That(frame.Place).IsEqualTo("here");
    }
}
