using System.Reflection;

namespace PLang.Tests.App.CompareRedesign;

// `path`'s string math lives on the type: `path.IsUnder(root)` for containment, `path.Kind` for its extension's
// type (`app.type.list.Extension`). `relative` (a path) and `extension` (text) are its public members, read with a
// dot.
public class Stage7_PathGrowthTests
{
    private static (global::app.@this app, global::app.actor.context.@this context, string dir) MakeApp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "plang_st7pg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var app = new global::app.@this(dir).Testing();
        return (app, app.actor.list.User.Context, dir);
    }

    [Test]
    public async Task PathIsUnder_ReplacesRelativeStartsWith()
    {
        var (app, context, _) = MakeApp();
        await using var __ = app;
        var root = global::app.type.item.path.@this.Resolve("/docs", context);
        var inside = global::app.type.item.path.@this.Resolve("/docs/readme.md", context);
        var outside = global::app.type.item.path.@this.Resolve("/other/readme.md", context);
        await Assert.That(inside.IsUnder(root).Value).IsTrue();
        await Assert.That(outside.IsUnder(root).Value).IsFalse();
        // the builder filter site routes through the type
        var src = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "PLang", "app", "module", "build", "code", "Default.cs"));
        await Assert.That(src).DoesNotContain("Relative.StartsWith");
        await Assert.That(src).Contains("f.Matches(bf, ");
    }

    [Test]
    public async Task PathKind_IsItsExtensionType()
    {
        var (app, context, _) = MakeApp();
        await using var __ = app;
        var p = global::app.type.item.path.@this.Resolve("/data/config.json", context);
        var kind = p.Kind(context);
        await Assert.That(kind.IsNull).IsFalse();
        await Assert.That(kind).IsEqualTo(app.type.list.Extension(".json", context));
    }

    [Test]
    public async Task PathRelative_IsAPublicPath_ThatNeedsTheAskersRoot()
    {
        // Relative needs the asker's root, so it is a one-context method; it answers a path, so it chains.
        var m = typeof(global::app.type.item.path.@this).GetMethod("Relative",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null, new[] { typeof(global::app.actor.context.@this) }, modifiers: null);
        await Assert.That(m).IsNotNull();
        await Assert.That(m!.ReturnType).IsEqualTo(typeof(global::app.type.item.path.@this));
    }

    [Test]
    public async Task PathExtension_IsAPublicText_ReadWithADot()
    {
        var prop = typeof(global::app.type.item.path.@this).GetProperty("Extension", BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(prop).IsNotNull();
        await Assert.That(prop!.PropertyType).IsEqualTo(typeof(global::app.type.item.text.@this));
        var (app, context, _) = MakeApp();
        await using var __ = app;
        var p = global::app.type.item.path.@this.Resolve("docs/readme.md", context);
        var ext = await new global::app.type.item.variable.parser.@this("%p.extension%").Variable.Single()
            .Start(await Bound(context, "p", p));
        await Assert.That((await ext.Value())?.ToString()).IsEqualTo("md");
    }

    // a context holding %name% = value, for a read through plang's own variable door
    private static async Task<global::app.actor.context.@this> Bound(global::app.actor.context.@this context, string name, object value)
    {
        await context.Variable.Set(name, new Data(name, value, context: context));
        return context;
    }

    private static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "PLang", "app")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir!;
    }
}
