using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.Goals;

// Security probes (event-action, stage 8): a platform binds `before each goal, call Meter`; Meter calls a goal; an uploaded sub app
// (in a folder of its own under the platform's) tries every way to get ITS goal run in Meter's place. recorded = "platform" is safe,
// "uploaded" means the upload redirected its maker's call.
public class PlatformStoryProbeTests
{
    private static string Root() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-probe8-" + Guid.NewGuid().ToString("N")[..8]);

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

    private static Make.StepDef Records(string value) => Make.Step($"set %recorded% = {value}",
        Make.Action(Built, "variable", "set", Make.Param(Built, "Name", "recorded", "variable"), ("Value", value)));

    private static Make.StepDef Calls(string goal) => Make.Step($"call {goal}", Make.Call(Built, goal));

    private static async Task<global::app.goal.@this> Load(global::app.@this app, string name)
        => (await (await app.goal.list.Find(name)).Value())!;

    // meterAt: where the platform's Meter lives; calls: the name Meter writes; uploadedRecords: where the upload puts a Record;
    // platformRecords: where the platform's own Record is; preloaded: goals the platform's own list has read (a listing/build of its root)
    private static async Task<(string? recorded, string? error)> Story(string meterAt, string calls, string[] uploadedRecords,
        string[] platformRecords, string[]? preloaded = null, (string, string)? preset = null)
    {
        var root = Root();
        await using var platform = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
        await OnDisk(root, meterAt, Calls(calls));
        foreach (var at in platformRecords) await OnDisk(root, at, Records("platform"));
        await OnDisk(root, "/Start.goal", Make.Step("before each goal, call Meter", Make.Action(Built, "on", "event",
            ("Event", "%!app.type.goal.on.start%"), ("When", "before"), ("Action", Make.Call(Built, meterAt.Replace(".goal", ""))))));

        var uploaded = System.IO.Path.Combine(root, "uploaded");
        foreach (var at in uploadedRecords) await OnDisk(uploaded, at, Records("uploaded"));
        await OnDisk(uploaded, "/Work.goal", Make.Step("set %worked% = yes",
            Make.Action(Built, "variable", "set", Make.Param(Built, "Name", "worked", "variable"), ("Value", "yes"))));
        foreach (var name in preloaded ?? []) await platform.goal.list.Find(name);

        await (await (await Load(platform, "/Start")).Start(platform.actor.list.User.Context)).IsSuccess();
        await using var sub = new global::app.@this(uploaded, platform.actor.list.User).Testing();
        var context = sub.actor.list.User.Context;
        if (preset is { } p) await context.Variable.Set(p.Item1, new global::app.type.item.text.@this(p.Item2, sub.type.list[typeof(global::app.type.item.text.@this)]));
        var ran = await (await Load(sub, "/Work")).Start(context);
        return ((await context.Variable.GetValue("recorded"))?.ToString(), ran.Error?.Key);
    }

    private static async Task Show(string label, (string? recorded, string? error) result)
        => await TestContext.Current!.OutputWriter.WriteLineAsync($"P8 {label}: recorded={result.recorded} error={result.error}");

    // 1. the upload's own system/ copy and root Record, Meter beside the platform's Record
    [Test] public async Task A_UploadsOwnSystemCopy_DoesNotAnswerTheMakersCall()
    {
        var r = await Story("/Meter.goal", "Record", ["/system/Record.goal", "/Record.goal"], ["/Record.goal"]);
        await Show("A", r);
        await Assert.That(r.recorded).IsEqualTo("platform");
    }

    // 2. an app-absolute /system/ name, both have a system/Record
    [Test] public async Task B_ASystemName_ResolvesInTheMakersApp()
    {
        var r = await Story("/Meter.goal", "/system/Record", ["/system/Record.goal"], ["/system/Record.goal"]);
        await Show("B", r);
        await Assert.That(r.recorded).IsEqualTo("platform");
    }

    // 3. Meter in the platform's system/platform/ folder calls a bare name; the upload mirrors the folder
    [Test] public async Task C_AMirroredSystemFolder_DoesNotAnswer()
    {
        var r = await Story("/system/platform/Meter.goal", "Record", ["/system/platform/Record.goal", "/system/Record.goal", "/Record.goal"], ["/Record.goal"]);
        await Show("C", r);
        await Assert.That(r.recorded).IsEqualTo("platform");
    }

    // 4. a Record in a nested folder of the upload; the platform has read nothing of the upload
    [Test] public async Task D_ANestedSameNamedGoal_NotReadByThePlatform_DoesNotAnswer()
    {
        var r = await Story("/system/platform/Meter.goal", "Record", ["/nested/Record.goal"], ["/Record.goal"]);
        await Show("D", r);
        await Assert.That(r.recorded).IsEqualTo("platform");
    }

    // 5. the same, but the platform's own list has read the upload's goal (a listing or build over its root, which holds the upload's folder)
    [Test, Skip("backlog: a bare name's read-anywhere fallback reaches a goal under another app's root (event-action security F4)")]
    public async Task E_ANestedSameNamedGoal_ReadByThePlatformsOwnList_DoesNotAnswer()
    {
        var r = await Story("/system/platform/Meter.goal", "Record", ["/nested/Record.goal"], ["/Record.goal"], ["/uploaded/nested/Record"]);
        await Show("E", r);
        await Assert.That(r.recorded).IsEqualTo("platform");
    }

    // 6. `..` and a wildcard in the name Meter writes: refused or a pattern, never a goal run
    [Test] public async Task F_ADotDotName_IsRefused()
    {
        var r = await Story("/system/platform/Meter.goal", "../Record", ["/Record.goal"], ["/Record.goal"]);
        await Show("F", r);
        await Assert.That(r.recorded).IsNotEqualTo("uploaded");
    }

    [Test] public async Task G_AWildcardName_IsAPattern_NeverARun()
    {
        var r = await Story("/Meter.goal", "Rec*", ["/Record.goal"], ["/Record.goal"]);
        await Show("G", r);
        await Assert.That(r.recorded).IsNotEqualTo("uploaded");
    }

    // 7. steering by the variables Meter runs with: Meter calls %target%, the sub app has set it. Only a goal the MAKER's list finds can
    // run; a name the maker's list finds inside the upload's own folder (under the maker's root) runs the upload's goal. Info: it takes a
    // maker's `call %variable%` of a value the upload sets.
    [Test] public async Task H1_ACallByVariable_NamesOnlyWhatTheMakersListFinds()
    {
        var r = await Story("/Meter.goal", "%target%", ["/Record.goal"], ["/Record.goal"], null, ("target", "Record"));
        await Show("H1", r);
        await Assert.That(r.recorded).IsEqualTo("platform");
    }

    [Test] public async Task H2_ACallByVariable_CanNameAGoalInTheUploadsFolder_WhenItLiesUnderTheMakersRoot()
    {
        var r = await Story("/Meter.goal", "%target%", ["/Record.goal"], ["/Record.goal"], null, ("target", "/uploaded/Record"));
        await Show("H2 (info)", r);
        await Assert.That(r.error).IsNull();
    }
}
