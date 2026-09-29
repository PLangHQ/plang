namespace PLang.Tests.App.Types.PathTests.Http;

// "Is a template" is a fact of the reference a read lands, whatever its scheme: a url read with its
// variables resolved is born a template, as a file's is; read plain, it is not.
public class HttpReadTemplateTests
{
    [Test] public async Task AUrlRead_AsATemplate_IsBornATemplate()
    {
        await using var app = TestApp.Create("/app");
        var ctx = app.actor.list.User.Context;
        var url = global::app.type.item.path.@this.Resolve("https://example.com/t.txt", ctx);

        var read = await url.Read(ctx, true);

        await read.IsSuccess();
        await Assert.That(read.Peek()!.Template).IsEqualTo("plang");
    }

    [Test] public async Task AUrlRead_Plain_IsNoTemplate()
    {
        await using var app = TestApp.Create("/app");
        var ctx = app.actor.list.User.Context;
        var url = global::app.type.item.path.@this.Resolve("https://example.com/t.txt", ctx);

        var read = await url.Read(ctx);

        await read.IsSuccess();
        await Assert.That(read.Peek()!.Template).IsNull();
    }
}
