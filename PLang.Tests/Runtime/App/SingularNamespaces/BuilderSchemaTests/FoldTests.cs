using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>The deterministic, build-time indent fold (<c>build.fold</c>): a deeper-indented block
/// after a condition step MOVES into that condition action's <c>Child</c>, recursively. Real steps
/// only — nothing synthesized. Indented under a non-condition is an authoring error (A4), never
/// silently dropped or kept flat.</summary>
public class FoldTests
{
    private static async Task<global::app.data.@this> Fold(Goal goal, global::app.actor.context.@this context)
    {
        var action = new global::app.module.action.build.fold(context) { Goal = context.Ok<Goal>(goal) };
        return await new global::app.module.action.build.code.Default().Fold(action);
    }

    [Test]
    public async Task Fold_IndentedBlockUnderCondition_BecomesChild()
    {
        await using var app = TestApp.Create("/test");
        var context = app.System.Context;

        var goal = Make.Goal("G",
            Make.Step("if %x% = 1", Make.Action("condition", "if", ("Left", "%x%"), ("Operator", "="), ("Right", 1))),
            Make.Step("write out inside", 1, Make.Action("output", "write", ("Content", "inside"))),
            Make.Step("write out after", Make.Action("output", "write", ("Content", "after"))));

        var result = await Fold(goal, context);
        await Assert.That(result.Success).IsTrue();

        // The indented step folds into the condition action's Child; top level keeps if + after.
        await Assert.That(goal.Step.Count).IsEqualTo(2);
        await Assert.That(goal.Step[0].Text).IsEqualTo("if %x% = 1");
        await Assert.That(goal.Step[1].Text).IsEqualTo("write out after");

        var cond = goal.Step[0].Action[0];
        await Assert.That(cond.IsCondition).IsTrue();
        await Assert.That(cond.Child.Count).IsEqualTo(1);
        await Assert.That(cond.Child[0].Text).IsEqualTo("write out inside");
    }

    [Test]
    public async Task Fold_NestedConditions_Recurse()
    {
        await using var app = TestApp.Create("/test");
        var context = app.System.Context;

        var goal = Make.Goal("G",
            Make.Step("if %x% = 1", Make.Action("condition", "if", ("Left", "%x%"), ("Operator", "="), ("Right", 1))),
            Make.Step("if %y% = 2", 1, Make.Action("condition", "if", ("Left", "%y%"), ("Operator", "="), ("Right", 2))),
            Make.Step("write out deep", 2, Make.Action("output", "write", ("Content", "deep"))));

        var result = await Fold(goal, context);
        await Assert.That(result.Success).IsTrue();

        await Assert.That(goal.Step.Count).IsEqualTo(1);
        var outer = goal.Step[0].Action[0];
        await Assert.That(outer.Child.Count).IsEqualTo(1);
        var inner = outer.Child[0].Action[0];
        await Assert.That(inner.IsCondition).IsTrue();
        await Assert.That(inner.Child.Count).IsEqualTo(1);
        await Assert.That(inner.Child[0].Text).IsEqualTo("write out deep");
    }

    [Test]
    public async Task Fold_IndentedUnderNonCondition_IsBuildError()
    {
        await using var app = TestApp.Create("/test");
        var context = app.System.Context;

        var goal = Make.Goal("G",
            Make.Step("do a thing", Make.Action("output", "write", ("Content", "a"))),
            Make.Step("nested but no condition", 1, Make.Action("output", "write", ("Content", "b"))));

        // No condition gate under the indented block → an authoring error, not a silent flat sibling.
        var result = await Fold(goal, context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("IndentUnderNonCondition");
    }
}
