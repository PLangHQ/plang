using System.Reflection;

namespace PLang.Tests.App.CompareRedesign;

// Stage 7 — `path`'s interior string-math moves onto the type (OBP smell #5).
// `path.IsUnder(root)` replaces `f.Relative.StartsWith(rootRel)`; `path.Kind`
// replaces `Format.TypeFromExtension(p.Extension)`. Raw `.Relative` /
// `.Extension` become `internal`, feeding the new methods + the `!relative` /
// `!extension` derived projections.
public class Stage7_PathGrowthTests
{
    private static (global::app.@this app, global::app.actor.context.@this context, string dir) MakeApp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "plang_st7pg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var app = new global::app.@this(dir);
        return (app, app.User.Context, dir);
    }

    [Test]
    public async Task PathIsUnder_ReplacesRelativeStartsWith()
    {
        var (app, context, _) = MakeApp();
        await using var __ = app;
        var root = global::app.type.path.@this.Resolve("/docs", context);
        var inside = global::app.type.path.@this.Resolve("/docs/readme.md", context);
        var outside = global::app.type.path.@this.Resolve("/other/readme.md", context);
        await Assert.That(inside.IsUnder(root).Value).IsTrue();
        await Assert.That(outside.IsUnder(root).Value).IsFalse();
        // the builder filter site routes through the type
        var src = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "PLang", "app", "module", "builder", "code", "Default.cs"));
        await Assert.That(src).DoesNotContain("Relative.StartsWith");
        await Assert.That(src).Contains("f.Matches(bf)");
    }

    [Test]
    public async Task PathKind_ReplacesFormatTypeFromExtension()
    {
        var (app, context, _) = MakeApp();
        await using var __ = app;
        var p = global::app.type.path.@this.Resolve("/data/config.json", context);
        var kind = p.Kind;
        await Assert.That(kind.IsNull).IsFalse();
        // the file.read Build hint routes through the type
        var src = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "PLang", "app", "module", "file", "read.cs"));
        await Assert.That(src).DoesNotContain("TypeFromExtension(p.Extension)");
        await Assert.That(src).Contains("p.Kind");
    }

    [Test]
    public async Task PathRelative_NowInternal_NotOnPublicSurface()
    {
        var prop = typeof(global::app.type.path.@this).GetProperty("Relative",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(prop).IsNotNull();
        await Assert.That(prop!.GetMethod!.IsPublic).IsFalse();
        await Assert.That(prop.GetMethod.IsAssembly).IsTrue();
    }

    [Test]
    public async Task PathExtension_NowInternal_PublicViaBangExtension()
    {
        var prop = typeof(global::app.type.path.@this).GetProperty("Extension",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(prop).IsNotNull();
        await Assert.That(prop!.GetMethod!.IsAssembly).IsTrue();
        // the `!extension` projection still answers on the property plane
        var (app, context, _) = MakeApp();
        await using var __ = app;
        var p = global::app.type.path.@this.Resolve("docs/readme.md", context);
        var data = new Data("p", p, context: context);
        var ext = await data.GetChild("!extension");
        await Assert.That(ext.Peek()?.ToString()).IsEqualTo("md");
    }

    private static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "PLang", "app")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir!;
    }
}
