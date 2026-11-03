using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.Context;

// Security probes (call-cost): what is now kept and shared that used to be made fresh — the setting store's kept option
// answers (by actor, by sub app), the catalog element's one resolver, kinded types, goal.list's kept candidates.
public class CallCostSecurityProbeTests
{
    private static global::app.actor.context.@this Built => global::PLang.Tests.Parent.Actor.Context;

    private static global::app.goal.step.action.@this SetX()
        => Make.Action(Built, "variable", "set", Make.Param(Built, "Name", "x", "variable"), ("Value", "1"));

    private static async Task<string?> Answer(global::app.actor.context.@this context, global::app.goal.step.action.@this action, string option)
    {
        var answer = await context.Setting.Get(action, option);
        return answer.IsInitialized ? (await answer.Value())?.ToString() : null;
    }

    // 1a. the user's own value beats the system's; the system's never reads the user's; each change is the next answer, both warm
    [Test]
    public async Task S1_UserOwnValue_WinsOverSystems_AndEachSetIsTheNextAnswer()
    {
        await using var app = new global::app.@this("/probe", global::PLang.Tests.Parent.Actor).Testing();
        var user = app.actor.list.User;
        var system = app.actor.list.System;
        var action = SetX();
        var key = $"{action.Address}.type";
        await Answer(user.Context, action, "type"); await Answer(system.Context, action, "type");
        user.Setting.Set(key, "mine");
        system.Setting.Set(key, "sys");
        await Assert.That(await Answer(user.Context, action, "type")).IsEqualTo("mine");
        await Assert.That(await Answer(system.Context, action, "type")).IsEqualTo("sys");
        user.Setting.Set(key, null);
        await Assert.That(await Answer(user.Context, action, "type")).IsEqualTo("sys");
        system.Setting.Set(key, "sys2");
        await Assert.That(await Answer(user.Context, action, "type")).IsEqualTo("sys2");
        system.Setting.Set(key, null);
        await Assert.That(await Answer(user.Context, action, "type")).IsNull();
        await Assert.That(await Answer(system.Context, action, "type")).IsNull();
    }

    // 1b. a sub app's system actor and the maker's: neither answers for the other, warm or cold, in either order
    [Test]
    public async Task S2_ASubAppsSystemActor_AndItsMakers_NeverAnswerForEachOther()
    {
        await using var maker = new global::app.@this("/probe", global::PLang.Tests.Parent.Actor).Testing();
        await using var sub = new global::app.@this("/probe/sub", maker.actor.list.User).Testing();
        var action = SetX();
        var key = $"{action.Address}.type";
        foreach (var app in new[] { maker, sub })
            foreach (var actor in new[] { app.actor.list.User, app.actor.list.System })
                await Answer(actor.Context, action, "type");
        sub.actor.list.System.Setting.Set(key, "sub-system");
        await Assert.That(await Answer(sub.actor.list.User.Context, action, "type")).IsEqualTo("sub-system");
        await Assert.That(await Answer(maker.actor.list.User.Context, action, "type")).IsNull();
        await Assert.That(await Answer(maker.actor.list.System.Context, action, "type")).IsNull();
        maker.actor.list.System.Setting.Set(key, "maker-system");
        await Assert.That(await Answer(sub.actor.list.User.Context, action, "type")).IsEqualTo("sub-system");
        await Assert.That(await Answer(sub.actor.list.System.Context, action, "type")).IsEqualTo("sub-system");
        sub.actor.list.System.Setting.Set(key, null);
        await Assert.That(await Answer(sub.actor.list.User.Context, action, "type")).IsNull();
        await Assert.That(await Answer(maker.actor.list.User.Context, action, "type")).IsEqualTo("maker-system");
    }

    // 1c. a goal-call child that sets its own value, with a grandchild reading through it: a sibling asked after never sees it,
    // and the child's value goes when the child's layer clears
    [Test]
    public async Task S3_AChildLayersValue_ReachesItsDescendantsAlone()
    {
        await using var app = new global::app.@this("/probe", global::PLang.Tests.Parent.Actor).Testing();
        var user = app.actor.list.User;
        var action = SetX();
        var key = $"{action.Address}.type";
        var a = user.Context.Child(user, System.Threading.CancellationToken.None);
        var b = user.Context.Child(user, System.Threading.CancellationToken.None);
        var grand = a.Child(user, System.Threading.CancellationToken.None);
        foreach (var c in new[] { user.Context, a, b, grand }) await Answer(c, action, "type");
        a.Setting.Set(key, "a-only");
        await Assert.That(await Answer(grand, action, "type")).IsEqualTo("a-only");
        await Assert.That(await Answer(b, action, "type")).IsNull();
        await Assert.That(await Answer(user.Context, action, "type")).IsNull();
        var late = a.Child(user, System.Threading.CancellationToken.None);
        await Assert.That(await Answer(late, action, "type")).IsEqualTo("a-only");
        a.Setting.Set(key, null);
        await Assert.That(await Answer(grand, action, "type")).IsNull();
        await Assert.That(await Answer(late, action, "type")).IsNull();
        a.Dispose(); b.Dispose(); grand.Dispose(); late.Dispose();
    }

