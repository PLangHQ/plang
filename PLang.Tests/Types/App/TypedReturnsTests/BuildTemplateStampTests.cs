using System.Linq;
using PLang.Tests.Shared;

namespace PLang.Tests.App.TypedReturnsTests;

/// <summary>
/// The builder is the ONE place a %ref% is detected: NormalizeParameterTypes stamps
/// type.template="plang" on a param whose value carries a %ref%, and leaves a literal
/// unflagged. This is the source of truth the .pr carries and read/render trust — no
/// runtime content re-detection. Driven via Make (real context-wired build objects).
/// </summary>
public class BuildTemplateStampTests
{
    [Test]
    public async Task RefParam_StampsTemplatePlang_AtBuild()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var ctx = app.User.Context;

        var goal = Make.Goal("G", "/g.goal",
            Make.Step("s", Make.Action("error", "throw", ("Message", "hello %name%"))));

        var actions = goal.Step[0].Action.Elements;  // bridge: dies with the Validate trilogy (NormalizeParameterTypes moves into action.Validate)
        global::app.module.action.build.code.Default.NormalizeParameterTypes(actions, app.Module, ctx);

        var msg = actions.First().Parameter.First(p => p.Name == "Message");
        await Assert.That(msg.Type?.Template).IsEqualTo("plang");
    }

    [Test]
    public async Task LiteralParam_StaysUnflagged_AtBuild()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var ctx = app.User.Context;

        var goal = Make.Goal("G", "/g.goal",
            Make.Step("s", Make.Action("error", "throw", ("Message", "hello world"))));

        var actions = goal.Step[0].Action.Elements;  // bridge: dies with the Validate trilogy (NormalizeParameterTypes moves into action.Validate)
        global::app.module.action.build.code.Default.NormalizeParameterTypes(actions, app.Module, ctx);

        var msg = actions.First().Parameter.First(p => p.Name == "Message");
        await Assert.That(msg.Type?.Template).IsNull();
    }

    [Test]
    public async Task EmbeddedRef_StampsTemplatePlang_AtBuild()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var ctx = app.User.Context;

        var goal = Make.Goal("G", "/g.goal",
            Make.Step("s", Make.Action("error", "throw", ("Message", "count is %n% today"))));

        var actions = goal.Step[0].Action.Elements;  // bridge: dies with the Validate trilogy (NormalizeParameterTypes moves into action.Validate)
        global::app.module.action.build.code.Default.NormalizeParameterTypes(actions, app.Module, ctx);

        var msg = actions.First().Parameter.First(p => p.Name == "Message");
        await Assert.That(msg.Type?.Template).IsEqualTo("plang");
    }
}
