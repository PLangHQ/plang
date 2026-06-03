using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

// `_raw` stays authoritative *until a mutation*. A mutation invalidates
// `_raw` — serialize then renders from `_value` via the renderer. The
// invalidate-on-mutation rule is what keeps verbatim passthrough sound:
// if anything cleared `_raw` on read, passthrough and signing break.
public class MutationInvalidatesRawTests
{
    [Test] public async Task SetValueDirect_InvalidatesRaw() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task NavigationSet_InvalidatesRaw() { throw new System.NotImplementedException("not implemented"); }

    // Independent #7 — the end-to-end follow-on. After a mutation, a
    // subsequent Wire.Write emits the renderer's output, *not* the (now
    // stale) raw. Pins "post-mutation, raw is no longer authoritative" all
    // the way through the courier path.
    [Test] public async Task AfterMutation_SerializeUsesRenderer_NotRaw() { throw new System.NotImplementedException("not implemented"); }
}
