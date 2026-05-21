using app.errors;
using ActionEntity = app.goals.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.Errors;

public class CallChainRendererTests
{
    private static ActionEntity Make(string goalName, string module = "test", int stepIndex = 0, int line = 1)
    {
        var goal = new Goal { Name = goalName, Path = $"/{goalName}.goal" };
        var step = new Step { Index = stepIndex, LineNumber = line, Text = "step", Goal = goal };
        var action = new ActionEntity { Module = module, ActionName = "act" };
        action.Step = step;
        return action;
    }

    [Test]
    public async Task Render_EmptyChain_ReturnsEmptyList()
    {
        var lines = CallChainRenderer.Render(Array.Empty<global::app.callstack.call.@this>());
        await Assert.That(lines.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Render_SingleFrame_ReturnsOneLineWithoutMultiplier()
    {
        var stack = new CallStack();
        await using var call = stack.Push(Make("Solo", line: 5));
        var lines = CallChainRenderer.Render(call.SnapshotChain());
        await Assert.That(lines.Count).IsEqualTo(1);
        await Assert.That(lines[0]).IsEqualTo("Solo - /Solo.goal:5");
    }

    [Test]
    public async Task Render_TwoDistinctFrames_NoCompression()
    {
        var stack = new CallStack();
        await using var outer = stack.Push(Make("Outer", line: 1));
        await using var inner = stack.Push(Make("Inner", line: 7));
        var lines = CallChainRenderer.Render(inner.SnapshotChain());
        await Assert.That(lines.Count).IsEqualTo(2);
        await Assert.That(lines[0]).DoesNotContain("×");
        await Assert.That(lines[1]).DoesNotContain("×");
    }

    [Test]
    public async Task Render_FiveDeepRecursion_CompressesToOneLineWithCount()
    {
        // Same action pushed 5 times — same goal/step/module → must collapse.
        var action = Make("Recur", stepIndex: 2, line: 9);
        var stack = new CallStack();
        await using var c1 = stack.Push(action);
        await using var c2 = stack.Push(action);
        await using var c3 = stack.Push(action);
        await using var c4 = stack.Push(action);
        await using var c5 = stack.Push(action);
        var lines = CallChainRenderer.Render(c5.SnapshotChain());
        await Assert.That(lines.Count).IsEqualTo(1);
        await Assert.That(lines[0]).IsEqualTo("Recur ×5 - /Recur.goal:9");
    }

    [Test]
    public async Task Render_ErroredFrameInRecursion_BreaksCompression()
    {
        // Chain shape (from leaf outward): [Recur(err), Recur, Recur].
        // Failing leaf must stay alone; the two outer Recur frames still compress (×2).
        var action = Make("Recur", line: 4);
        var stack = new CallStack();
        await using var outerA = stack.Push(action);
        await using var outerB = stack.Push(action);
        await using var failing = stack.Push(action);
        failing.Errors.Add(new global::app.errors.Error("boom", "Crash", 500));

        var lines = CallChainRenderer.Render(failing.SnapshotChain());
        await Assert.That(lines.Count).IsEqualTo(2);
        await Assert.That(lines[0]).IsEqualTo("Recur - /Recur.goal:4");
        await Assert.That(lines[1]).IsEqualTo("Recur ×2 - /Recur.goal:4");
    }

    // Render_CauseBoundary_* / Render_DescendantsInsideRecovery_* deleted
    // in stage 2a.7 — they pinned the Cause-linkage feature which is gone
    // (Call.@this.Cause is always null post-2a.5; renderers no-op on it).
}
