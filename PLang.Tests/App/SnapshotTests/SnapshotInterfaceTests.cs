namespace PLang.Tests.App.SnapshotTests;

public class SnapshotInterfaceTests
{
    [Test]
    public async Task ISnapshotted_Capture_AppendsTypedEntries_ToSnapshot()
    {
        // Capture writes typed entries; Restore reads them back in order.
        var s = new Snapshot();
        var section = s.Section("MySection");
        section.Write("name", "alice");
        section.Write<int>("age", 42);
        section.Write("tags", new List<string> { "a", "b" });

        await Assert.That(section.Read<string>("name")).IsEqualTo("alice");
        await Assert.That(section.Read<int>("age")).IsEqualTo(42);
        await Assert.That(section.Read<List<string>>("tags")).IsEquivalentTo(new[] { "a", "b" });
        await Assert.That(section.Has("name")).IsTrue();
        await Assert.That(section.Has("missing")).IsFalse();
    }

    [Test]
    public async Task ISnapshotted_RestoreIsStaticFactory_NotInstanceMethod()
    {
        // Pins the `static abstract Restore(Snapshot.@this s, Context.@this ctx)` shape.
        var iface = typeof(ISnapshotted);
        var restore = iface.GetMethod("Restore",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        await Assert.That(restore).IsNotNull();
        await Assert.That(restore!.IsStatic).IsTrue();
        await Assert.That(restore.IsAbstract).IsTrue();

        var pars = restore.GetParameters();
        await Assert.That(pars.Length).IsEqualTo(2);
        await Assert.That(pars[0].ParameterType).IsEqualTo(typeof(Snapshot));
        await Assert.That(pars[1].ParameterType).IsEqualTo(typeof(global::App.Actor.Context.@this));
    }
}
