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

        var born = Bytes(() => new global::app.actor.@this("probe", app));

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

    [Test]
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

    [Test]
    public async Task AnActorsPermissionView_IsOneView()
    {
        var actor = new global::app.actor.@this("probe", global::PLang.Tests.Parent.Actor.App);

        await Assert.That(actor.Permission).IsSameReferenceAs(actor.Permission);
        await Assert.That(actor.Task).IsSameReferenceAs(actor.Task);
    }
}
