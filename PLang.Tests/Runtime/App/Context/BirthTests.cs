namespace PLang.Tests.App.Context;

/// <summary>An actor's and a context's birth makes only what is asked for: what is never used is never made.</summary>
public class BirthTests
{
    // the bytes one birth of what make makes allocates, the JIT and statics paid first
    private static long Bytes(System.Func<object> make)
    {
        make();
        var from = System.GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 20; i++) make();
        return (System.GC.GetAllocatedBytesForCurrentThread() - from) / 20;
    }

    [Test]
    public async Task AnActorsTasksAndPermission_AreMadeWhenFirstAsked()
    {
        var app = global::PLang.Tests.Parent.Actor.App;

        var born = Bytes(() => new global::app.actor.@this(new global::app.actor.role.user(), app));

        await Assert.That(born).IsLessThan(23 * 1024L).Because("an actor's tasks and permission view are made when first asked");
    }

    [Test]
    public async Task AContextsSystemVariables_AreMadeWhenFirstAsked()
    {
        var actor = global::PLang.Tests.Parent.Actor.App.actor.list.User;

        var born = Bytes(() => new global::app.actor.context.@this(actor.App, actor));

        await Assert.That(born).IsLessThan(17 * 1024L).Because("a memory's system variables are made when first asked");
    }

    [Test]
    public async Task AMemorysTable_IsMadeAtItsFirstWrite()
    {
        var actor = global::PLang.Tests.Parent.Actor.App.actor.list.User;

        var born = Bytes(() => new global::app.actor.context.@this(actor.App, actor));

        await Assert.That(born).IsLessThan(10_800L).Because("a memory's table is made at its first write");
    }

    [Test]
    public async Task AContextsCalls_FindTheirTypesAtTheFirstPush_AndTheirAuditAtTheFirstError()
    {
        var actor = global::PLang.Tests.Parent.Actor.App.actor.list.User;

        var born = Bytes(() => new global::app.actor.context.@this(actor.App, actor));

        await Assert.That(born).IsLessThan(9_700L).Because("a context's calls make their frame types and audit when first used");
    }

    [Test]
    public async Task ASystemVariable_IsReadUnderTheMemory_AndShadowedByASet()
    {
        await using var app = new global::app.@this("/birth", global::PLang.Tests.Parent.Actor).Testing();
        var memory = app.actor.list.User.Context.Variable;

        await Assert.That((await memory.Get("GUID")).IsInitialized).IsTrue();
        await Assert.That(memory.GetAll().Select(each => each.Key).Last()).IsEqualTo("Identity")
            .Because("the system's variables come after the memory's own");
        await memory.Set("Now", "set");
        await Assert.That((await memory.Value("Now")).ToString()).IsEqualTo("set");
        await Assert.That(memory.GetAll().Count(each => string.Equals(each.Key, "Now", System.StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
    }

    [Test]
    public async Task ASubAppsNodes_AreMadeWhenFirstUsed()
    {
        var born = Bytes(() =>
        {
            var app = new global::app.@this("/birth", global::PLang.Tests.Parent.Actor);
            _ = app.DisposeAsync();
            return app;
        });

        await Assert.That(born).IsLessThan(156_000L).Because("a node finds its type, its list and its setting class when first used");
    }

    // bytes measured on Linux: another OS makes a different number of them
    [Test, RunOn(TUnit.Core.Enums.OS.Linux)]
    public async Task ASubAppsCodeProviders_AreMadeAtTheirFirstUse()
    {
        var born = Bytes(() =>
        {
            var app = new global::app.@this("/birth", global::PLang.Tests.Parent.Actor);
            _ = app.DisposeAsync();
            return app;
        });

        await Assert.That(born).IsLessThan(130_000L).Because("an app's code providers are made at its registry's first use");
    }

    // bytes measured on Linux: another OS makes a different number of them
    [Test, RunOn(TUnit.Core.Enums.OS.Linux)]
    public async Task ASubAppsSystemPermissionView_IsMadeAtItsFirstAsk()
    {
        var born = Bytes(() =>
        {
            var app = new global::app.@this("/birth", global::PLang.Tests.Parent.Actor);
            _ = app.DisposeAsync();
            return app;
        });

        await Assert.That(born).IsLessThan(97_000L).Because("the system's standing grants are its view's, made at its first ask");
    }

    [Test]
    public async Task AValuesKeptList_IsMadeWhenAProgramAddsAMember()
    {
        var context = global::PLang.Tests.Parent.Actor.Context;
        var text = context.App.type.list["text"];

        var born = Bytes(() => new global::app.data.@this("x", new global::app.type.item.text.@this("v", text), context: context));

        await Assert.That(born).IsLessThan(600L).Because("a value's kept list is made when a program adds a member");
    }

    [Test]
    public async Task AValuesAttachment_AndItsPropertiesBag_AreMadeWhenAsked()
    {
        var context = global::PLang.Tests.Parent.Actor.Context;
        var text = context.App.type.list["text"];

        var born = Bytes(() => new global::app.data.@this("x", new global::app.type.item.text.@this("v", text), context: context));

        await Assert.That(born).IsLessThan(250L).Because("a value's attachment and its properties bag are made when asked");
    }

    [Test]
    public async Task AFaceOfAValue_SharesItsProperties_AndACopyTakesItsOwn()
    {
        var context = global::PLang.Tests.Parent.Actor.Context;
        var value = new global::app.data.@this("x", new global::app.type.item.text.@this("v", context.App.type.list["text"]), context: context);

        value.Properties["tag"] = "one";
        var copy = value.Copy("y");
        copy.Properties["tag"] = "two";

        await Assert.That(await value.Properties.Value("tag")).IsEqualTo("one");
        await Assert.That(await copy.Properties.Value("tag")).IsEqualTo("two");
    }

    // alone: the bytes are the process's, and a test beside it would add its own
    // bytes measured on Linux: another OS makes a different number of them
    [Test, NotInParallel, RunOn(TUnit.Core.Enums.OS.Linux)]
    public async Task ARequestReadsItsSettingClassesFacts_OncePerCode()
    {
        var root = System.IO.Path.Combine(Fixture.Root(), "test");
        async Task Request()
        {
            await using var app = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
            var loaded = await app.goal.Load("/module/condition/if/basic/Condition.test.goal");
            await (await ((await loaded.Value()) as global::app.goal.@this)!.Start(app.actor.list.User.Context)).IsSuccess();
        }
        for (var i = 0; i < 5; i++) await Request();
        var from = System.GC.GetTotalAllocatedBytes(true);
        for (var i = 0; i < 20; i++) await Request();
        var born = (System.GC.GetTotalAllocatedBytes(true) - from) / 20;

        await Assert.That(born).IsLessThan(1_475_000L).Because("a setting class's path and members are its code's facts, read once per code");
    }

    // a sub app that never caches makes no step cache (a .NET MemoryCache, ~47 KB): it is made at its first ask
    [Test]
    public async Task ASubAppsStepCache_IsMadeAtItsFirstAsk()
    {
        var born = Bytes(() =>
        {
            var app = new global::app.@this("/birth", global::PLang.Tests.Parent.Actor);
            _ = app.DisposeAsync();
            return app;
        });

        // 39,008 bytes on this machine; 86,036 with the cache made at birth
        await Assert.That(born).IsLessThan(60_000L).Because("the step cache is made at its first ask");
    }

    // a frame pushed and popped that nothing writes to makes no variable store, tags or errors
    [Test]
    public async Task AFrameNothingWritesTo_MakesNoStoreTagsOrErrors()
    {
        await using var app = new global::app.@this("/birth", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var setting = await context.call.Setting();
        var goal = Make.Goal(global::PLang.Tests.Parent.Actor.Context, "Frame");

        var born = Bytes(() =>
        {
            var frame = context.call.Push(goal, setting);
            _ = frame.DisposeAsync();
            return frame;
        });

        // 1,544 bytes on this machine; 2,592 with the three made at the push
        await Assert.That(born).IsLessThan(2_000L).Because("a frame's store, tags and errors are made at their first write");
    }

    // a goal started again judges no broken steps again: it was judged at its first start
    [Test]
    public async Task AGoalStartedAgain_JudgesItsStepsOnce()
    {
        await using var app = new global::app.@this("/birth", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var goal = Make.Goal(global::PLang.Tests.Parent.Actor.Context, "Again");
        for (var i = 0; i < 3; i++) await (await goal.Start(context)).IsSuccess();

        var from = System.GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 20; i++) await goal.Start(context);
        var born = (System.GC.GetAllocatedBytesForCurrentThread() - from) / 20;

        // 4,136 bytes on this machine; 4,376 with its steps judged at every start
        await Assert.That(born).IsLessThan(4_256L).Because("a goal's broken steps are judged once, at its first start");
    }

    // a step's Error written after a start (the build refusing it) is heard: the goal judges its steps again
    [Test]
    public async Task AGoalWhoseStepIsRefusedAfterAStart_AnswersBrokenAtItsNextStart()
    {
        await using var app = new global::app.@this("/birth", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var parent = global::PLang.Tests.Parent.Actor.Context;
        var goal = Make.Goal(parent, "Refused", Make.Step("set %x% = 1",
            Make.Action(parent, "variable", "set", Make.Param(parent, "Name", "x", "variable"), ("Value", "1"))));
        await (await goal.Start(context)).IsSuccess();

        goal.Step.Items().First().Error = new global::app.error.@this("refused", "Refused", 400);

        await Assert.That((await goal.Start(context)).Error?.Key).IsEqualTo("GoalBroken");
    }

    // a goal run again makes no number type per count or depth: the app's number type answers its kinded type, kept
    [Test]
    public async Task AGoalRunAgain_FindsItsNumberTypes()
    {
        await using var app = new global::app.@this("/birth", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var parent = global::PLang.Tests.Parent.Actor.Context;
        var goal = Make.Goal(parent, "Counted", Enumerable.Range(0, 8).Select(i => Make.Step($"set %x{i}% = 1",
            Make.Action(parent, "variable", "set", Make.Param(parent, "Name", $"x{i}", "variable"), ("Value", "1")))).ToArray());
        for (var i = 0; i < 3; i++) await (await goal.Start(context)).IsSuccess();

        var from = System.GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 20; i++) await goal.Start(context);
        var born = (System.GC.GetAllocatedBytesForCurrentThread() - from) / 20;

        // 189,456 bytes on this machine; 227,552 with a number type made per count and depth
        await Assert.That(born).IsLessThan(208_000L).Because("a count and a depth are of the app's number types, found, not made");
    }

    [Test]
    public async Task AnActorsPermissionView_IsOneView()
    {
        var actor = new global::app.actor.@this(new global::app.actor.role.user(), global::PLang.Tests.Parent.Actor.App);

        await Assert.That(actor.Permission).IsSameReferenceAs(actor.Permission);
        await Assert.That(actor.Task).IsSameReferenceAs(actor.Task);
    }
}
