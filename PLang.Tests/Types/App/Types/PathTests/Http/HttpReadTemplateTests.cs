namespace PLang.Tests.App.Types.PathTests.Http;

// "Is a template" is a fact of the reference a read lands, whatever its scheme: a url read with its
// variables resolved is born a template, as a file's is; read plain, it is not.
public class HttpReadTemplateTests
{
    [Test] public async Task AUrlRead_AsATemplate_IsBornATemplate()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var url = global::app.type.item.path.@this.Resolve("https://example.com/t.txt", ctx);

        var read = await url.Read(ctx, new global::app.type.item.template.kind.plang.@this());

        await read.IsSuccess();
        await Assert.That(read.Peek()!.Template?.Name).IsEqualTo("plang");
    }

    // Its text content renders at use: the url's variables are filled from the reader's.
    [Test] public async Task AUrlRead_AsATemplate_RendersItsContent()
    {
        using var server = new HttpTestServer();
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "urltpl-" + System.Guid.NewGuid().ToString("N")[..6])).Testing();
        var ctx = app.actor.list.User.Context;
        var address = server.MapStoredBody(System.Text.Encoding.UTF8.GetBytes("Hello %name%!"), "text/plain");
        var grant = new global::app.type.item.permission.@this("User", new global::app.type.item.path.http.@this(address).Absolute,
            global::app.type.item.permission.@this.AllVerbs, global::app.type.item.permission.Match.Exact);
        await ctx.Actor!.Permission.Add(new global::app.data.@this<global::app.type.item.permission.@this>("", grant, context: ctx), persist: false);
        await ctx.Variable.Set("name", "World");

        var read = await global::app.type.item.path.@this.Resolve(address, ctx).Read(ctx, new global::app.type.item.template.kind.plang.@this());

        await read.IsSuccess();
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("Hello World!");
    }

    [Test] public async Task AUrlRead_Plain_IsNoTemplate()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var url = global::app.type.item.path.@this.Resolve("https://example.com/t.txt", ctx);

        var read = await url.Read(ctx);

        await read.IsSuccess();
        await Assert.That(read.Peek()!.Template).IsNull();
    }
}
