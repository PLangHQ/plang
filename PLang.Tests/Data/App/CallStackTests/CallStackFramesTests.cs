using ActionEntity = app.goal.step.action.@this;

namespace PLang.Tests.App.CallStackTests;

// A call stack in a snapshot is its frames, from the root to the frame the run stopped in, each written as what it keeps;
// through text they come back in order, each under the one before it.
public class CallStackFramesTests
{
    private static (Goal goal, Step step, ActionEntity action) MakeFrame(global::app.@this app, string goalName, string stepText = "step")
    {
        var goal = new Goal(app.type.list) { Name = goalName, Path = global::app.type.item.path.@this.Resolve($"/{goalName}.goal", app.actor.list.User.Context) };
        var step = new Step(app.type.list) { Index = 0, Text = stepText, Goal = goal };
        var action = new ActionEntity(app.type.list) { Module = app.Module("test"), Name = "test", Step = step };
        step.Code.Add(action);
        goal.Step.Add(step);
        app.goal.list.Add(goal);
        return (goal, step, action);
    }

    private static global::app.@this App() => new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();

    [Test]
    public async Task TheStack_IsWrittenAsItsFrames_NoTreeNoCurrentNoIds()
    {
        await using var src = App();
        var (_, _, a1) = MakeFrame(src, "Outer");
        var (_, _, a2) = MakeFrame(src, "Inner");
        var context = src.actor.list.User.Context;

        await using var outer = context.call.Push(a1, await context.call.Setting());
        await using var inner = context.call.Push(a2, await context.call.Setting());
        var text = await src.Snapshot(context).Saved().Text(context);

        await Assert.That(text).Contains("\"frames\"");
        foreach (var gone in new[] { "\"root\"", "\"children\"", "\"current\"", outer.Id.ToString()!, inner.Id.ToString()! })
            await Assert.That(text).DoesNotContain(gone);
    }

    [Test]
    public async Task TheFrames_ComeBackInOrder_EachUnderTheOneBeforeIt()
    {
        await using var src = App();
        var (_, _, a1) = MakeFrame(src, "Outer");
        var (_, _, a2) = MakeFrame(src, "Inner");
        var context = src.actor.list.User.Context;
        string text;
        await using (context.call.Push(a1, await context.call.Setting()))
        await using (context.call.Push(a2, await context.call.Setting()))
            text = await src.Snapshot(context).Saved().Text(context);

        await using var dst = App();
        var (_, _, da1) = MakeFrame(dst, "Outer");
        var (_, _, da2) = MakeFrame(dst, "Inner");
        var into = dst.actor.list.User.Context;
        await dst.Restore(await into.Snapshot(text), into);

        var root = into.call.Root!;
        await Assert.That(root.Action).IsSameReferenceAs(da1);
        var child = root.Children.Single();
        await Assert.That(child.Caller).IsSameReferenceAs(root);
        await Assert.That(child.Action).IsSameReferenceAs(da2);
    }

    [Test]
    public async Task ACompletedChild_IsHistory_NotAmongTheFrames()
    {
        await using var src = App();
        var (_, _, a1) = MakeFrame(src, "Parent");
        var (_, _, a2) = MakeFrame(src, "CompletedChild");
        var context = src.actor.list.User.Context;
        // history keeps the completed child among its caller's children; the snapshot keeps only the live chain
        context.Setting.Set("app.call.setting.history", true);

        await using var parent = context.call.Push(a1, await context.call.Setting());
        await using (context.call.Push(a2, await context.call.Setting())) { }
        var text = await src.Snapshot(context).Saved().Text(context);

        await Assert.That(text).Contains("Parent");
        await Assert.That(text).DoesNotContain("CompletedChild");
    }

    [Test]
    public async Task WhatAProgramAddedToItsCall_ComesBack()
    {
        // %!call.scope.retries%: kept on the goal's frame as long as the call lives, and through a restore
        await using var src = App();
        var (g1, _, _) = MakeFrame(src, "ScopeGoal");
        var context = src.actor.list.User.Context;
        string text;
        await using (var frame = context.call.Push(g1, await context.call.Setting()))
        {
            frame.property.Add("retries", new global::app.data.@this("retries", 3L, context: context));
            text = await src.Snapshot(context).Saved().Text(context);
        }

        await using var dst = App();
        MakeFrame(dst, "ScopeGoal");
        var into = dst.actor.list.User.Context;
        await dst.Restore(await into.Snapshot(text), into);

        var retries = into.call.Root!.property["retries"];
        await Assert.That(retries).IsNotNull();
        await Assert.That((await retries!.Value())?.ToString()).IsEqualTo("3");
    }

    [Test]
    public async Task AFramesOwnVariables_ComeBackByTheirNames()
    {
        await using var src = App();
        var (g1, _, _) = MakeFrame(src, "VarGoal");
        var context = src.actor.list.User.Context;
        string text;
        await using (context.call.Push(g1, await context.call.Setting(), [new global::app.data.@this("who", "ingi", context: context)]))
            text = await src.Snapshot(context).Saved().Text(context);

        await using var dst = App();
        MakeFrame(dst, "VarGoal");
        var into = dst.actor.list.User.Context;
        await dst.Restore(await into.Snapshot(text), into);

        var root = into.call.Root!;
        await Assert.That(root.TryGet("who", out var who)).IsTrue();
        await Assert.That((await who.Value())?.ToString()).IsEqualTo("ingi");
        await Assert.That(root.Arguments.Contains("who")).IsTrue();
    }

