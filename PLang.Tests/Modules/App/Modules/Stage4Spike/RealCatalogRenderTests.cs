using app.module.action.ui;

namespace PLang.Tests.App.Modules.Stage4Spike;

// 4a/4b end-to-end: the REAL catalog (app.module.list → module elements → module.Actions →
// action elements) renders through the real Fluid provider via the PlangDoorStrategy. Proves
// the element surface + the Data.Get door carry the whole chain a builder template will walk.
public class RealCatalogRenderTests
{
    private static async Task<string> Render(global::app.@this app, string template)
    {
        var ctx = app.User.Context;
        ctx.Variable.Set(new Data("modules", app.Module.list, context: ctx));
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
    public async Task ModuleElements_Enumerate_ThroughFluid()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/tmp/s4-realcat-1");
        var outp = await Render(app, "{% for m in modules %}[{{ m.Name }}]{% endfor %}");
        await Assert.That(outp).Contains("[file]");
        await Assert.That(outp).Contains("[variable]");
    }

    [Test]
    public async Task ModuleActions_RenderTheirNames_ThroughFluid()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/tmp/s4-realcat-2");
        // module.Actions is the native list of action class-zoom elements; each action's Name
        // navigates through the Data.Get door.
        var outp = await Render(app,
            "{% for m in modules %}{% if m.Name == 'file' %}{% for a in m.Actions %}{{ a.Name }};{% endfor %}{% endif %}{% endfor %}");
        await Assert.That(outp).Contains("file.read");
    }

    [Test]
    public async Task ModuleActions_IsNativeList_Filterable()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/tmp/s4-realcat-3");
        var file = app.Module["file"];
        await Assert.That(file.Actions.CountRaw).IsGreaterThan(0);
    }
}
