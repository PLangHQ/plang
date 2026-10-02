namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// A path holds no context. The members that need one — <c>Relative</c>, <c>MimeType</c>,
/// <c>Kind</c> — are one-context methods, and the <c>!</c> plane answers them with the
/// asking Data's own context.
/// </summary>
public class PathAskerContextTests
{
    private static global::app.@this NewApp() =>
        new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-asker-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();

    private static async Task<string?> Nav(global::app.data.@this d, string key) =>
        (await (await d.Get(key)).Value())?.ToString();

    [Test] public async Task BangPlane_ContextMembers_AnswerWithTheDatasContext()
    {
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        var p = global::app.type.item.path.@this.Resolve("/data/config.json", ctx);
        var d = new global::app.data.@this("p", p, context: ctx);

        await Assert.That(await Nav(d, "relative")).IsEqualTo("/data/config.json");
        await Assert.That(await Nav(d, "mimetype")).IsEqualTo("application/json");
    }

    // A step's read: %name.member% through plang's own variable door, as the asker.
    private static async Task<string?> Read(global::app.actor.context.@this ctx, string variable)
    {
        var read = await new global::app.type.item.variable.parser.@this(variable).Variable.Single().Start(ctx);
        return read.Success ? (await read.Value())?.ToString() : "refused: " + read.Error?.Key;
    }

    [Test] public async Task APathsRelativeAndExtension_AreReadWithADot()
    {
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("p", new global::app.data.@this("p", global::app.type.item.path.@this.Resolve("/data/config.json", ctx), context: ctx));

        await Assert.That(await Read(ctx, "%p.relative%")).IsEqualTo("/data/config.json");
        await Assert.That(await Read(ctx, "%p.extension%")).IsEqualTo("json");
        await Assert.That(await Read(ctx, "%p.relative.extension%")).IsEqualTo("json");
    }

    // `!` reads facts about the thing — a Data's own and a reference's own; a plain path's members are read with a dot.
    [Test] public async Task APlainPathsRelative_IsNoBangFact()
    {
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("p", new global::app.data.@this("p", global::app.type.item.path.@this.Resolve("/data/config.json", ctx), context: ctx));

        await Assert.That(await Read(ctx, "%p!relative%")).IsNotEqualTo("/data/config.json");
    }

    // A read file: its dot is its content (the JSON's own `path` key); its `!path` is the file's location, whose
    // members read with a dot.
    [Test] public async Task AReadFile_BangPathIsItsLocation_DotIsItsContent()
    {
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(app.AbsolutePath, "data"));
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(app.AbsolutePath, "data", "config.json"), "{\"path\": \"its own key\"}");
        var read = await global::app.type.item.path.@this.Resolve("/data/config.json", ctx).Read(ctx);
        await read.IsSuccess();
        await ctx.Variable.Set("config", read);

        await Assert.That(await Read(ctx, "%config!path.relative%")).IsEqualTo("/data/config.json");
        await Assert.That(await Read(ctx, "%config.path%")).IsEqualTo("its own key");
    }

    [Test] public async Task OsLocation_ShowsItsPlangForm_NeverTheInstallRoot()
    {
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        var inside = System.IO.Path.Combine(app.AbsolutePath, "data", "x.txt");
        var outside = "//tmp/plang-outside-" + System.Guid.NewGuid().ToString("N")[..6] + ".txt";

        await Assert.That(global::app.type.item.path.@this.Resolve(inside, ctx).ToString()).IsEqualTo("/data/x.txt");
        await Assert.That(global::app.type.item.path.@this.Resolve(outside, ctx).ToString()).IsEqualTo(outside);
        // A typed location stays as typed.
        await Assert.That(global::app.type.item.path.@this.Resolve("data/x.txt", ctx).ToString()).IsEqualTo("data/x.txt");
    }

    [Test] public async Task ListingEntries_FromAnOsRoot_ShowTheirPlangForm()
    {
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        var dir = System.IO.Path.Combine(app.AbsolutePath, "docs");
        System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "a.txt"), "a");

        var root = global::app.type.item.path.@this.Resolve(dir, ctx);
        var listed = await root.List(ctx);
        await listed.IsSuccess();
        var entries = (await listed.Value())!.Items().Select(p => p.ToString()).ToList();

        await Assert.That(root.ToString()).IsEqualTo("/docs");
        await Assert.That(entries).Contains("/docs/a.txt");
    }

    [Test] public async Task Relative_IsTheAskersRoot_NotTheCreators()
    {
        await using var app1 = NewApp();
        await using var app2 = NewApp();
        var p = global::app.type.item.path.@this.Resolve("/data/x.txt", app1.actor.list.User.Context);

        await Assert.That(p.Relative(app1.actor.list.User.Context).Raw).IsEqualTo("/data/x.txt");
        // Outside the asker's root the relative form is the location itself.
        await Assert.That(p.Relative(app2.actor.list.User.Context).Raw).IsEqualTo(p.Absolute);
    }
}
