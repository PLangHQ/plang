namespace PLang.Tests.App.Types;

// In text a type is the word it goes by with its kind (list<path>); a kind is a type too, and its word is its name.
public class TypeTextLookTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    [Test]
    public async Task AType_InText_IsItsWordWithItsKind()
        => await Assert.That(await Ctx.Written(app.type.list[new global::app.type.@this("list", "path"), Ctx])).IsEqualTo("list<path>");

    [Test]
    public async Task AKind_InText_IsItsName()
        => await Assert.That(await Ctx.Written(app.type.list["item"].kind["json"]!)).IsEqualTo("json");
}
