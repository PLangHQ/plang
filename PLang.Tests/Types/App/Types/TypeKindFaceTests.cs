namespace PLang.Tests.App.Types;

/// <summary>
/// A value's type kind, as plang reads it (`%x!type.kind%`), is the kind's name as text — "json" for a json file — and
/// null when the type has no kind; never a C# carrier around the kind object.
/// </summary>
public class TypeKindFaceTests
{
    private static async Task<global::app.data.@this> Read(global::app.actor.context.@this ctx, string path)
        => await new global::app.type.item.variable.@this(path).Start(ctx);

    [Test]
    public async Task AJsonFilesTypeKind_IsTheTextJson()
    {
        var root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_kindface_" + Guid.NewGuid().ToString("N")[..8])).FullName;
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(root, "config.json"), "{\"port\":8080}");
        await using var app = new global::app.@this(root).Testing();
        var ctx = app.actor.list.User.Context;
        var read = await Make.Action(ctx, "file", "read", ("Path", "config.json")).Start(ctx);
        await ctx.Variable.Set(new global::app.data.@this("cfg", read.Peek(), context: ctx));

        var kind = await Read(ctx, "cfg!type.kind");

        await Assert.That(await kind.Value()).IsTypeOf<global::app.type.item.text.@this>();
        await Assert.That((await kind.Value())?.ToString()).IsEqualTo("json");
    }

    [Test]
    public async Task APlainTextsTypeKind_IsNull()
    {
        await using var app = new global::app.@this("/tmp/plang_kindface_" + Guid.NewGuid().ToString("N")[..8]).Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("s", "hi");

        var kind = await Read(ctx, "s!type.kind");

        await Assert.That((await kind.Value())?.IsNull ?? true).IsTrue();
    }
}