    // A goal kept in what a program added to its call, in an app that must load that goal from its .pr: the frame's
    // own goal is held, so only the kept one needs the load. Red while the snapshot view's load reaches only a frame's
    // found members (goal, step, action), not a value kept inside another member.
    [Test]
    public async Task AGoalKeptInWhatAProgramAdded_IsLoaded_InAnAppThatMustLoadIt()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-kept-" + System.Guid.NewGuid().ToString("N")[..8]);
        await using var src = new global::app.@this(folder, global::PLang.Tests.Parent.Actor).Testing();
        var (frameGoal, _, _) = MakeFrame(src, "FrameGoal");
        var (kept, _, _) = MakeFrame(src, "KeptGoal");
        var context = src.actor.list.User.Context;
        string text;
        await using (var frame = context.call.Push(frameGoal, await context.call.Setting()))
        {
            frame.property.Add("place", new global::app.data.@this("place", kept, context: context));
            text = await src.Snapshot(context).Saved().Text(context);
        }
        // only the kept goal is on disk, built
        var pr = System.IO.Path.Combine(folder, kept.Pr!.Path.Raw.TrimStart('/'));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(pr)!);
        System.IO.File.WriteAllText(pr, await context.Pr(kept));

        await using var dst = new global::app.@this(folder, global::PLang.Tests.Parent.Actor).Testing();
        MakeFrame(dst, "FrameGoal");
        var into = dst.actor.list.User.Context;
        await dst.Restore(await into.Snapshot(text), into);

        var place = into.call.Root!.property["place"];
        await Assert.That(place).IsNotNull();
        await Assert.That((await place!.Value<Goal>())?.Name).IsEqualTo("KeptGoal");
    }

    // A frame's parameters are its own variables, not a copy of them: a goal called with order=… that sets %order% reads
    // the new value through %!call.scope.parameter.order%.
    [Test]
    public async Task AParameterSetInItsGoal_IsWhatTheFramesParameterReads()
    {
        await using var app = App();
        var (goal, _, _) = MakeFrame(app, "ParamGoal");
        var context = app.actor.list.User.Context;
        await using var frame = context.call.Push(goal, await context.call.Setting(), [new global::app.data.@this("order", "first", context: context)]);

        await (await context.Variable.Set("order", "second")).IsSuccess();

        var parameter = new global::app.type.item.variable.parser.@this("%!call.scope.parameter.order%", app.type.list).Whole!;
        var order = await parameter.Start(context);
        await Assert.That((await order.Value())?.ToString()).IsEqualTo("second");
    }

    // An error first placed where a frame observed it (Record) is in that frame's step, so in its goal: %!error.goal%.
    [Test]
    public async Task AnErrorPlacedByAFrame_IsInItsStepsGoal()
    {
        await using var app = App();
        var (goal, _, action) = MakeFrame(app, "ErrGoal");
        var context = app.actor.list.User.Context;
        await using var frame = context.call.Push(action, await context.call.Setting());
        var error = new global::app.error.@this("it broke", "Broke");

        frame.Record(error, context);

        await Assert.That(error.Goal).IsSameReferenceAs(goal);
    }

    // An isolated frame keeps every write made under it, so it holds names it wasn't born with: they come back as
    // written there, never as names its runner supplied.
    [Test]
    public async Task AnIsolatedFramesWrite_ComesBack_NotAsANameItWasBornWith()
    {
        await using var src = App();
        var (_, _, a1) = MakeFrame(src, "IsoGoal");
        var context = src.actor.list.User.Context;
        string text;
        await using (context.call.Push(a1, await context.call.Setting()))
        await using (context.call.Isolate([new global::app.data.@this("city", "Reykjavik", context: context)], await context.call.Setting()))
        {
            await context.Variable.Set("written", "here");
            text = await src.Snapshot(context).Saved().Text(context);
        }

        await using var dst = App();
        MakeFrame(dst, "IsoGoal");
        var into = dst.actor.list.User.Context;
        await dst.Restore(await into.Snapshot(text), into);

        var isolated = into.call.Root!.Children.Single();
        await Assert.That(isolated.TryGet("written", out _)).IsTrue();
        await Assert.That(isolated.Arguments.Contains("city")).IsTrue();
        await Assert.That(isolated.Arguments.Contains("written")).IsFalse();
    }

    [Test]
    public async Task ARestoreThatFails_LeavesNothingEnteredBehind()
    {
        await using var src = App();
        var (_, _, a1) = MakeFrame(src, "Kept");
        var (_, _, a2) = MakeFrame(src, "Changed");
        var context = src.actor.list.User.Context;
        string text;
        await using (context.call.Push(a1, await context.call.Setting()))
        await using (context.call.Push(a2, await context.call.Setting()))
            text = await src.Snapshot(context).Saved().Text(context);

        // the second frame's goal changed since the save: the first is entered, then the restore fails on the second
        await using var dst = App();
        MakeFrame(dst, "Kept");
        MakeFrame(dst, "Changed", "another step");
        var into = dst.actor.list.User.Context;
        var started = await (await into.Snapshot(text)).Start(dst, into);

        await Assert.That(started.Error?.Key).IsEqualTo("GoalHashMismatch");
        await Assert.That(into.call.Current).IsNull();
        await Assert.That(into.call.Root?.Children.Count ?? 0).IsEqualTo(0);
    }
}
