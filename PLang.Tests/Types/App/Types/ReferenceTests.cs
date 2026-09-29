using UrlValue = global::app.type.item.url.@this;
using PathValue = global::app.type.item.path.@this;
using HttpTestServer = PLang.Tests.App.Types.PathTests.Http.HttpTestServer;

namespace PLang.Tests.App.Types;

// A reference (file, url) samples its bytes once with their format; the format decodes them and says whether
// they are text. The reference itself is no plang type.
public class ReferenceTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-reference-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    // The base is held internal — out of the types' face — and its subclasses are not: each class declares it.
    [Test] public async Task TheReference_IsInternal_ItsSubclassesAreNot()
    {
        await Assert.That(app.type.list["content"].Internal).IsTrue();
        await Assert.That(app.type.list["file"].Internal).IsFalse();
        await Assert.That(app.type.list["url"].Internal).IsFalse();
        await Assert.That(app.type.list["clr"].Internal).IsTrue();
    }

    [Test]
    [Arguments("text/plain", true)]
    [Arguments("text/csv", true)]
    [Arguments("application/xml", true)]
    [Arguments("application/json", true)]
    [Arguments("image/png", false)]
    [Arguments("application/octet-stream", false)]
    public async Task AKind_SaysWhetherItsContentIsText(string mime, bool text)
        => await Assert.That(app.type.list.Mime(mime).IsText).IsEqualTo(text);

    // The response's Content-Type is the format: a url with no extension served as json decodes as json.
    [Test] public async Task AUrl_DecodesByItsContentType()
    {
        using var server = new HttpTestServer();
        var address = server.MapStoredBody(System.Text.Encoding.UTF8.GetBytes("{\"a\":1}"), "application/json");
        var grant = new global::app.type.item.permission.@this("User", new global::app.type.item.path.http.@this(address).Absolute,
            global::app.type.item.permission.@this.AllVerbs, global::app.type.item.permission.Match.Exact);
        await Ctx.Actor!.Permission.Add(new global::app.data.@this<global::app.type.item.permission.@this>("", grant, context: Ctx), persist: false);

        var url = new UrlValue(PathValue.Resolve(address, Ctx), Ctx);
        var held = new global::app.data.@this("u", url, context: Ctx);
        var content = await held.Value();

        await held.IsSuccess();
        await Assert.That(content!.Type.kind.Name).IsEqualTo("json");
    }
}