    // 2. the one resolver (made with the module's app's System context) never runs a step: a run in a sub app writes its own
    // context, whichever app asked first; the maker's contexts see nothing
    [Test]
    public async Task R1_ARunsHandler_IsTheRunsOwn_WhoeverWarmedTheResolver()
    {
        await using var maker = new global::app.@this("/probe", global::PLang.Tests.Parent.Actor).Testing();
        await using var sub = new global::app.@this("/probe/sub", maker.actor.list.User).Testing();
        var action = SetX();
        // the sub app asks first, then the maker, then the sub again
        await (await action.Start(sub.actor.list.User.Context)).IsSuccess();
        await Assert.That((await sub.actor.list.User.Context.Variable.GetValue("x"))?.ToString()).IsEqualTo("1");
        await Assert.That(await maker.actor.list.User.Context.Variable.GetValue("x")).IsNull();
        await Assert.That(await maker.actor.list.System.Context.Variable.GetValue("x")).IsNull();
        await Assert.That(await sub.actor.list.System.Context.Variable.GetValue("x")).IsNull();

        var other = Make.Action(Built, "variable", "set", Make.Param(Built, "Name", "y", "variable"), ("Value", "2"));
        await (await other.Start(maker.actor.list.User.Context)).IsSuccess();
        await (await other.Start(sub.actor.list.User.Context)).IsSuccess();
        await Assert.That(await maker.actor.list.System.Context.Variable.GetValue("y")).IsNull();
        await Assert.That(await sub.actor.list.System.Context.Variable.GetValue("y")).IsNull();
        await Assert.That((await maker.actor.list.User.Context.Variable.GetValue("y"))?.ToString()).IsEqualTo("2");

        // the resolver itself: whose System context holds it (info), and that a run is never given it
        var resolver = action.Module[action.Name]!.Resolver!;
        var held = (global::app.actor.context.@this)resolver.GetType().GetProperty("Context")!.GetValue(resolver)!;
        await TestContext.Current!.OutputWriter.WriteLineAsync(
            $"CC resolver context: maker-system={ReferenceEquals(held, maker.actor.list.System.Context)} sub-system={ReferenceEquals(held, sub.actor.list.System.Context)}");
        await Assert.That(ReferenceEquals(held, maker.actor.list.User.Context)).IsFalse();
        await Assert.That(ReferenceEquals(held, sub.actor.list.User.Context)).IsFalse();
    }

    // 2b. a step's params resolve with the run's context: a [Default] and a variable are read in the run's variables
    [Test]
    public async Task R2_AParameterVariable_IsReadInTheRunsContext_NotTheResolvers()
    {
        await using var maker = new global::app.@this("/probe", global::PLang.Tests.Parent.Actor).Testing();
        await using var sub = new global::app.@this("/probe/sub", maker.actor.list.User).Testing();
        var set = Make.Action(Built, "variable", "set", Make.Param(Built, "Name", "z", "variable"), ("Value", "%who%"));
        await maker.actor.list.User.Context.Variable.Set("who", new global::app.type.item.text.@this("maker", maker.type.list[typeof(global::app.type.item.text.@this)]));
        await sub.actor.list.User.Context.Variable.Set("who", new global::app.type.item.text.@this("sub", sub.type.list[typeof(global::app.type.item.text.@this)]));
        await maker.actor.list.System.Context.Variable.Set("who", new global::app.type.item.text.@this("system", maker.type.list[typeof(global::app.type.item.text.@this)]));
        await (await set.Start(maker.actor.list.User.Context)).IsSuccess();
        await (await set.Start(sub.actor.list.User.Context)).IsSuccess();
        await Assert.That((await maker.actor.list.User.Context.Variable.GetValue("z"))?.ToString()).IsEqualTo("maker");
        await Assert.That((await sub.actor.list.User.Context.Variable.GetValue("z"))?.ToString()).IsEqualTo("sub");
    }

    // 3. a kind a sub app coins on a type it shares with its maker: shared (the documented cache — a kind coined once); no template
    // or strict rides along, and the entry itself (no kind) is untouched
    [Test]
    public async Task T1_AKindedType_ASubAppAsks_IsTheMakersObjectToo_AndTheBareTypeIsUntouched()
    {
        await using var maker = new global::app.@this("/probe", global::PLang.Tests.Parent.Actor).Testing();
        await using var sub = new global::app.@this("/probe/sub", maker.actor.list.User).Testing();
        var number = maker.type.list[typeof(global::app.type.item.number.@this)];
        var viaSub = sub.type.list[typeof(global::app.type.item.number.@this)].Kinded("probekind-sub");
        var viaMaker = number.Kinded("probekind-sub");
        await TestContext.Current!.OutputWriter.WriteLineAsync($"CC kinded: same object in maker and sub = {ReferenceEquals(viaSub, viaMaker)}");
        await Assert.That(number.Kinded(null)).IsSameReferenceAs(number);
        await Assert.That(viaSub.Strict).IsFalse();
        await Assert.That(viaSub.Template).IsNull();
        await Assert.That(number.Strict).IsFalse();
        await Assert.That(number.Template).IsNull();
    }

