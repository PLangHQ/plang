namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>
/// A step's pre-filled formal line puts each certain action in its place: the test a lone if is asked of (an action
/// that answers a bool) before the if, so the if reads its answer; what the if runs inside its body; a chain flat.
/// </summary>
public class PrefillTests
{
    // The step's line once the decider marked these actions certain.
    private static async Task<string?> Line(string text, params string[] certain)
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var context = app.actor.list.System.Context;
        var goal = Make.Goal(app.actor.list.User.Context, "G", Make.Step(text));
        var answer = new Dictionary<string, object?>();
        foreach (var action in certain)
            answer[$"s0_{action}"] = new Dictionary<string, object?> { ["type"] = "noul", ["noul"] = 0.99 };
        foreach (var step in goal.Step.Items()) await step.Pick.Take(Make.Dict(answer, context), [], context);
        return goal.Step[0].Pick.Formal;
    }

    [Test]
    public async Task ATestTheIfAsks_LeadsTheIf()
    {
        var line = await Line("if %items% contains \"milk\", write out \"got milk\"", "condition.if", "list.contains", "output.write");
        await Assert.That(line).StartsWith("list.contains(");
        await Assert.That(line).Contains("; condition.if(");
        await Assert.That(line).Contains("{ output.write(");
    }

    [Test]
    public async Task AnExistenceQuestion_LeadsTheIf()
    {
        var line = await Line("if 'list.json' exists, write out \"there\"", "condition.if", "file.exists", "output.write");
        await Assert.That(line).StartsWith("file.exists(");
        await Assert.That(line).Contains("; condition.if(");
        await Assert.That(line).Contains("{ output.write(");
    }

    // a save answers its path too, but it is no question: it is what the if runs
    [Test]
    public async Task ASaveTheIfRuns_StaysInItsBody()
    {
        var line = await Line("if %x% > 5, save %data% to 'a.txt'", "condition.if", "file.save");
        await Assert.That(line).StartsWith("condition.if(");
        await Assert.That(line).Contains("{ file.save(");
    }

    [Test]
    public async Task WhatTheIfRuns_StaysInItsBody()
    {
        var line = await Line("if %x% > 5, read 'a.txt'", "condition.if", "file.read");
        await Assert.That(line).StartsWith("condition.if(");
        await Assert.That(line).Contains("{ file.read(");
    }

    [Test]
    public async Task AChain_StaysFlat()
    {
        var line = await Line("if %items% contains \"milk\", write out \"yes\", else write out \"no\"",
            "condition.if", "condition.else", "list.contains", "output.write");
        await Assert.That(line).StartsWith("condition.if(");
        await Assert.That(line).DoesNotContain("{");
    }
}
