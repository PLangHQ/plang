namespace PLang.Tests.App.VariablesTests;

// A variable's events: one store and one way to bind. An event bound on a variable (on.event's `after %count.on.set%`,
// debug's watch) is bound as any is, its item the variable; a set of that variable runs it, a set of any other runs the
// variable type's list. A set makes no list for a variable nothing is bound on, so held memory grows with names that
// have events, never with sets or names.
public class VariableEventsTests
{
    private static global::app.type.item.variable.@this Variable(global::app.@this app, string name)
        => new global::app.type.item.variable.parser.@this(name, app.type.list).Whole!;

    // The user's event `moment` (set, remove) of `name`, recording `tag` when it runs.
    private static async Task<global::app.@event.@this> Watch(global::app.@this app, string name, List<string> ran, string? tag = null,
        string moment = "set")
    {
        var user = app.actor.list.User;
        return await user.@event.Add(new global::app.@event.code.@this(Variable(app, name), moment, global::app.@event.When.after,
            (item, result, _) => { lock (ran) ran.Add(tag ?? ((global::app.type.item.variable.@this)item).Name); return Task.FromResult(result); },
            user, global::app.@event.code.@this.Always));
    }

    // The user's event after any variable is set, recording `name` and the variable it ran for.
    private static Task Typed(global::app.@this app, List<string> ran, string name)
        => app.actor.list.User.@event.Add(new global::app.@event.code.@this(app.variable.type, "set", global::app.@event.When.after,
            (item, result, ctx) => { lock (ran) ran.Add($"{name} {((global::app.type.item.variable.@this)item).Name}"); return Task.FromResult(result); },
            app.actor.list.User, global::app.@event.code.@this.Always)).AsTask();

    // The bytes 1,000 sets of distinct names under `prefix` allocate, after a warm-up.
    private static async Task<long> SetBytes(global::app.actor.context.@this context, string prefix)
    {
        for (var i = 0; i < 200; i++) await context.Variable.Set($"{prefix}warm{i}", i);
        var from = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) await context.Variable.Set($"{prefix}{i}", i);
        return GC.GetAllocatedBytesForCurrentThread() - from;
    }

    // what on.event binds (the plain door), on the variable count: its set runs it
    [Test] public async Task AnEventBoundOnAVariable_ThePlainWay_RunsOnItsSet()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var ran = new List<string>();
        await Watch(app, "count", ran, "plain");

        await app.actor.list.User.Context.Variable.Set("count", 1);

        await Assert.That(ran).IsEquivalentTo(new[] { "plain" });
    }

    [Test] public async Task AWatchOnCount_LeavesASetOfAnotherName_NearTheNothingBoundBytes()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var nothing = await SetBytes(context, "plain");

        await Watch(app, "count", new List<string>());
        var watched = await SetBytes(context, "other");

        // the tester measured a miss at 34,363 bytes a set beside 5,009 with nothing bound; a few hundred bytes of noise
        await Assert.That((watched - nothing) / 1000).IsLessThan(500L);
    }

    // the process's memory is measured, so nothing else runs beside it
    [Test, NotInParallel] public async Task TenThousandDistinctNames_WithAnEventBound_HoldNoMoreThanWithNone()
    {
        async Task<long> Held(bool bound)
        {
            await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
            var context = app.actor.list.User.Context;
            if (bound) await Watch(app, "count", new List<string>());
            var before = GC.GetTotalMemory(true);
            for (var i = 0; i < 10_000; i++) await context.Variable.Set($"name{i}", i);
            return GC.GetTotalMemory(true) - before;
        }

        // the first app made in the run warms what every app shares; the two measured come after it
        await Held(false);
        var bound = await Held(true);
        var none = await Held(false);

        // what the 10,000 values hold is the same either way; an event bound must not add a list per name
        await Assert.That(Math.Abs(bound - none)).IsLessThan(200_000L);
    }

    [Test] public async Task AnEventBoundOnCount_RunsOnCountsNextSet_AndNotOnAnotherName()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var ran = new List<string>();
        await context.Variable.Set("count", 1);
        await Watch(app, "count", ran);

        await context.Variable.Set("count", 2);
        await context.Variable.Set("other", 3);

        await Assert.That(ran).IsEquivalentTo(new[] { "count" });
    }

    // a remove of the variable runs what is bound on its remove
    [Test] public async Task AnEventBoundOnCountsRemove_RunsOnItsRemove()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var ran = new List<string>();
        await Watch(app, "count", ran, "removed", "remove");

        await context.Variable.Set("count", 1);
        await context.Variable.Remove("count");

        await Assert.That(ran).IsEquivalentTo(new[] { "removed" });
    }

    // the variable's list is made while an event is bound on it; the last one unbound, the variable has none, and a set
    // of it runs the variable type's
    [Test] public async Task UnbindingTheLastEventOnCount_LeavesNoList()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var events = app.actor.list.User.@event;
        var count = Variable(app, "count");
        var first = await Watch(app, "count", new List<string>());
        var second = await Watch(app, "count", new List<string>());
        await context.Variable.Set("count", 1);
        var type = await events.On(app.variable.type);
        await Assert.That(await events.Moments(count)).IsNotSameReferenceAs(type);

        first.Remove();
        await Assert.That(await events.Moments(count)).IsNotSameReferenceAs(type);
        second.Remove();
        await Assert.That(await events.Moments(count)).IsSameReferenceAs(type);
    }

    // the moments of the variable count are one list, whoever asks: the set's ask and an ask by the variable item
    [Test] public async Task AVariablesMoments_AreOneList_TheSetsAndAnItemsAsk()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var events = app.actor.list.User.@event;
        var ran = new List<string>();
        await Watch(app, "count", ran);
        await context.Variable.Set("count", 1);

        await Assert.That(await events.On(Variable(app, "count"))).IsSameReferenceAs(await events.Moments(Variable(app, "count")));
        await Assert.That(ran).IsEquivalentTo(new[] { "count" });
    }

    // a bind after the variable's list was made (its first set) joins that list: the next set runs both, in the order bound
    [Test] public async Task ABindAfterAVariablesListWasMade_JoinsIt()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var ran = new List<string>();

        await Watch(app, "count", ran, "a");
        await context.Variable.Set("count", 1);
        await Watch(app, "count", ran, "b");
        await context.Variable.Set("count", 2);

        await Assert.That(ran).IsEquivalentTo(new[] { "a", "a", "b" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test] public async Task ASetOfAName_RunsWhatItsEventsAnswer_TheTypesAlone_OrTheTypesAndItsOwn_InTheOrderBound()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var ran = new List<string>();
        await Typed(app, ran, "type");
        await Watch(app, "count", ran);
        await Typed(app, ran, "later");

        await context.Variable.Set("other", 1);
        await context.Variable.Set("count", 2);

        await Assert.That(ran).IsEquivalentTo(new[] { "type other", "later other", "type count", "count", "later count" },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
