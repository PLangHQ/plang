using HttpTestServer = PLang.Tests.App.Types.PathTests.Http.HttpTestServer;

namespace PLang.Tests.App.Types.PathTests;

// plang's own content from another actor answers only signed — a url refuses what doesn't verify, as the
// http action does.
public class UrlPlangContentTests
{
    private static (global::app.@this app, string root) NewApp()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-urlplang-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        // real signing — the no-crypto test signer verifies anything, and verifying is what's under test
        var app = new global::app.@this(root);
        TestApp.UseSharedIdentity(app);
        return (app, root);
    }

    private static async Task<global::app.data.@this> Read(global::app.@this app, string url)
    {
        var ctx = app.actor.list.User.Context;
        // the actor may read the url — the consent gate isn't what's under test
        var grant = new global::app.type.item.permission.@this("User", new global::app.type.item.path.http.@this(url).Absolute,
            global::app.type.item.permission.@this.AllVerbs, global::app.type.item.permission.Match.Exact);
        await ctx.Actor!.Permission.Add(new global::app.data.@this<global::app.type.item.permission.@this>("", grant, context: ctx), persist: true);
        var data = new global::app.data.@this("fetched",
            new global::app.type.item.url.@this(global::app.type.item.path.@this.Resolve(url, ctx)!, ctx), context: ctx);
        await data.Value();
        return data;
    }

    [Test] public async Task UnsignedPlangContent_IsRefused()
    {
        using var server = new HttpTestServer();
        var (app, root) = NewApp();
        try
        {
            var ctx = app.actor.list.User.Context;
            // the signed write's inner @schema:data record — the same content with its signature taken off
            var signed = (await ctx.Format("application/plang").Serialize(ctx.Ok("hello"), ctx).Value())!.Clr<string>()!;
            using var doc = System.Text.Json.JsonDocument.Parse(signed);
            var unsigned = System.Text.Encoding.UTF8.GetBytes(doc.RootElement.GetProperty("value").GetRawText());

            var url = server.MapStoredBody(unsigned, "application/plang");
            var read = await Read(app, url);
            await Assert.That(read.Success).IsFalse();
            await Assert.That(read.Error!.Key).IsEqualTo("UnsignedPlang");
        }
        finally { await app.DisposeAsync(); System.IO.Directory.Delete(root, recursive: true); }
    }

    [Test] public async Task SignedPlangContent_IsAccepted()
    {
        using var server = new HttpTestServer();
        var (app, root) = NewApp();
        try
        {
            var ctx = app.actor.list.User.Context;
            var signed = (await ctx.Format("application/plang").Serialize(ctx.Ok("hello"), ctx).Value())!.Clr<string>()!;
            var read = await Read(app, server.MapStoredBody(System.Text.Encoding.UTF8.GetBytes(signed), "application/plang"));
            await read.IsSuccess();
        }
        finally { await app.DisposeAsync(); System.IO.Directory.Delete(root, recursive: true); }
    }
}
