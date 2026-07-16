using app.module.action.ui;

namespace PLang.Tests.App.Modules.Stage4Spike;

/// <summary>
/// 4d load-bearing check: a prose <c>file</c> door renders through Fluid the way the templates need —
/// <c>{{ module.Notes }}</c> emits the md CONTENT (the door awaits Value), and
/// <c>{% if module.Notes %}</c> is an EXISTENCE guard (an absent facet file is falsy, so the block is
/// omitted without a read). Proves the architect's "prose doors are file handles" ruling holds in the
/// real Fluid provider before the template rewrite leans on it.
/// </summary>
public class ProseDoorFluidTests
{
    private const string FixtureModule = "fixturemod";

    private sealed class FixtureAction { }

    private static async Task<string> Render(global::app.@this app, string template)
    {
        var ctx = app.System.Context;
        ctx.Variable.Set(new Data("m", app.Module[FixtureModule], context: ctx));
        var action = new Render(ctx)
        {
            Template = (global::app.type.item.text.@this)template,
            IsFile = (global::app.type.item.@bool.@this)false,
        };
        var result = await new global::app.module.action.ui.code.Fluid().Render(action);
        await result.IsSuccess();
        return (await result.Value())?.ToString() ?? "";
    }

    private static global::app.@this Stage(string tempDir, params (string file, string body)[] prose)
    {
        var mdRoot = System.IO.Path.Combine(tempDir, "mdroot");
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(mdRoot, FixtureModule));
        foreach (var (file, body) in prose)
            System.IO.File.WriteAllText(System.IO.Path.Combine(mdRoot, FixtureModule, file), body);

        var app = global::PLang.Tests.TestApp.Create(tempDir);
        app.Module.MarkdownTeachingRoot = mdRoot;
        app.Module.RegisterType(FixtureModule, "setvalue", typeof(FixtureAction));
        return app;
    }

    [Test]
    public async Task PresentProse_RendersContent_AndGuardIsTrue()
    {
        await using var app = Stage("/tmp/prose-fluid-1", ("module.notes.md", "The module rule."));
        var outp = await Render(app, "{% if m.Notes %}[{{ m.Notes }}]{% endif %}");
        await Assert.That(outp).IsEqualTo("[The module rule.]");
    }

    [Test]
    public async Task AbsentProse_GuardIsFalse_BlockOmitted()
    {
        // notes staged, examples NOT — the existence guard omits the examples block without reading.
        await using var app = Stage("/tmp/prose-fluid-2", ("module.notes.md", "rule"));
        var outp = await Render(app, "N:{% if m.Notes %}yes{% endif %} E:{% if m.Examples %}yes{% endif %}");
        await Assert.That(outp).IsEqualTo("N:yes E:");
    }
}
