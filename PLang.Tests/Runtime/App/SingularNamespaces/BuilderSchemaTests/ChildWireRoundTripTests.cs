using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>
/// The singular-wire round-trip: a goal serializes through <c>goal.Output</c> (singular keys
/// <c>step</c>/<c>action</c>/<c>name</c>/<c>child</c>, no <c>indent</c>) and reads back through the
/// three serializer readers. Pins that a condition action's branch body (<c>Child</c>) survives the
/// write→read cycle — the runtime tree is what the rebuilt <c>.pr</c> will carry.
/// </summary>
public class ChildWireRoundTripTests
{
    [Test]
    public async Task ConditionChild_SurvivesOutputReadRoundTrip()
    {
        await using var app = TestApp.Create("/test");
        var context = app.System.Context;

        // A condition step with an indented body, folded so the body lives on the gate action's Child.
        var goal = Make.Goal("G",
            Make.Step("if %x% = 1", Make.Action("condition", "if", ("Left", "%x%"), ("Operator", "="), ("Right", 1))),
            Make.Step("write out inside", 1, Make.Action("output", "write", ("Content", "inside"))));

        var fold = new global::app.module.action.build.fold(context) { Goal = context.Ok<global::app.goal.@this>(goal) };
        var folded = await new global::app.module.action.build.code.Default().Fold(fold);
        await Assert.That(folded.Success).IsTrue();

        var loaded = await RealGoalLoad.ViaChannel(app, goal);

        // Top level: only the condition step; the body rode into the gate action's Child.
        await Assert.That(loaded.Step.Count).IsEqualTo(1);
        var cond = loaded.Step[0].Action[0];
        await Assert.That(cond.Module).IsEqualTo("condition");
        await Assert.That(cond.ActionName).IsEqualTo("if");
        await Assert.That(cond.Child.Count).IsEqualTo(1);
        await Assert.That(cond.Child[0].Text).IsEqualTo("write out inside");
        await Assert.That(cond.Child[0].Action[0].Module).IsEqualTo("output");
        await Assert.That(cond.Child[0].Action[0].ActionName).IsEqualTo("write");
    }
}
