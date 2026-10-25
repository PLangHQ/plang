namespace PLang.Tests.App.Serialization;

// Bytes a json token reader can't read as what they say they are (a name that is no string, a bool that is a number)
// are the decode's answer — a PlangDeserializeError — never an exception out of Decode: a receive loop lives on.
public class MalformedWireTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    [Arguments("{\"name\":5,\"type\":\"text\",\"value\":\"a\"}")]
    [Arguments("{\"name\":true,\"type\":\"text\",\"value\":\"a\"}")]
    [Arguments("{\"name\":[1],\"type\":\"text\",\"value\":\"a\"}")]
    public async Task AWrongToken_IsTheDecodesError_NotAThrow(string json)
    {
        var ctx = app.actor.list.User.Context;
        var decoded = await app.type.list.Mime("application/plang").Decode(System.Text.Encoding.UTF8.GetBytes(json), ctx, "x");

        await decoded.IsFailure();
        await Assert.That(decoded.Error!.Key).IsEqualTo("PlangDeserializeError");
    }
}
