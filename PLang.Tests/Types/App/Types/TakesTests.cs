using FileValue = global::app.type.item.file.@this;
using UrlValue = global::app.type.item.url.@this;
using PathValue = global::app.type.item.path.@this;
using HttpTestServer = PLang.Tests.App.Types.PathTests.Http.HttpTestServer;

namespace PLang.Tests.App.Types;

// A type's own "made from" answer: a value of a type it takes, declared it, is made into it by its own birth,
// handed the declaration; any other container declared it is held as it is.
public class TakesTests : System.IAsyncDisposable
{
    private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-takes-" + System.Guid.NewGuid().ToString("N")[..8]);
    private readonly global::app.@this app;
    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    public TakesTests()
    {
        System.IO.Directory.CreateDirectory(root);
        app = new global::app.@this(root).Testing();
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.type.@this Declared(string name, string? kind = null, string? template = null)
        => app.type.list[new global::app.type.@this(name, kind, template: template), Ctx];

    [Test] public async Task AUrlMadeFromAPath_IsBornWithTheDeclaredTemplate()
    {
        var address = PathValue.Resolve("https://example.com/t.txt", Ctx);

        var made = Declared("url", template: "plang").Make(address, Ctx);

        await Assert.That(made).IsTypeOf<UrlValue>();
        await Assert.That(((UrlValue)made).Path).IsSameReferenceAs(address);
        await Assert.That(made.Template).IsEqualTo("plang");
    }

    // The declaration rides beside the binding: a binding that declares nothing still births a template.
    [Test] public async Task TheDeclaration_ReachesTheBirth_NotTheBinding()
    {
        var declared = Declared("file", template: "plang");
        var binding = new global::app.data.@this("", context: Ctx);

        var aFile = FileValue.Create(PathValue.Resolve("some.txt", Ctx), declared, binding);
        var aUrl = UrlValue.Create(PathValue.Resolve("https://example.com/t.txt", Ctx), declared, binding);

        await Assert.That(aFile!.Template).IsEqualTo("plang");
        await Assert.That(aUrl!.Template).IsEqualTo("plang");
    }

    [Test] public async Task APlainList_DeclaredATypedList_IsTakenAsIt()
    {
        var plain = new global::app.type.item.list.@this();

        var made = Declared("list", "path").Make(plain, Ctx);

        await Assert.That(made).IsTypeOf<global::app.type.item.list.@this<PathValue>>();
    }

    [Test] public async Task ADict_DeclaredATypedList_IsHeld()
    {
        var dict = new global::app.type.item.dict.@this();

        var made = Declared("list", "path").Make(dict, Ctx);

        await Assert.That(made).IsSameReferenceAs(dict);
    }

    // A file and a url born templates render their text content the same way, from the reader's variables.
    [Test] public async Task AFileAndAUrl_DeclaredTemplates_RenderAlike()
    {
        using var server = new HttpTestServer();
        var body = System.Text.Encoding.UTF8.GetBytes("Hello %name%!");
        await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(root, "t.txt"), body);
        var address = server.MapStoredBody(body, "text/plain");
        var grant = new global::app.type.item.permission.@this("User", new global::app.type.item.path.http.@this(address).Absolute,
            global::app.type.item.permission.@this.AllVerbs, global::app.type.item.permission.Match.Exact);
        await Ctx.Actor!.Permission.Add(new global::app.data.@this<global::app.type.item.permission.@this>("", grant, context: Ctx), persist: false);
        await Ctx.Variable.Set("name", "World");

        var aFile = Declared("file", template: "plang").Make(PathValue.Resolve("t.txt", Ctx), Ctx);
        var aUrl = Declared("url", template: "plang").Make(PathValue.Resolve(address, Ctx), Ctx);

        var fromFile = await new global::app.data.@this("f", aFile, context: Ctx).Value();
        var fromUrl = await new global::app.data.@this("u", aUrl, context: Ctx).Value();

        await Assert.That(fromFile?.ToString()).IsEqualTo("Hello World!");
        await Assert.That(fromUrl?.ToString()).IsEqualTo(fromFile?.ToString());
    }
}
