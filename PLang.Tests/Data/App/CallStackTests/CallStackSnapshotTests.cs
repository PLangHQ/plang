using ActionEntity = app.goal.step.action.@this;

namespace PLang.Tests.App.CallStackTests;

public class CallStackSnapshotTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private (Goal goal, Step step, ActionEntity action) MakeFrame(
        string goalName, string stepText = "step", string module = "test", string actionName = "test")
    {
        var goal = new Goal { Name = goalName, Path = global::app.type.item.path.@this.Resolve($"/{goalName}.goal", app.actor.list.User.Context) };
        var step = new Step { Index = 0, Text = stepText, Goal = goal };
        var action = new ActionEntity { Module = app.actor.list.User.Context.App.Module(module), Name = actionName, Step = step };
        step.Code.Add(action);
        goal.Step.Add(step);
        return (goal, step, action);
    }

    private global::app.@this BuildAppWithGoals(params Goal[] goals)
    {
        var app = new global::app.@this("/test").Testing();
        foreach (var g in goals) app.goal.list.Add(g);
        return app;
    }

    [Test]
    public async Task CallStack_Capture_WalksActiveFrameChain_OuterToBottom()
    {
        var (g1, _, a1) = MakeFrame("Outer");
        var (g2, _, a2) = MakeFrame("Inner");
        var app = BuildAppWithGoals(g1, g2);

        var stack = app.actor.list.User.Context.call;
        await using var outer = stack.Push(a1);
        await using var inner = stack.Push(a2);

        var section = new Snapshot(app.actor.list.User.Context);
        stack.Capture(section);

        var frames = await section.Frames("frames")!;
        await Assert.That(frames.Count).IsEqualTo(2);
        // Outer first → bottom (inner) last.
        await Assert.That(await frames[0].Text("goalPrPath")).IsEqualTo(g1.PrPath?.ToString());
        await Assert.That(await frames[1].Text("goalPrPath")).IsEqualTo(g2.PrPath?.ToString());
    }

    [Test]
    public async Task CallStack_Capture_DropsCompletedChildren_AsHistoryNotState()
    {
        var (g1, _, a1) = MakeFrame("Parent");
        var (g2, _, a2) = MakeFrame("CompletedChild");
        var app = BuildAppWithGoals(g1, g2);
        var stack = app.actor.list.User.Context.call;
        // Turn History on so completed children stay in the tree — we'll assert the snapshot
        // still excludes them because they're not on the *active* chain.
        stack.Setting.History = true;

        await using (var parent = stack.Push(a1))
        {
            await using (var child = stack.Push(a2)) { /* completes here */ }
            var section = new Snapshot(app.actor.list.User.Context);
            stack.Capture(section);
            var frames = await section.Frames("frames")!;
            await Assert.That(frames.Count).IsEqualTo(1);
            await Assert.That(await frames[0].Text("goalPrPath")).IsEqualTo(g1.PrPath?.ToString());
        }
    }

    [Test]
    public async Task CallStack_Restore_RebuildsChain_BottomFrameIsResumePoint()
    {
        // Build src with 2 frames; capture; replay onto dst that has matching goals.
        var (g1, _, a1) = MakeFrame("Outer2");
        var (g2, _, a2) = MakeFrame("Inner2");
        var src = BuildAppWithGoals(g1, g2);

        await using (var outer = src.actor.list.User.Context.call.Push(a1))
        await using (var inner = src.actor.list.User.Context.call.Push(a2))
        {
            var snap = src.Snapshot(src.actor.list.User.Context);

            // Build dst with matching goals (same Path + Hash via identical step text).
            var (dg1, _, _) = MakeFrame("Outer2");
            var (dg2, _, _) = MakeFrame("Inner2");
            var dst = BuildAppWithGoals(dg1, dg2);

            await dst.Restore(snap, dst.actor.list.User.Context);

            var chain = dst.actor.list.User.Context.call.RestoredChain!;
            await Assert.That(chain.Count).IsEqualTo(2);
            await Assert.That(chain[0].Goal.PrPath?.ToString()).IsEqualTo(g1.PrPath?.ToString());
            await Assert.That(chain[^1].Goal.PrPath?.ToString()).IsEqualTo(g2.PrPath?.ToString());

            await Assert.That(dst.actor.list.User.Context.call.BottomFrame).IsNotNull();
            await Assert.That(dst.actor.list.User.Context.call.BottomFrame!.Goal.PrPath?.ToString()).IsEqualTo(g2.PrPath?.ToString());
        }
    }

    [Test]
    public async Task CallStack_RoundTrip_InsideGoalAndStepFrames_KeepsTheActionPositions()
    {
        // A real run's chain: goal → step → action (a call) → goal → step → action. Only the actions are
        // resume points; the goal and step frames between them add nothing to the snapshot.
        var (g1, s1, a1) = MakeFrame("FrOuter");
        var (g2, s2, a2) = MakeFrame("FrInner");
        var src = BuildAppWithGoals(g1, g2);
        var stack = src.actor.list.User.Context.call;

        await using (stack.Push(g1))
        await using (stack.Push(s1))
        await using (stack.Push(a1))
        await using (stack.Push(g2))
        await using (stack.Push(s2))
        await using (stack.Push(a2))
        {
            var snap = src.Snapshot(src.actor.list.User.Context);

            var (dg1, _, _) = MakeFrame("FrOuter");
            var (dg2, _, _) = MakeFrame("FrInner");
            var dst = BuildAppWithGoals(dg1, dg2);
            await dst.Restore(snap, dst.actor.list.User.Context);

            var chain = dst.actor.list.User.Context.call.RestoredChain!;
            await Assert.That(chain.Count).IsEqualTo(2);
            await Assert.That(chain[0].Goal.Name).IsEqualTo("FrOuter");
            await Assert.That(chain[^1].Goal.Name).IsEqualTo("FrInner");
            await Assert.That(chain[^1].StepIndex).IsEqualTo(s2.Index);
            await Assert.That(chain[^1].ActionIndex).IsEqualTo(0);
        }
    }

    [Test]
    public async Task CallStack_BottomFrame_FromAStepFrame_IsTheNearestActionOutward()
    {
        // Inside a callee's step, before its first action: the resume point is the call that led here.
        var (g1, s1, a1) = MakeFrame("BfOuter");
        var (g2, s2, _) = MakeFrame("BfInner");
        var app = BuildAppWithGoals(g1, g2);
        var stack = app.actor.list.User.Context.call;

        await using var goal = stack.Push(g1);
        await using var step = stack.Push(s1);
        await using var call = stack.Push(a1);
        await using var callee = stack.Push(g2);
        await using var calleeStep = stack.Push(s2);

        await Assert.That(stack.BottomFrame!.Action).IsSameReferenceAs(a1);
    }

    [Test]
    public async Task CallStack_BottomFrame_IdentifiesThrowingCall()
    {
        // On a *live* CallStack, BottomFrame is the deepest active frame.
        var (g1, _, a1) = MakeFrame("LBOuter");
        var (g2, _, a2) = MakeFrame("LBInner");
        var app = BuildAppWithGoals(g1, g2);
        var stack = app.actor.list.User.Context.call;
        await using var outer = stack.Push(a1);
        await using var inner = stack.Push(a2);

        var bottom = stack.BottomFrame;
        await Assert.That(bottom).IsNotNull();
        await Assert.That(bottom!.Action).IsSameReferenceAs(a2);
    }
}
