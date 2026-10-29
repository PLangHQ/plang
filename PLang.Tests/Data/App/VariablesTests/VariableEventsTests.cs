namespace PLang.Tests.App.VariablesTests;

// A variable's events: one bound on a name (the debug watch) is held under that name, and a set of that name runs it; a
// set of any other name runs the variable type's list, and the variable object is made only when something runs. Held
// memory grows with binds, never with sets or names.
public class VariableEventsTests
{
    // The user's event after `name` is set, counting its runs.
    private static async Task<global::app.@event.@this> Watch(global::app.@this app, string name, List<string> ran)
    {
        var user = app.actor.list.User;
        var variable = new global::app.type.item.variable.parser.@this($"%{name}%", app.type.list).Variable.Single();
        return await user.@event.Add(name, new global::app.@event.code.@this(variable, "set", global::app.@event.When.after,
            (item, result, ctx) => { lock (ran) ran.Add(((global::app.type.item.variable.@this)item).Name); return Task.FromResult(result); },
            user, global::app.@event.code.@this.Always));
    }

    // The bytes 1,000 sets of distinct names under `prefix` allocate, after a warm-up.
    private static async Task<long> SetBytes(global::app.actor.context.@this context, string prefix)
    {
        for (var i = 0; i < 200; i++) await context.Variable.Set($"{prefix}warm{i}", i);
        var from = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) await context.Variable.Set($"{prefix}{i}", i);
        return GC.GetAllocatedBytesForCurrentThread() - from;
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

    [Test] public async Task UnbindingTheLastEventOnCount_RemovesItsEntry()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var events = app.actor.list.User.@event;
        var first = await Watch(app, "count", new List<string>());
        var second = await Watch(app, "count", new List<string>());
        await context.Variable.Set("count", 1);
        var type = await events.On(app.variable.type);
        await Assert.That(await events.On("count")).IsNotSameReferenceAs(type);

        first.Remove();
        await Assert.That(await events.On("count")).IsNotSameReferenceAs(type);
        second.Remove();
        await Assert.That(await events.On("count")).IsSameReferenceAs(type);
    }

    // The user's event after `name` is set, recording `tag` when it runs.
    private static Task Tagged(global::app.@this app, string name, List<string> ran, string tag)
    {
        var variable = new global::app.type.item.variable.parser.@this($"%{name}%", app.type.list).Variable.Single();
        return app.actor.list.User.@event.Add(name, new global::app.@event.code.@this(variable, "set", global::app.@event.When.after,
            (_, result, _) => { lock (ran) ran.Add(tag); return Task.FromResult(result); },
            app.actor.list.User, global::app.@event.code.@this.Always)).AsTask();
    }

    // a bind after the name's list was made (its first set) joins that list: the next set runs both, in the order bound
    [Test] public async Task ABindAfterANamesListWasMade_JoinsIt()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var ran = new List<string>();

        await Tagged(app, "count", ran, "a");
        await context.Variable.Set("count", 1);
        await Tagged(app, "count", ran, "b");
        await context.Variable.Set("count", 2);

        await Assert.That(ran).IsEquivalentTo(new[] { "a", "a", "b" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    // The user's event after any variable is set, recording `name` and the variable it ran for.
    private static Task Typed(global::app.@this app, List<string> ran, string name)
        => app.actor.list.User.@event.Add(new global::app.@event.code.@this(app.variable.type, "set", global::app.@event.When.after,
            (item, result, ctx) => { lock (ran) ran.Add($"{name} {((global::app.type.item.variable.@this)item).Name}"); return Task.FromResult(result); },
            app.actor.list.User, global::app.@event.code.@this.Always)).AsTask();

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
