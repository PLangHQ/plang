using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

// app/data/Wire.cs:141 today eagerly deserialises the value slot via
// `LiftDataIfShaped` / `Deserialize<object?>`. Stage 3 captures the value
// slot's raw json into `_raw`, stamps `type`/`kind` from the type slot,
// and defers materialisation. This is what makes wire-sourced Data pass
// through verbatim and verify against the original bytes.
public class WireReadLazyTests
{
    [Test] public async Task WireRead_CapturesValueSlotRaw_DefersMaterialisation() { throw new System.NotImplementedException("not implemented"); }

    // Behaviour-level pin: a payload with a malformed value slot (e.g. a
    // truncated number string) does not throw at Wire.Read time. The error
    // surfaces only when `.Value` is touched.
    [Test] public async Task WireRead_DoesNotEagerlyDeserialiseValueSlot() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task WireRead_StampsTypeKindFromTypeSlot() { throw new System.NotImplementedException("not implemented"); }

    // Independent #20a — the reflection probe: app/data/Wire.cs:346 private
    // static `LiftDataIfShaped` is gone by name.
    [Test] public async Task LiftDataIfShaped_MethodIsGone() { throw new System.NotImplementedException("not implemented"); }

    // Independent #20b — the behaviour probe: a payload with `name`+`value`
    // keys at the value slot stays as a dict. No shape guess, no double-
    // parse. Two-prong because the method could be renamed without the
    // heuristic actually being removed.
    [Test] public async Task LiftDataIfShaped_BehaviourIsGone() { throw new System.NotImplementedException("not implemented"); }

    // The case `LiftDataIfShaped` covered. After deletion, a genuinely
    // nested Data round-trips because the containing type's reader
    // (e.g. `Signature` rebuilding its Data field) does the
    // reconstruction — not a key-shape guess on the json.
    [Test] public async Task NestedSignedData_RebuiltByContainingTypeReader_NotByKeyGuess() { throw new System.NotImplementedException("not implemented"); }
}
