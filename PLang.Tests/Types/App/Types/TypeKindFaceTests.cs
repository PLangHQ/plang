namespace PLang.Tests.App.Types;

/// <summary>
/// A value's type kind, as plang reads it (`%x!type.kind%`), is the kind's name as text — "json" for a json file — and
/// null when the type has no kind; never a C# carrier around the kind object.
/// </summary>
public class TypeKindFaceTests
{
    private static async Task<global::app.data.@this> Read(global::app.actor.context.@this ctx, string path)
        => await new global::app.type.item.variable.@this(path, ctx.App.type.list).Start(ctx);

    // Ruling 3: a kind answers its own, else its parent's. The decimal kind has no description of its
    // own, so it falls back to number's (its Family) — the same teaching the number type reads.
    [Test]
    public async Task ADecimalKindsDescription_IsNumbers()
    {
        await using var app = global::PLang.Tests.Session.App("/tmp/plang_kindface_" + Guid.NewGuid().ToString("N")[..8]).Testing();
        var ctx = app.actor.list.User.Context;
        var number = ctx.App.type.list[new global::app.type.@this("number"), ctx];
        var @decimal = number.kind["decimal"];

        await Assert.That(@decimal).IsNotNull();
        await Assert.That(await @decimal!.Description(ctx).Text(ctx))
            .IsEqualTo(await number.Description(ctx).Text(ctx));
    }

    [Test]
    public async Task AJsonFilesTypeKind_IsTheTextJson()
    {
        var root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_kindface_" + Guid.NewGuid().ToString("N")[..8])).FullName;
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(root, "config.json"), "{\"port\":8080}");
        await using var app = global::PLang.Tests.Session.App(root).Testing();
        var ctx = app.actor.list.User.Context;
        var read = await Make.Action(ctx, "file", "read", ("Path", "config.json")).Start(ctx);
        await ctx.Variable.Set(new global::app.data.@this("cfg", read.Peek(), context: ctx));

        // Decision 774: a type's kind is the kind itself, a navigable type — `.kind.name` is "json" and
        // the kind renders as its name. (That json is item's kind, not text's — the text→item move — is
        // decision 773 / stage 3; this stage only makes the kind a navigable type.)
        var kind = await Read(ctx, "cfg!type.kind");
        await Assert.That((await kind.Value())?.ToString()).IsEqualTo("json");

        var name = await Read(ctx, "cfg!type.kind.name");
        await Assert.That((await name.Value())?.ToString()).IsEqualTo("json");
    }

    [Test]
    public async Task APlainTextsTypeKind_IsNull()
    {
        await using var app = global::PLang.Tests.Session.App("/tmp/plang_kindface_" + Guid.NewGuid().ToString("N")[..8]).Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("s", "hi");

        var kind = await Read(ctx, "s!type.kind");

        await Assert.That((await kind.Value())?.IsNull ?? true).IsTrue();
    }
}
