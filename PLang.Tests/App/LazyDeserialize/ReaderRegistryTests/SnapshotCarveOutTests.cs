using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.ReaderRegistryTests;

// The architect's carve-out: snapshot's `FromWire` and the `app.Snapshot*`
// methods stay. Another branch owns snapshot's OBP rename; this branch
// touches snapshot internals only to compile. These rows pin that the
// signatures are still there after Stage 1 lands.
public class SnapshotCarveOutTests
{
    private static Assembly PLangAssembly => typeof(global::app.@this).Assembly;

    // app/snapshot/this.Wire.cs — snapshot's static `FromWire(string, string?)` stays.
    [Test] public async Task Snapshot_FromWire_StillExists()
    {
        var t = PLangAssembly.GetType("app.snapshot.this");
        await Assert.That(t).IsNotNull();
        var m = t!.GetMethod("FromWire", BindingFlags.Public | BindingFlags.Static);
        await Assert.That(m).IsNotNull();
    }

    [Test] public async Task App_SnapshotToWire_StillExists()
        => await Assert.That(typeof(global::app.@this).GetMethod("SnapshotToWire")).IsNotNull();

    [Test] public async Task App_SnapshotFromWire_StillExists()
        => await Assert.That(typeof(global::app.@this).GetMethod("SnapshotFromWire")).IsNotNull();

    [Test] public async Task App_ResumeFromWire_StillExists()
        => await Assert.That(typeof(global::app.@this).GetMethod("ResumeFromWire")).IsNotNull();
}
