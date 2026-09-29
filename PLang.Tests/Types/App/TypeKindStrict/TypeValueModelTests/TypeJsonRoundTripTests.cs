using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// A type descriptor {name, kind?, strict?} reads through the type's own reader — the door a Data row's
// `type` slot is read through.
public class TypeJsonRoundTripTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/typejson-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.type.@this Read(string json)
    {
        var ctx = _app.actor.list.User.Context;
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
        utf8.Read();
        var reader = new global::app.type.item.kind.json.Reader(utf8, bytes);
        return (global::app.type.@this)ctx.App.type.list.Reader.Reader("type", null, ctx)
            .Read(ref reader, null, new global::app.type.reader.ReadContext(ctx));
    }

    [Test] public async Task Deserialize_JustName_Works()
    {
        var entity = Read("{\"name\":\"text\"}");
        await Assert.That(entity.Name).IsEqualTo("text");
        await Assert.That(entity.kind.IsEmpty).IsTrue();
        await Assert.That(entity.Strict).IsFalse();
    }

    [Test] public async Task Deserialize_FullDict_Works()
    {
        var entity = Read("{\"name\":\"image\",\"kind\":\"gif\",\"strict\":true}");
        await Assert.That(entity.Name).IsEqualTo("image");
        await Assert.That(entity.kind.Name).IsEqualTo("gif");
        await Assert.That(entity.Strict).IsTrue();
    }
}
