using PLangGoal = app.goal.@this;
using Scope = global::app.@event.binding.Scope;

namespace PLang.Tests.App.Events;

// Security probes on a maker's handler running in a sub app (snapshot 251): the handler's goal is found in the maker's
// own app, but the goals IT calls are found by the goal.call that runs in the child's context.
public class InheritedHandlerResolutionProbeTests
{
    private string _dir = null!;

    [Before(Test)]
    public void Setup()
    {
        _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_probe_inherit_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_dir, "sub"));
    }

    [After(Test)]
    public void Teardown()
    {
        try { System.IO.Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    // a goal named `name` built at `folder` (.build/name.pr beside name.goal), through the goal's own writer
    private static async Task Write(global::app.@this app, string folder, string name)
    {
        var ctx = app.actor.list.User.Context;
        var goal = new PLangGoal(app.type.list) { Name = name, Path = global::app.type.item.path.@this.Resolve("/" + name + ".goal", ctx) };
        var pr = await ctx.Pr(goal);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(folder, ".build"));
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(folder, ".build", name.ToLowerInvariant() + ".pr"), pr);
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(folder, name + ".goal"), name + "\n");
    }

    // Pinned by Ingi's ruling (snapshot 265): "it's fine that a sub app can have a checkpolicy and overwrite". A maker's handler
    // goal calls Helper; the child's own Helper answers, as layers let a sub app override. So policy that must hold in a sub
    // app is not built on an inherited handler's calls.
    [Test]
    public async Task AHandlersNestedCall_IsAnsweredBy_AChildsOwnGoalOfTheSameName_ByDesign()
    {
        var maker = new global::app.@this(_dir, global::PLang.Tests.Parent.Actor).Testing();
        await using var _m = maker;
        await Write(maker, _dir, "Helper");
        var sub = new global::app.@this(System.IO.Path.Combine(_dir, "sub"), maker.actor.list.User);
        await using var _s = sub;
        await Write(sub, System.IO.Path.Combine(_dir, "sub"), "Helper");

        var handler = new PLangGoal(maker.type.list) { Name = "Handler", Path = global::app.type.item.path.@this.Resolve("/Handler.goal", maker.actor.list.User.Context) };
        // the child has loaded its own Helper
        var own = new PLangGoal(sub.type.list) { Name = "Main", Path = global::app.type.item.path.@this.Resolve("/Main.goal", sub.actor.list.User.Context) };
        await sub.goal.list.Find("Helper", own);

        var found = await sub.goal.list.Find("Helper", handler);

        await Assert.That(found.Error?.Message ?? "ok").IsEqualTo("ok");
        var path = (found.Peek() as PLangGoal)?.Path?.Absolute.Replace('\\', '/') ?? "";
        await Assert.That(path).Contains("/sub/");
    }

    // Within ONE app: two loaded goals named Helper, in different folders. A bare name from a caller beside one selects that
    // one, and an app-absolute `/Helper` is exactly /Helper.goal, never another folder's Helper by its suffix.
    [Test]
    public async Task InOneApp_ABareNamePrefersTheGoalBesideItsCaller_AndASlashAddressMatchesExactly()
    {
        var maker = new global::app.@this(_dir, global::PLang.Tests.Parent.Actor).Testing();
        await using var _m = maker;
        var other = System.IO.Path.Combine(_dir, "other");
        await Write(maker, other, "Helper");
        await Write(maker, _dir, "Helper");
        var ctx = maker.actor.list.User.Context;
        // the other folder's Helper is the one loaded last
        await maker.goal.list.Find("/Helper", null);
        await maker.goal.list.Find("/other/Helper", null);

        var caller = new PLangGoal(maker.type.list) { Name = "Caller", Path = global::app.type.item.path.@this.Resolve("/Caller.goal", ctx) };
        var bare = await maker.goal.list.Find("Helper", caller);
        var slash = await maker.goal.list.Find("/Helper", null);

        await Assert.That(bare.Error?.Message ?? "ok").IsEqualTo("ok");
        await Assert.That((bare.Peek() as PLangGoal)?.Path?.Absolute.Replace('\\', '/') ?? "").DoesNotContain("/other/");
        await Assert.That(slash.Error?.Message ?? "ok").IsEqualTo("ok");
        await Assert.That((slash.Peek() as PLangGoal)?.Path?.Absolute.Replace('\\', '/') ?? "").DoesNotContain("/other/");
    }

    // The handler itself: found in the maker's own app (Actor.App), so a child's goal of the same name never answers.
    [Test]
    public async Task TheHandlersOwnGoal_IsTheMakers_WhateverTheChildLoaded()
    {
        var maker = new global::app.@this(_dir, global::PLang.Tests.Parent.Actor).Testing();
        await using var _m = maker;
        await Write(maker, _dir, "Log");
        var sub = new global::app.@this(System.IO.Path.Combine(_dir, "sub"), maker.actor.list.User);
        await using var _s = sub;
        await Write(sub, System.IO.Path.Combine(_dir, "sub"), "Log");
        var step = new PLangGoal(maker.type.list) { Name = "Bind", Path = global::app.type.item.path.@this.Resolve("/Bind.goal", maker.actor.list.User.Context) };
        await sub.goal.list.Find("Log", new PLangGoal(sub.type.list) { Name = "Main", Path = global::app.type.item.path.@this.Resolve("/Main.goal", sub.actor.list.User.Context) });

        // the binding's own lookup: Actor.App.goal.list.Find(name, action.Step.Goal), Actor being the maker's actor
        var found = await maker.actor.list.User.App.goal.list.Find("Log", step);

        await Assert.That(found.Error?.Message ?? "ok").IsEqualTo("ok");
        await Assert.That((found.Peek() as PLangGoal)?.Path?.Absolute.Replace('\\', '/') ?? "").DoesNotContain("/sub/");
    }

    // A handler that makes a sub app each time it fires ends in AppTooDeep, never a hang or a stack overflow.
    [Test]
    public async Task AHandlerThatMakesSubApps_EndsInAppTooDeep()
    {
        var maker = new global::app.@this(_dir, global::PLang.Tests.Parent.Actor).Testing();
        await using var _m = maker;
        var item = maker.actor.list.User.Context.Dict();
        var made = 0;
        string? key = null;
        var side = maker.@event.Add(item).Add("start")!.before;
        side.Add(s => new global::app.@event.binding.@this(s, async (_, _, ctx) =>
        {
            made++;
            try
            {
                var child = new global::app.@this(_dir, ctx.Actor);
                await item.on.start.Before(item, child.actor.list.User.Context);
            }
            catch (global::app.error.AppException ex) { key = ex.Error.Key; }
            return ctx.Ok();
        }, maker.actor.list.System, Scope.app, global::app.@event.binding.@this.Always));

        var done = item.on.start.Before(item, maker.actor.list.User.Context).AsTask();
        var finished = await Task.WhenAny(done, Task.Delay(TimeSpan.FromSeconds(60)));

        await Assert.That(ReferenceEquals(finished, done)).IsTrue();
        await Assert.That(key).IsEqualTo("AppTooDeep");
        await Assert.That(made).IsLessThanOrEqualTo(20);
    }
}
