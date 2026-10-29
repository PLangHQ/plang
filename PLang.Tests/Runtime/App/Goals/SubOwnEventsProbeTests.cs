using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.Goals;

// Security probes (event-action): a maker's policy is `before each goal, call Check` and Check fails; a sub app (its own folder) is the
// maker's upload. Which of the SUB APP'S OWN events can still let its goal run past the maker's failing check?
public class SubOwnEventsProbeTests
{
    private static string Root() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-probe8b-" + Guid.NewGuid().ToString("N")[..8]);

    private static global::app.actor.context.@this Built => global::PLang.Tests.Parent.Actor.Context;

    private static async Task OnDisk(string root, string path, params Make.StepDef[] steps)
    {
        var goal = Make.Goal(Built, System.IO.Path.GetFileNameWithoutExtension(path), path, steps);
        var source = System.IO.Path.Combine(root, path.TrimStart('/'));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(source)!);
        await System.IO.File.WriteAllTextAsync(source, goal.Name + "\n");
        var pr = System.IO.Path.Combine(root, goal.Pr!.Raw.TrimStart('/'));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(pr)!);
        await System.IO.File.WriteAllTextAsync(pr, await Built.Pr(goal));
    }

    private static Make.StepDef Sets(string name, string value) => Make.Step($"set %{name}% = {value}",
        Make.Action(Built, "variable", "set", Make.Param(Built, "Name", name, "variable"), ("Value", value)));

    private static async Task<global::app.goal.@this> Load(global::app.@this app, string name)
        => (await (await app.goal.list.Find(name)).Value())!;

    // the sub app's own Start binds `bind` (an event path, a side) to its Swallow goal, then Work runs; returns (worked, work's error key)
    // checkOn: what the maker's Check is bound on (each goal by default)
    private static async Task<(string? worked, string? error)> Run(string? subEvent, string? subWhen, bool asSystemEvent = false, string ownAt = "/Own.goal",
        string checkOn = "%!app.type.goal.on.start%")
    {
        var root = Root();
        await using var platform = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await OnDisk(root, "/Check.goal", Make.Step("throw", Make.Action(Built, "error", "throw", ("Message", "denied by the platform"), ("Key", "Denied"))));
        await OnDisk(root, "/Start.goal", Make.Step("before each goal, call Check", Make.Action(Built, "on", "event",
            ("Event", checkOn), ("When", "before"), ("Action", Make.Call(Built, "/Check")))));
        await (await (await Load(platform, "/Start")).Start(platform.actor.list.User.Context)).IsSuccess();

        var uploaded = System.IO.Path.Combine(root, "uploaded");
        await OnDisk(uploaded, "/Swallow.goal", Sets("swallowed", "yes"));
        await OnDisk(uploaded, "/Work.goal", Sets("worked", "yes"));
        if (subEvent != null)
            await OnDisk(uploaded, asSystemEvent ? "/system/event/Own.goal" : ownAt, Make.Step("bind", Make.Action(Built, "on", "event",
                ("Event", subEvent), ("When", subWhen!), ("Action", Make.Call(Built, "/Swallow")))));
        await using var sub = new global::app.@this(uploaded, platform.actor.list.User).Testing();
        var context = sub.actor.list.User.Context;
        if (subEvent != null && !asSystemEvent) await (await Load(sub, ownAt.Replace(".goal", ""))).Start(context);
        var ran = await (await Load(sub, "/Work")).Start(context);
        // the control: the sub app's own event did run (it set `swallowed` in the context it ran in)
        await TestContext.Current!.OutputWriter.WriteLineAsync($"P8b control swallowed={(await context.Variable.GetValue("swallowed"))?.ToString()}");
        return ((await context.Variable.GetValue("worked"))?.ToString(), ran.Error?.Key);
    }

    private static Task Show(string label, (string? worked, string? error) r)
        => TestContext.Current!.OutputWriter.WriteLineAsync($"P8b {label}: worked={r.worked} error={r.error}");

    // baseline: the maker's check stops the upload's goal
    [Test] public async Task A_Baseline_TheMakersFailingCheck_StopsTheUploadsGoal()
    {
        var r = await Run(null, null);
        await Show("A", r);
        await Assert.That(r.worked).IsNull();
    }

    // the sub app binds its own `on error` for each action and takes the check's failure
    [Test] public async Task B_ASubsOwnOnErrorEvent_CannotTakeTheMakersChecksFailure()
    {
        var r = await Run("%!app.type.action.on.error%", "before");
        await Show("B", r);
        await Assert.That(r.worked).IsNull();
    }

    // the same events, bound by the sub app's own system/event/ file: those run as the actor when its events first load, BEFORE the maker's
    // events are taken in (events.Load: Run(...) then the inherited joins), so the sub app's are first in bind order
    [Test] public async Task B2_ASubsSystemEventFile_BindsAnOnErrorBeforeTheMakersChecksJoin()
    {
        var r = await Run("%!app.type.action.on.error%", "before", asSystemEvent: true);
        await Show("B2 (system/event file, on error)", r);
        await Assert.That(r.worked).IsNull();
    }

    // what survives an ordering fix: the sub app's own goal under system/ (a plang goal, so "each goal" events are skipped under
    // event.setting.goal app, the maker's check too) binds the on error event AFTER the maker's events are in
    [Test] public async Task B3_ASubsOwnSystemGoal_BindsAnOnErrorAfterTheMakersChecksJoined()
    {
        var r = await Run("%!app.type.action.on.error%", "before", ownAt: "/system/Own.goal");
        await Show("B3 (own goal under system/, called by the sub)", r);
        await Assert.That(r.worked).IsNull();
    }

    // a policy that denies only some goals: the maker checks the goals /Wor* names, so the sub app's own goal that binds
    // `on error` each action runs, and the check's failure inside Work is its maker's, never the sub app's to take
    [Test] public async Task B4_ASubsOwnOnErrorEvent_BoundByAGoalTheCheckLetsThrough_CannotTakeTheMakersChecksFailure()
    {
        var r = await Run("%!app.type.action.on.error%", "before", checkOn: "%!app.goal[\"/Wor*\"].on.start%");
        await Show("B4 (a check on /Wor* only; the sub's on error bound by /Own)", r);
        await Assert.That(r.error).IsEqualTo("Denied");
        await Assert.That(r.worked).IsNull();
    }

    // the plainest road: the upload's own goal lives under its system/ folder, where "each goal" events (the maker's check) are skipped
    [Test] public async Task E_AnUploadsGoalUnderItsSystemFolder_IsStillCheckedByTheMakersEachGoalEvent()
    {
        var root = Root();
        await using var platform = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await OnDisk(root, "/Check.goal", Make.Step("throw", Make.Action(Built, "error", "throw", ("Message", "denied by the platform"), ("Key", "Denied"))));
        await OnDisk(root, "/Start.goal", Make.Step("before each goal, call Check", Make.Action(Built, "on", "event",
            ("Event", "%!app.type.goal.on.start%"), ("When", "before"), ("Action", Make.Call(Built, "/Check")))));
        await (await (await Load(platform, "/Start")).Start(platform.actor.list.User.Context)).IsSuccess();
        var uploaded = System.IO.Path.Combine(root, "uploaded");
        await OnDisk(uploaded, "/Work.goal", Sets("worked", "yes"));
        await OnDisk(uploaded, "/system/Work.goal", Sets("workedUnderSystem", "yes"));
        await using var sub = new global::app.@this(uploaded, platform.actor.list.User).Testing();
        var context = sub.actor.list.User.Context;

        var plain = await (await Load(sub, "/Work")).Start(context);
        var underSystem = await (await Load(sub, "/system/Work")).Start(context);

        await TestContext.Current!.OutputWriter.WriteLineAsync($"P8b E: plain error={plain.Error?.Key}; system/ error={underSystem.Error?.Key} ran={(await context.Variable.GetValue("workedUnderSystem"))?.ToString()}");
        await Assert.That(plain.Error?.Key).IsEqualTo("Denied");
        await Assert.That(underSystem.Error?.Key).IsEqualTo("Denied");
    }

    [Test] public async Task C2_ASubsSystemEventFile_BindsABeforeEachGoalAheadOfTheMakersCheck()
    {
        var r = await Run("%!app.type.goal.on.start%", "before", asSystemEvent: true);
        await Show("C2 (system/event file, before each goal)", r);
        await Assert.That(r.worked).IsNull();
    }

    // the sub app binds its own `before each goal` that runs first on the same goals
    [Test] public async Task C_ASubsOwnBeforeEachGoal_CannotLetItPass()
    {
        var r = await Run("%!app.type.goal.on.start%", "before");
        await Show("C", r);
        await Assert.That(r.worked).IsNull();
    }

    // the sub app binds its own `after each goal` that would turn the failure into a success
    [Test] public async Task D_ASubsOwnAfterEachGoal_CannotClearTheFailure()
    {
        var r = await Run("%!app.type.goal.on.start%", "after");
        await Show("D", r);
        await Assert.That(r.worked).IsNull();
    }
}
