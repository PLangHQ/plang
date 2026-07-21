using app.module.action.ui;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// The stepActionDetails template renders from the module list (%!app.module.list%): it walks each
/// module's Actions and keeps only those in the planner's set for the step
/// (`planStep.actions contains a.Name`). Catalog actions carry their context, so each answers its
/// Name, Properties (declared param rows composed into the desc: type face + <c>?</c> + <c>= default</c>
/// + <c>%var%</c>), Return, and prose through the lazy file doors. Renders over the REAL catalog +
/// real os/system/modules prose so the shape is pinned against reality.
/// </summary>
public class StepActionDetailsTemplateTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PLang.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("PLang.sln not found");
    }

    private static async Task<string> Render(global::app.@this app, params string[] actionNames)
    {
        // Point OsDirectory at the repo os/ so /system/modules resolves under the authorized system
        // dir (the real builder's path) — an external MarkdownTeachingRoot would be AuthGate-blocked.
        app.OsDirectory = Path.Combine(RepoRoot(), "os");
        var ctx = app.System.Context;

        // %modules% = the module list (%!app.module.list%); %planStep.actions% = the planner's set
        // for this step — the template walks modules and keeps `planStep.actions contains a.Name`.
        ctx.Variable.Set(new Data("modules", app.Module.list, context: ctx));
        ctx.Variable.Set(new Data("planStep",
            new global::app.type.item.dict.@this(ctx).Set("actions",
                new global::app.type.item.list.@this(new List<object?>(actionNames), ctx)),
            context: ctx));

        var template = File.ReadAllText(Path.Combine(RepoRoot(),
            "os/system/builder/llm/templates/stepActionDetails.template"));
        var action = new Render(ctx)
        {
            Template = (global::app.type.item.text.@this)template,
            IsFile = (global::app.type.item.@bool.@this)false,
        };
        var result = await new global::app.module.action.ui.code.Fluid().Render(action);
        await result.IsSuccess();
        return (await result.Value())?.ToString() ?? "";
    }

    [Test]
    public async Task Renders_ActionHeader_AndParams()
    {
        await using var app = TestApp.Create("/app");
        var outp = await Render(app, "file.read");

        // Structural render over the real catalog (prose-block rendering is proven by
        // ProseDoorFluidTests — the template's {% if a.X %}{{ a.X }} is that same construct).
        await Assert.That(outp).Contains("## file.read");
        await Assert.That(outp).Contains("Parameters:");
        await Assert.That(outp).Contains("Path: path");     // the declared param row → desc "path"
    }

    [Test]
    public async Task Renders_MultipleActions_EachAsABlock()
    {
        await using var app = TestApp.Create("/app");
        var outp = await Render(app, "file.read", "variable.set");

        await Assert.That(outp).Contains("## file.read");
        await Assert.That(outp).Contains("## variable.set");
    }

    [Test]
    public async Task NoCacheModifier_TaggedInHeader()
    {
        await using var app = TestApp.Create("/app");
        // variable.set is [no-cache] (Cacheable=false) — the header tags it.
        var outp = await Render(app, "variable.set");
        await Assert.That(outp).Contains("## variable.set [no-cache]");
    }
}
