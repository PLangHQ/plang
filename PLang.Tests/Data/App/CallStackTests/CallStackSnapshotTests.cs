using ActionEntity = app.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.CallStackTests;

public class CallStackSnapshotTests
{
    private static (Goal goal, Step step, ActionEntity action) MakeFrame(
        string goalName, string stepText = "step", string module = "test", string actionName = "test")
    {
        var goal = new Goal { Name = goalName, Path = global::app.type.path.@this.Resolve($"/{goalName}.goal", global::PLang.Tests.TestApp.SharedContext) };
        var step = new Step { Index = 0, Text = stepText, Goal = goal };
        var action = new ActionEntity { Module = module, ActionName = actionName };
        action.Step = step;
        step.Actions.Add(action);
        goal.Steps.Add(step);
        return (goal, step, action);
    }

    private static global::app.@this BuildAppWithGoals(params Goal[] goals)
    {
        var app = global::PLang.Tests.TestApp.Create("/test");
        foreach (var g in goals) { g.Steps.Context = app.User.Context; app.Goal.Add(g); }
        return app;
    }

    [Test]
    public async Task CallStack_Capture_WalksActiveFrameChain_OuterToBottom()
    {
        var (g1, _, a1) = MakeFrame("Outer");
        var (g2, _, a2) = MakeFrame("Inner");
        var app = BuildAppWithGoals(g1, g2);

        var stack = app.User.CallStack;
        await using var outer = stack.Push(a1);
        await using var inner = stack.Push(a2);

        var section = new Snapshot(global::PLang.Tests.TestApp.SharedContext);
        stack.Capture(section);

        var frames = section.Read<List<Snapshot>>("frames")!;
        await Assert.That(frames.Count).IsEqualTo(2);
        // Outer first → bottom (inner) last.
        await Assert.That(frames[0].Read<string>("goalPrPath")).IsEqualTo(g1.PrPath?.ToString());
        await Assert.That(frames[1].Read<string>("goalPrPath")).IsEqualTo(g2.PrPath?.ToString());
    }

    [Test]
    public async Task CallStack_Capture_DropsCompletedChildren_AsHistoryNotState()
    {
        var (g1, _, a1) = MakeFrame("Parent");
        var (g2, _, a2) = MakeFrame("CompletedChild");
        var app = BuildAppWithGoals(g1, g2);
        var stack = app.User.CallStack;
        // Turn History on so completed children stay in the tree — we'll assert the snapshot
        // still excludes them because they're not on the *active* chain.
        stack.History = true;

        await using (var parent = stack.Push(a1))
        {
            await using (var child = stack.Push(a2)) { /* completes here */ }
            var section = new Snapshot(global::PLang.Tests.TestApp.SharedContext);
            stack.Capture(section);
            var frames = section.Read<List<Snapshot>>("frames")!;
            await Assert.That(frames.Count).IsEqualTo(1);
            await Assert.That(frames[0].Read<string>("goalPrPath")).IsEqualTo(g1.PrPath?.ToString());
        }
    }

    [Test]
    public async Task CallStack_Restore_RebuildsChain_BottomFrameIsResumePoint()
    {
        // Build src with 2 frames; capture; replay onto dst that has matching goals.
        var (g1, _, a1) = MakeFrame("Outer2");
        var (g2, _, a2) = MakeFrame("Inner2");
        var src = BuildAppWithGoals(g1, g2);

        await using (var outer = src.User.CallStack.Push(a1))
        await using (var inner = src.User.CallStack.Push(a2))
        {
            var snap = src.Snapshot();

            // Build dst with matching goals (same Path + Hash via identical step text).
            var (dg1, _, _) = MakeFrame("Outer2");
            var (dg2, _, _) = MakeFrame("Inner2");
            var dst = BuildAppWithGoals(dg1, dg2);

            dst.Restore(snap, dst.User.Context);

            var chain = dst.User.CallStack.RestoredChain!;
            await Assert.That(chain.Count).IsEqualTo(2);
            await Assert.That(chain[0].Goal.PrPath).IsEqualTo(g1.PrPath?.ToString());
            await Assert.That(chain[^1].Goal.PrPath).IsEqualTo(g2.PrPath?.ToString());

            await Assert.That(dst.User.CallStack.BottomFrame).IsNotNull();
            await Assert.That(dst.User.CallStack.BottomFrame!.Goal.PrPath).IsEqualTo(g2.PrPath?.ToString());
        }
    }

    [Test]
    public async Task CallStack_BottomFrame_IdentifiesThrowingCall()
    {
        // On a *live* CallStack, BottomFrame is the deepest active frame.
        var (g1, _, a1) = MakeFrame("LBOuter");
        var (g2, _, a2) = MakeFrame("LBInner");
        var app = BuildAppWithGoals(g1, g2);
        var stack = app.User.CallStack;
        await using var outer = stack.Push(a1);
        await using var inner = stack.Push(a2);

        var bottom = stack.BottomFrame;
        await Assert.That(bottom).IsNotNull();
        await Assert.That(bottom!.Action).IsSameReferenceAs(a2);
    }
}
