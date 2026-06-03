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
    // app/snapshot/this.Wire.cs:83 — snapshot's `FromWire(...)` stays.
    [Test] public async Task Snapshot_FromWire_StillExists() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task App_SnapshotToWire_StillExists() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task App_SnapshotFromWire_StillExists() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task App_ResumeFromWire_StillExists() { throw new System.NotImplementedException("not implemented"); }
}
