using UrlValue = global::app.type.item.url.@this;
using PathValue = global::app.type.item.path.@this;
using HttpTestServer = PLang.Tests.App.Types.PathTests.Http.HttpTestServer;

namespace PLang.Tests.App.Types;

// Stage 4 (kind-is-type, decision 792): a url served `application/json` decodes to a value whose
// `type.name` is `item` and `type.kind.name` is `json` (json is item's, decision 773).
public class UrlDecodesByContentTypeJsonTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.Session.App(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-urljson-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test] public async Task UrlServedApplicationJson_DecodesAsItemOfKindJson()
    {
        using var server = new HttpTestServer();
        var address = server.MapStoredBody(System.Text.Encoding.UTF8.GetBytes("{\"a\":1}"), "application/json");
        var grant = new global::app.type.item.permission.@this("User", Ctx.Url(address).Absolute,
            global::app.type.item.permission.@this.AllVerbs, global::app.type.item.permission.Match.exact, Ctx.App.type.list[typeof(global::app.type.item.permission.@this)]);
        await Ctx.Actor!.Permission.Add(new global::app.data.@this<global::app.type.item.permission.@this>("", grant, context: Ctx), persist: false);

        var url = new UrlValue(PathValue.Resolve(address, Ctx), Ctx);
        var held = new global::app.data.@this("u", url, context: Ctx);
        var content = await held.Value();

        await held.IsSuccess();
        await Assert.That(content!.Type.Name).IsEqualTo("item");
        await Assert.That(content!.Type.kind.Name).IsEqualTo("json");
    }
}
