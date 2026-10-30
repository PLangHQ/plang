using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.Goals;

// A call finds its goal in the app its caller goal is: a maker's goal running in a sub app calls the maker's goals, never
// the sub app's own of the same name; an os goal (the os's folder is no app's) calls in the running app, its own system/
// copy first; an app's own system/ goal replaces the os's. Each goal is read from its .pr by its app's own list.
public class CallFindsItsAppTests
{
    private static string Root() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-callapp-" + Guid.NewGuid().ToString("N")[..8]);

    private static global::app.actor.context.@this Built => global::PLang.Tests.Parent.Actor.Context;

    // `path` in `root` with its .pr beside it, as a build leaves it
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

    private static Make.StepDef Records(string value) => Make.Step($"set %recorded% = {value}",
        Make.Action(Built, "variable", "set", Make.Param(Built, "Name", "recorded", "variable"), ("Value", value)));

    private static Make.StepDef Calls(string goal) => Make.Step($"call {goal}", Make.Call(Built, goal));

    private static async Task<global::app.goal.@this> Load(global::app.@this app, string name)
        => (await (await app.goal.list.Find(name)).Value())!;

    // the platform binds `before each goal, call <meter>` in its Start, Meter calls Record; an uploaded sub app in a
    // folder of its own has its own Record: a goal of the sub app runs the platform's Meter, which calls the platform's
    // Record
    private static async Task<string?> Metered(string meter)
    {
        var root = Root();
        await using var platform = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await OnDisk(root, meter + ".goal", Calls("Record"));
        await OnDisk(root, "/Record.goal", Records("platform"));
        await OnDisk(root, "/Start.goal", Make.Step("before each goal, call Meter", Make.Action(Built, "on", "event",
            ("Event", "%!app.type.goal.on.start%"), ("When", "before"), ("Action", Make.Call(Built, meter)))));
        await (await (await Load(platform, "/Start")).Start(platform.actor.list.User.Context)).IsSuccess();

        var uploaded = System.IO.Path.Combine(root, "uploaded");
        await using var sub = new global::app.@this(uploaded, platform.actor.list.User).Testing();
        await OnDisk(uploaded, "/Record.goal", Records("uploaded"));
        await OnDisk(uploaded, "/Work.goal", Make.Step("set %worked% = yes",
            Make.Action(Built, "variable", "set", Make.Param(Built, "Name", "worked", "variable"), ("Value", "yes"))));
        var context = sub.actor.list.User.Context;
        await (await (await Load(sub, "/Work")).Start(context)).IsSuccess();
        return (await context.Variable.GetValue("recorded"))?.ToString();
    }

    [Test] public async Task APlatformsMeter_InAnUploadedSubApp_CallsThePlatformsRecord()
        => await Assert.That(await Metered("/Meter")).IsEqualTo("platform");

    // the same with Meter in the platform's own system/ folder: an app's system/ copy is that app's, as any of its files
    [Test] public async Task APlatformsMeter_InItsOwnSystemFolder_StillCallsThePlatformsRecord()
        => await Assert.That(await Metered("/system/platform/Meter")).IsEqualTo("platform");

    // an os goal (read from the os's folder: no app's) calling /system/Record in a sub app finds the sub app's own system/
    // copy, never its maker's
    [Test] public async Task AnOsGoalsCall_InASubApp_FindsTheSubAppsOwnSystemCopy()
    {
        var root = Root();
        await using var maker = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await OnDisk(root, "/system/Record.goal", Records("maker"));
        var uploaded = System.IO.Path.Combine(root, "uploaded");
        await using var sub = new global::app.@this(uploaded, maker.actor.list.User).Testing();
        await OnDisk(uploaded, "/system/Record.goal", Records("uploaded"));
        var context = sub.actor.list.User.Context;
        // a goal of the os's, read from where the os keeps it
        var made = Make.Goal(Built, "Caller", "/system/probe/Caller.goal", Calls("/system/Record"));
        var at = global::app.type.item.path.@this.Resolve(System.IO.Path.Combine(sub.filesystem.os.Absolute, "system", "probe", ".build", "caller.pr"), context);
        var os = await global::PLang.Tests.Shared.RealGoalLoad.Read(sub, await Built.Pr(made), at);

        await (await os.Start(context)).IsSuccess();

        await Assert.That(os.App).IsNull();
        await Assert.That((await context.Variable.GetValue("recorded"))?.ToString()).IsEqualTo("uploaded");
    }

    // an app's own system/error/Show.goal replaces the os's for its goal's call
    [Test] public async Task AnAppsOwnSystemGoal_ReplacesTheOss()
    {
        var root = Root();
        await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await OnDisk(root, "/system/error/Show.goal", Records("app's own"));
        await OnDisk(root, "/Fail.goal", Calls("/system/error/Show"));
        var context = app.actor.list.User.Context;

        await (await (await Load(app, "/Fail")).Start(context)).IsSuccess();

        await Assert.That((await context.Variable.GetValue("recorded"))?.ToString()).IsEqualTo("app's own");
    }
}
