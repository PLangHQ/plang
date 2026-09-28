using Render = global::app.module.action.ui.Render;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>
/// The bare `{{ p.Value }}` is the resolve door — navigating into an authored leaf executes it, which
/// throws on the unset ref.
/// </summary>
public class RenderStoreViewTests
{
    private static async Task<(bool ok, string? err, string outp)> Render(app.@this app, string template)
    {
        var ctx = app.actor.list.System.Context;
        var goal = Make.Goal("MyGoal",
            Make.Step("write out \"Hello %name%\"",
                Make.Action("output", "write", ("Data", "Hello %name%"))));
        ctx.Variable.Set(new global::app.data.@this("goal", goal, context: ctx));
        var action = new Render(ctx)
        {
            Template = (global::app.type.item.text.@this)template,
            IsFile = (global::app.type.item.@bool.@this)false,
        };
        var result = await new global::app.module.action.ui.code.Fluid().Render(action);
        var err = result.Error?.Message;                                   // capture BEFORE Value() (which re-fails on an errored Data)
        var outp = result.Success ? (await result.Value())?.ToString() ?? "" : "";
        return (result.Success, err, outp);
    }

    [Test]
    public async Task ResolveDoor_StillThrowsOnUnsetRef()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test");
        // {{ p.Value }} navigates INTO the authored leaf → executes it → the unset %name% throws.
        var r = await Render(app,
            "{% for a in goal.Step[0].Code %}{% for p in a.Property %}{{ p.Value }}{% endfor %}{% endfor %}");
        await Assert.That(r.ok).IsFalse();
    }
}
