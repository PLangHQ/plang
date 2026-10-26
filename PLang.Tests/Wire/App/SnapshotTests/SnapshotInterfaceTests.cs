namespace PLang.Tests.App.SnapshotTests;

public class SnapshotInterfaceTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task ISnapshotted_Capture_AppendsTypedEntries_ToSnapshot()
    {
        // A node holds typed entries; they read back through their typed asks.
        var s = new Snapshot(app.actor.list.User.Context);
        var section = s.Node("MySection");
        section.Write("name", "alice");
        section.Write<int>("age", 42);
        section.Write("tags", new List<string> { "a", "b" });

        await Assert.That(await section.Text("name")).IsEqualTo("alice");
        await Assert.That(await section.Int("age")).IsEqualTo(42);
        var tags = new List<string>();
        foreach (var row in await section.Rows("tags"))
            tags.Add((await row.Value<global::app.type.item.text.@this>()).ToString());
        await Assert.That(tags).IsEquivalentTo(new[] { "a", "b" });
        await Assert.That(section.Has("name")).IsTrue();
        await Assert.That(section.Has("missing")).IsFalse();
    }

    [Test]
    public async Task ISnapshot_IsStoreAndStart_OnTheInstance_AndNamesNoPlace()
    {
        // A part owns both halves on its own instance — Store writes its node, Start comes back from it, each answering
        // Data — and names no place: its place is its member path, given by the walk.
        var iface = typeof(ISnapshot);
        var store = iface.GetMethod("Store");
        var start = iface.GetMethod("Start");

        await Assert.That(store).IsNotNull();
        await Assert.That(start).IsNotNull();
        await Assert.That(store!.IsStatic || start!.IsStatic).IsFalse();
        await Assert.That(store.ReturnType).IsEqualTo(typeof(global::app.data.@this));
        await Assert.That(start!.ReturnType).IsEqualTo(typeof(System.Threading.Tasks.Task<global::app.data.@this>));
        await Assert.That(iface.GetProperty("Section")).IsNull();
        foreach (var method in new[] { store, start })
        {
            var pars = method.GetParameters();
            await Assert.That(pars.Length).IsEqualTo(2);
            await Assert.That(pars[0].ParameterType).IsEqualTo(typeof(Snapshot));
            await Assert.That(pars[1].ParameterType).IsEqualTo(typeof(global::app.actor.context.@this));
        }
    }
}
