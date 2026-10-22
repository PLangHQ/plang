using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.Runtime.App.Types;

/// A json value's parse is never handed to a context other than the one it was parsed for: a typed row inside it (a
/// <c>{type, value}</c> entry, read as the Data it is) is born with the context that parsed it, so a parse the System
/// opened first is not the one a user reads.
public class JsonParseContextTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
        "plang-json-ctx-" + System.Guid.NewGuid().ToString("N")[..8])).TestSigning();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test] public async Task ATypedRow_TheSystemOpenedFirst_IsTheUsersWhenTheUserReadsIt()
    {
        var system = app.actor.list.System.Context;
        var user = app.actor.list.User.Context;
        var source = (global::app.type.item.kind.json.source)(await app.type.list[new global::app.type.@this("item", "json"), system].kind.Load(
            System.Text.Encoding.UTF8.GetBytes("""{"row": {"type": {"name": "text"}, "value": "x"}}"""), system))!.Peek()!;
        await source.AsBooleanAsync(system);                         // the System opens it first

        var row = await new global::app.data.@this("j", source, context: user).Get("row");
        await Assert.That(row.Context.Actor.Name).IsEqualTo(user.Actor.Name);
    }
}
