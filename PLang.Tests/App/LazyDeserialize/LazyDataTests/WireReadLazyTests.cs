using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using plang = global::app.channel.serializer.plang.@this;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

// Architect call #3 (2026-06-03): Wire.Read defers (captures _raw) only when
// the type slot is present; untyped slots stay eager. LiftDataIfShaped is kept
// LEAN — envelope-recognition stays (a leaf serializer recognizing its own
// canonical shape, not banned format-sniffing), only the GetRawText double-
// parse is dropped. Signing recanonicalizes (no "verify on raw").
public class WireReadLazyTests
{
    private static data RoundTrip(data d)
        => (data)(plang.ContextLessFallback.Deserialize(plang.ContextLessFallback.Serialize(d).Materialize()!.ToString()).Materialize())!;

    // Typed value-slot deferral: a shape-typed (object/table) value rides as raw
    // and materializes only on touch. Scoped to object/table so scalars/domain/
    // dict<…> values keep their eager path.
    [Test] public async Task WireRead_CapturesValueSlotRaw_DefersMaterialisation()
    {
        var d = data.FromRaw("{\"a\":1}", global::app.type.@this.Create("object", "json"));
        d.Name = "cfg";
        var back = RoundTrip(d);
        await Assert.That(back.HasRaw).IsTrue();
        await Assert.That(back.MaterializeCount).IsEqualTo(0);
        await Assert.That(back.Type.Name).IsEqualTo("object");
    }

    // Deferral means the value slot is NOT eagerly parsed at read time — a value
    // whose content would fail a structured parse reads back without throwing (it
    // errors only on a later touch).
    [Test] public async Task WireRead_DoesNotEagerlyDeserialiseValueSlot()
    {
        // value is a valid json *string* token whose content is malformed json,
        // typed {object, json} — read defers it, no throw.
        const string wire = "{\"name\":\"x\",\"type\":{\"name\":\"object\",\"kind\":\"json\"},\"value\":\"{not json\"}";
        var back = (data)(await plang.ContextLessFallback.Deserialize(wire).Value())!;
        await Assert.That(back.HasRaw).IsTrue();
        await Assert.That(back.MaterializeCount).IsEqualTo(0);
    }

    // Testable now (eager): a typed Data round-trips its type/kind through the
    // type slot.
    [Test] public async Task WireRead_StampsTypeKindFromTypeSlot()
    {
        var d = data.Ok(5);                 // number / int derived
        d.Name = "n";
        var back = RoundTrip(d);
        await Assert.That(back.Type.Name).IsEqualTo("number");
        await Assert.That(back.Kind).IsEqualTo("int");
    }

    // Flipped (architect call #3): LiftDataIfShaped is KEPT lean, not deleted —
    // pin that the private static method still exists.
    [Test] public async Task LiftDataIfShaped_KeptLean_StillExists()
    {
        var m = typeof(global::app.data.Wire).GetMethod("LiftDataIfShaped",
            BindingFlags.NonPublic | BindingFlags.Static);
        await Assert.That(m).IsNotNull();
    }

    // Flipped: envelope-recognition STAYS. A nested Data in a bare untyped value
    // slot round-trips as a Data (not degraded to a dict) via lean recognition.
    [Test] public async Task NestedDataInUntypedSlot_RecognizedAsData_NotDict()
    {
        var inner = data.Ok("hello");
        inner.Name = "inner";
        var outer = data.Ok(inner);
        outer.Name = "outer";
        var back = RoundTrip(outer);
        await Assert.That(back.Value).IsTypeOf<data>();
        await Assert.That((await ((data)(await back.Value())!).Value())?.ToString()).IsEqualTo("hello");
    }

    // The case LiftDataIfShaped covers: a genuinely nested Data round-trips and
    // stays a reconstructed Data (so its signature would reach signing.verify),
    // via the lean envelope-recognition — one parse, no key-shape double-parse.
    // (Full sign→wire→verify is integration Cut 3.)
    [Test] public async Task NestedSignedData_RebuiltByContainingTypeReader_NotByKeyGuess()
    {
        var inner = data.Ok("payload");
        inner.Name = "inner";
        var outer = data.Ok(inner);
        outer.Name = "outer";
        var back = RoundTrip(outer);
        await Assert.That(back.Value).IsTypeOf<data>();
    }
}
