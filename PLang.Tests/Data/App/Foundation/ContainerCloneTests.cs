namespace PLang.Tests.App.Foundation;

/// <summary>
/// A container clones by its elements: each Data in it is copied in its own context, never a copy of the context (a
/// context reaches the app, so a structural copy of one copies the app), and each nested container is a copy of its own.
/// </summary>
public class ContainerCloneTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this(
        "/tmp/ContainerCloneTests-" + System.Guid.NewGuid().ToString("N")[..6], global::PLang.Tests.Parent.Actor).Testing();

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Context => app.actor.list.User.Context;

    private global::app.type.item.dict.@this Dict() => new(app.type.list[typeof(global::app.type.item.dict.@this)]);

    [Test]
    public async Task ADictsClone_HoldsItsDataInTheirOwnContext_AndIsACopy()
    {
        var nested = Dict();
        nested.Set(new global::app.data.@this("deep", "before", context: Context));
        var dict = Dict();
        dict.Set(new global::app.data.@this("inner", "x", context: Context));
        dict.Set(new global::app.data.@this("nested", nested, context: Context));

        var clone = (global::app.type.item.dict.@this)dict.Clone();

        await Assert.That(clone.Get("inner", Context)!.Context).IsSameReferenceAs(Context);
        // a copy: a change to the clone's nested container doesn't reach the original's
        var clonedNested = await clone.Get("nested", Context)!.Value<global::app.type.item.dict.@this>();
        clonedNested!.Set(new global::app.data.@this("deep", "after", context: Context));
        await Assert.That((await nested.Get("deep", Context)!.Value())?.ToString()).IsEqualTo("before");
    }

    [Test]
    public async Task AListsClone_HoldsItsDataInTheirOwnContext_AndIsACopy()
    {
        var nested = Dict();
        nested.Set(new global::app.data.@this("deep", "before", context: Context));
        var list = new global::app.type.item.list.@this(new global::app.data.@this[]
        {
            new("", "x", context: Context),
            new("", nested, context: Context),
        }, app.type.list[typeof(global::app.type.item.list.@this)]);

        var clone = (global::app.type.item.list.@this)list.Clone();

        var rows = clone.Items(Context).ToList();
        await Assert.That(rows[0].Context).IsSameReferenceAs(Context);
        var clonedNested = await rows[1].Value<global::app.type.item.dict.@this>();
        clonedNested!.Set(new global::app.data.@this("deep", "after", context: Context));
        await Assert.That((await nested.Get("deep", Context)!.Value())?.ToString()).IsEqualTo("before");
    }

    [Test]
    public async Task ASnapshotInsideAList_ClonesInItsOwnContext()
    {
        var snapshot = new global::app.snapshot.@this(Context);
        snapshot.Entries.Set(new global::app.data.@this("id", "c1", context: Context));
        var list = new global::app.type.item.list.@this(new global::app.type.item.@this[] { snapshot },
            app.type.list[typeof(global::app.type.item.list.@this)]);

        var clone = (global::app.type.item.list.@this)list.Clone();

        var copied = clone.Items(Context).Single().Peek() as global::app.snapshot.@this;
        await Assert.That(copied).IsNotSameReferenceAs(snapshot);
        await Assert.That(copied!.Context).IsSameReferenceAs(Context);
    }
}
