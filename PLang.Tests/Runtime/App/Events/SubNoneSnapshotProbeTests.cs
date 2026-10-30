using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.Events;

// Security probe (event-action, item 3): an event bound with Sub=none must never reach a sub app. A sub app's identity is its maker's, so
// a snapshot the maker took verifies in the sub app. Resumed there, does its saved Sub=none event bind onto the sub app's actor?
[NotInParallel]
public class SubNoneSnapshotProbeTests
{
    private string _dir = null!;

    [Before(Test)]
    public void Setup()
    {
        _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_probe_subnone_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_dir, "sub"));
    }

    [After(Test)]
    public void Teardown()
    {
        try { System.IO.Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private static System.Threading.Tasks.ValueTask<global::app.data.@this> Read(string variable, global::app.actor.context.@this context)
        => new global::app.type.item.variable.@this(variable.Trim('%'), context.App.type.list).Start(context);

    // the events the sub app's user holds that a program bound (the os's system events, taken in too, left out); a read
    // that fails is a failed test, never a count
    private static async Task<int> Count(global::app.@this app)
    {
        var context = app.actor.list.User.Context;
        var read = await Read("%!app.event.list%", context);
        await read.IsSuccess();
        return (read.Peek() as global::app.type.item.list.@this<global::app.@event.@this>)!.Items(context)
            .Count(row => row.Peek() is global::app.@event.@this @event && @event.action?.Step?.Goal.IsSystem != true);
    }

    private async Task<(int before, int after, string? resumed)> Run(string sub)
    {
        await using var maker = new global::app.@this(_dir).Consenting();
        var ctx = maker.actor.list.User.Context;
        var log = Make.Goal(ctx, "Log", Make.Step("set x", Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "x", "variable"), ("Value", "1"))));
        maker.goal.list.Add(log);
        await (await Make.Action(ctx, "on", "event", ("Event", "%!app.type.goal.on.start%"), ("When", "before"),
            ("Action", Make.Call(ctx, "Log")), ("Sub", sub)).Start(ctx)).IsSuccess();
        var snap = await maker.Snapshot(ctx);
        await snap.IsSuccess();

        await using var child = new global::app.@this(System.IO.Path.Combine(_dir, "sub"), maker.actor.list.User).Consenting();
        var childCtx = child.actor.list.User.Context;
        var before = await Count(child);
        await childCtx.Variable.Set("snap", snap);
        var resumed = await (await childCtx.Action("snapshot.resume(Snapshot=%snap%)")).Start(childCtx);
        return (before, await Count(child), resumed.Error?.Key);
    }

    [Test]
    public async Task ASubNoneEvent_IsNotInTheSubApp_AndAResumeOfTheMakersSnapshotThereDoesNotBindIt()
    {
        var (before, after, resumed) = await Run("none");
        await TestContext.Current!.OutputWriter.WriteLineAsync($"P8e Sub=none: sub events before resume={before} after={after} resume={resumed}");
        await Assert.That(before).IsEqualTo(0);
        // the maker's snapshot is refused in the sub app, so it binds nothing there
        await Assert.That(resumed).IsEqualTo("SnapshotAppMismatch");
        await Assert.That(after).IsEqualTo(0);
    }

    // control: Sub=inherit is in the sub app from its first ask
    [Test]
    public async Task Control_ASubInheritEvent_IsInTheSubApp()
    {
        var (before, after, resumed) = await Run("inherit");
        await TestContext.Current!.OutputWriter.WriteLineAsync($"P8e Sub=inherit: sub events before resume={before} after={after} resume={resumed}");
        await Assert.That(before).IsEqualTo(1);
        await Assert.That(resumed).IsEqualTo("SnapshotAppMismatch");
        await Assert.That(after).IsEqualTo(1);
    }
}
