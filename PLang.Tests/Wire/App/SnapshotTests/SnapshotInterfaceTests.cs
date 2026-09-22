namespace PLang.Tests.App.SnapshotTests;

public class SnapshotInterfaceTests
{
    [Test]
    public async Task ISnapshotted_Capture_AppendsTypedEntries_ToSnapshot()
    {
        // Capture writes typed entries; Restore reads them back in order.
        var s = new Snapshot(global::PLang.Tests.TestApp.SharedContext);
        var section = s.Section("MySection");
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
    public async Task ISnapshotted_RestoreIsStaticFactory_NotInstanceMethod()
    {
        // Pins the `static abstract Restore(Snapshot.@this s, Context.@this context)` shape.
        var iface = typeof(ISnapshot);
        var restore = iface.GetMethod("Restore",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        await Assert.That(restore).IsNotNull();
        await Assert.That(restore!.IsStatic).IsTrue();
        await Assert.That(restore.IsAbstract).IsTrue();

        var pars = restore.GetParameters();
        await Assert.That(pars.Length).IsEqualTo(2);
        await Assert.That(pars[0].ParameterType).IsEqualTo(typeof(Snapshot));
        await Assert.That(pars[1].ParameterType).IsEqualTo(typeof(global::app.actor.context.@this));
    }
}