    // 6. goal.list.Find: the kept candidates go when a goal comes in, per list; a goal added to the maker's list is never a sub app's
    // candidate, and one added to the sub's is never the maker's
    [Test]
    public async Task F1_AKeptFind_SeesAGoalAddedLater_InItsOwnListOnly()
    {
        await using var maker = new global::app.@this("/probe", global::PLang.Tests.Parent.Actor).Testing();
        await using var sub = new global::app.@this("/probe/sub", maker.actor.list.User).Testing();
        var mc = maker.actor.list.User.Context; var sc = sub.actor.list.User.Context;
        var makerCaller = Make.Goal(mc, "MakerCaller", "/area/MakerCaller.goal");
        var subCaller = Make.Goal(sc, "SubCaller", "/area/SubCaller.goal");
        maker.goal.list.Add(makerCaller);
        sub.goal.list.Add(subCaller);
        await maker.goal.list.Find("Late", makerCaller);
        await sub.goal.list.Find("Late", subCaller);
        maker.goal.list.Add(Make.Goal(mc, "Late", "/area/Late.goal"));
        await Assert.That((await maker.goal.list.Find("Late", makerCaller)).Success).IsTrue();
        var viaSub = await sub.goal.list.Find("Late", subCaller);
        await TestContext.Current!.OutputWriter.WriteLineAsync($"CC sub find Late: success={viaSub.Success} error={viaSub.Error?.Key} app-is-maker={(viaSub.Success && ReferenceEquals((await viaSub.Value())?.App, maker))} app-is-sub={(viaSub.Success && ReferenceEquals((await viaSub.Value())?.App, sub))} subItems={sub.goal.list.CountRaw} makerItems={maker.goal.list.CountRaw}");
        await Assert.That(sub.goal.list.CountRaw).IsEqualTo(1);   // the test disk is shared (Make.Goal writes it): a find may open it; the lists are what is checked
        sub.goal.list.Add(Make.Goal(sc, "Late2", "/area/Late2.goal"));
        await Assert.That((await sub.goal.list.Find("Late2", subCaller)).Success).IsTrue();
        await Assert.That(maker.goal.list.CountRaw).IsEqualTo(2);
        // the same name in both: each caller gets the one of its own app's list
        var makers = Make.Goal(mc, "Same", "/area/Same.goal");
        var subs = Make.Goal(sc, "Same", "/area/Same.goal");
        maker.goal.list.Add(makers);
        sub.goal.list.Add(subs);
        await Assert.That(await (await maker.goal.list.Find("Same", makerCaller)).Value()).IsSameReferenceAs(makers);
        await Assert.That(await (await sub.goal.list.Find("Same", subCaller)).Value()).IsSameReferenceAs(subs);
    }

    // B1 (auditor): a kept answer is one Data shared by every run of its keeper; a handler's typed face of it (`As<T>`) that
    // writes a property: does another run's read of the answer see it?
    [Test]
    public async Task B1_Pinned_APropertyWrittenOnARunsFaceOfAKeptAnswer_IsSeenByAnotherRun()
    {
        await using var app = new global::app.@this("/probe", global::PLang.Tests.Parent.Actor).Testing();
        var user = app.actor.list.User;
        var action = SetX();
        user.Setting.Set($"{action.Address}.type", "text");
        var runA = user.Context.Child(user, System.Threading.CancellationToken.None);
        var runB = user.Context.Child(user, System.Threading.CancellationToken.None);
        var seenByA = await runA.Setting.Get(action, "type");
        var face = seenByA.As<global::app.type.item.text.@this>();
        face.Properties["leak"] = "from-run-A";
        var seenByB = await runB.Setting.Get(action, "type");
        var leaked = await seenByB.Properties.Value("leak");
        await TestContext.Current!.OutputWriter.WriteLineAsync(
            $"CC B1: same Data={ReferenceEquals(seenByA, seenByB)} leaked={leaked?.ToString()} contextIsRunB={ReferenceEquals(seenByB.Context, runB)} contextIsKeeper={ReferenceEquals(seenByB.Context, user.Context)}");
        // pinned edge: the face shares the answer's properties bag, so the write is shared. No production handler writes onto a
        // parameter it was given (grep of Properties writers: all on a result it made); flip this when the answer is copied per run
        await Assert.That(leaked?.ToString()).IsEqualTo("from-run-A");
        runA.Dispose(); runB.Dispose();
    }
}
