using Render = global::app.module.action.ui.Render;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>
/// Two doors, kept distinct. A compiled program value (a step's actions) is shown to the LLM by
/// EMBEDDING it through the `store` filter — the value drives its own Store writer, so an authored
/// %ref% ("Hello %name%") rides out literally: %name% is a runtime variable, unset at build, and it
/// must NOT resolve. The bare `{{ p.Value }}` is the resolve door — navigating into an authored leaf
/// executes it, which throws on the unset ref. The filter never member-accesses the leaf; it
/// serializes the container subtree, so it sidesteps the throw entirely.
/// </summary>
public class RenderStoreViewTests
{
    private static async Task<(bool ok, string? err, string outp)> Render(app.@this app, string template)
    {
        var ctx = app.System.Context;
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
    public async Task StoreFilter_EmbedsAuthoredWire_NoResolve()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test");
        var r = await Render(app, "{{ goal.Step[0].Action | store }}");
        await Assert.That(r.ok).IsTrue();                    // container embed never touches the leaf's resolve door
        await Assert.That(r.outp).Contains("Hello %name%");  // authored %ref%, preserved by the Store writer
        await Assert.That(r.outp).Contains("\"module\": \"output\"");   // the real .pr action wire, embedded whole
    }

    [Test]
    public async Task ResolveDoor_StillThrowsOnUnsetRef()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test");
        // {{ p.Value }} navigates INTO the authored leaf → executes it → the unset %name% throws.
        // The two doors stay distinct: embedding is raw, member access resolves.
        var r = await Render(app,
            "{% for a in goal.Step[0].Action %}{% for p in a.Parameter %}{{ p.Value }}{% endfor %}{% endfor %}");
        await Assert.That(r.ok).IsFalse();
    }
}
