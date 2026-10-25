using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.NumberTowerTests;

/// What a peer sends is read as what it says: a number slot holding a bool is refused as the json reader refuses any
/// wrong token, never read as a number of nothing.
public class JsonReaderTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/jsonreadertests-" + System.Guid.NewGuid().ToString("N")[..6], global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    // the number type's reader at the one token the json holds: what it reads, or what it was refused with
    private (global::app.type.item.@this? Read, System.Exception? Refused) Number(string json)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var tokens = new System.Text.Json.Utf8JsonReader(bytes);
        tokens.Read();
        var reader = new global::app.type.item.kind.json.Reader(tokens, bytes);
        try { return (new global::app.type.item.number.serializer.Reader().Read(ref reader, null, new global::app.type.reader.ReadContext(_app.actor.list.User.Context)), null); }
        catch (System.Exception ex) { return (null, ex); }
    }

    [Test]
    public async Task ANumberSlot_RefusesABool()
    {
        var (read, refused) = Number("true");
        await Assert.That(read).IsNull().Because($"true is no number; it read as {read?.GetType().FullName}");
        await Assert.That(refused).IsTypeOf<System.Text.Json.JsonException>();
        await Assert.That(refused!.Message).Contains("a number");
    }

    [Test]
    public async Task ANumberSlot_ReadsANumber()
    {
        var (read, refused) = Number("5");
        await Assert.That(refused).IsNull();
        await Assert.That(read?.ToString()).IsEqualTo("5");
    }
}
