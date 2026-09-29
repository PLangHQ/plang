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

    [Test] public async Task App_SnapshotToWire_StillExists()
        => await Assert.That(typeof(global::app.@this).GetMethod("SnapshotToWire")).IsNotNull();

    [Test] public async Task App_SnapshotFromWire_StillExists()
        => await Assert.That(typeof(global::app.@this).GetMethod("SnapshotFromWire")).IsNotNull();

    [Test] public async Task App_ResumeFromWire_StillExists()
        => await Assert.That(typeof(global::app.@this).GetMethod("ResumeFromWire")).IsNotNull();
}
