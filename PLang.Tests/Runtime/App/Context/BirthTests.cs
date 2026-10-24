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
    public async Task AnActorsPermissionView_IsOneView()
    {
        var actor = new global::app.actor.@this("probe", global::PLang.Tests.Parent.Actor.App);

        await Assert.That(actor.Permission).IsSameReferenceAs(actor.Permission);
        await Assert.That(actor.Task).IsSameReferenceAs(actor.Task);
    }
}
