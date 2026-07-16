using app.module.action.ui;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// The 4d summary.planner template renders the module-element TREE — `{% for module in modules %}`
/// with module.Name / module.Description and module.Actions / module.Modifiers as the structural
/// split. The old flat `map|uniq|sort` + `where "IsModifier"` grouping is gone. Structure pinned over
/// the real catalog (module descriptions ride ProseDoorFluidTests — same file-door construct).
/// </summary>
public class SummaryPlannerTemplateTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PLang.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("PLang.sln not found");
    }

    private static async Task<string> Render(global::app.@this app)
    {
        var ctx = app.System.Context;
        ctx.Variable.Set(new Data("modules", app.Module.list, context: ctx));
        var template = File.ReadAllText(Path.Combine(RepoRoot(), "os/system/actions/v2/summary.planner.md"));
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
    public async Task Renders_ModuleTree_ActionsAndModifiers()
    {
        await using var app = TestApp.Create("/app");
        var outp = await Render(app);

        // module headers + action names (the tree, no flat grouping)
        await Assert.That(outp).Contains("## file");
        await Assert.That(outp).Contains("- `file.read`");
        await Assert.That(outp).Contains("- `variable.set`");

        // the Modifiers section lists modifiers under their module — structurally, not by an IsModifier flag
        await Assert.That(outp).Contains("# Modifiers");
        await Assert.That(outp).Contains("- `cache.wrap`");

        // a modifier must NOT appear in the actions tree (before the # Modifiers heading)
        var modIdx = outp.IndexOf("# Modifiers", StringComparison.Ordinal);
        await Assert.That(outp.Substring(0, modIdx)).DoesNotContain("cache.wrap");
    }
}
