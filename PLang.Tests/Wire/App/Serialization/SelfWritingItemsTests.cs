namespace PLang.Tests.App.Serialization;

// A structure with a flat form of its own writes itself — its own Write — on the wire, not as a reflected bag:
// a path its location, a url its address, a hash its digest, code its source, a grant and a variable hop their
// object, an error its flat error shape.
public class SelfWritingItemsTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/tmp/selfwrite-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;

    private async Task<string> Json(global::app.type.item.@this value)
    {
        using var ms = new System.IO.MemoryStream();
        var written = await Ctx.Format("application/json").Encode(ms, new global::app.data.@this("v", value, context: Ctx), Ctx);
        await written.IsSuccess();
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    [Test]
    public async Task EachWritesItsOwnForm()
    {
        var filePath = global::app.type.item.path.@this.Resolve("/docs/a.txt", Ctx);
        var httpPath = global::app.type.item.path.@this.Resolve("https://example.com/x/y.json", Ctx);

        await Assert.That(await Json(filePath)).IsEqualTo("\"/docs/a.txt\"");
        await Assert.That(await Json(httpPath)).IsEqualTo("\"https://example.com/x/y.json\"");
        await Assert.That(await Json(new global::app.type.item.url.@this(httpPath, Ctx))).IsEqualTo("\"https://example.com/x/y.json\"");
        await Assert.That(await Json(new global::app.module.crypto.type.hash.@this(new byte[] { 1, 2, 3 }, new global::app.module.crypto.type.hash.kind.sha256.@this()))).IsEqualTo("\"AQID\"");
        await Assert.That(await Json(new global::app.type.code.@this("x = 1", "python"))).IsEqualTo("\"x = 1\"");
        await Assert.That(await Json(new global::app.type.item.permission.@this("user", "/docs/*",
                new HashSet<global::app.type.item.permission.Verb> { global::app.type.item.permission.Verb.read }, global::app.type.item.permission.Match.glob)))
            .IsEqualTo("{\"actor\":\"user\",\"path\":\"/docs/*\",\"match\":\"glob\",\"verbs\":[\"read\"]}");
        var hops = global::app.type.item.variable.@this.Resolve("%a.b%", Ctx).Code.Items().ToList();
        await Assert.That(await Json(hops[0])).IsEqualTo("{\"variable\":\"a\"}");
        await Assert.That(await Json(hops[1])).IsEqualTo("{\"property\":\"b\"}");

        var error = await Json(new global::app.error.ServiceError("boom", "Boom", 500));
        // what kind of error it is, its key says — never its C# class
        await Assert.That(error).StartsWith("{\"id\":\"");
        await Assert.That(error).DoesNotContain("ServiceError");
        await Assert.That(error).Contains("\"message\":\"boom\",\"key\":\"Boom\",\"status\":500");
    }
}
