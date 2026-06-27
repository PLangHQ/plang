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
        => new plang(global::PLang.Tests.TestApp.SharedContext).Deserialize(new plang(global::PLang.Tests.TestApp.SharedContext).Serialize(d).Peek()!.ToString()!);   // Deserialize returns the reconstruction itself

    // Typed value-slot deferral: a shape-typed (object/table) value rides as raw
    // and materializes only on touch. Scoped to object/table so scalars/domain/
    // dict<…> values keep their eager path.
    [Test] public async Task WireRead_CapturesValueSlotRaw_DefersMaterialisation()
    {
        var d = data.FromRaw("{\"a\":1}", global::app.type.@this.Create("object", "json"));
        d.Name = "cfg";
        var back = RoundTrip(d);
        await Assert.That(back.HasRaw).IsTrue();
        await Assert.That(back.MaterializeCount()).IsEqualTo(0);
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
        var back = new plang(global::PLang.Tests.TestApp.SharedContext).Deserialize(wire);
        await Assert.That(back.HasRaw).IsTrue();
        await Assert.That(back.MaterializeCount()).IsEqualTo(0);
    }

    // Testable now (eager): a typed Data round-trips its type/kind through the
    // type slot.
    [Test] public async Task WireRead_StampsTypeKindFromTypeSlot()
    {
        // The wire carries {number, kind:int}; the read honors a value's kind when
        // a Context is present (the realistic runtime path — the context-less
        // fallback can't resolve a kind and lifts the bare JSON number as long).
        await using var app = global::PLang.Tests.TestApp.Create("/test");
        var ctx = app.User.Context;
        var serializer = new plang(ctx);
        var d = app.Ok(5);                 // number / int derived
        d.Name = "n";
        var back = serializer.Deserialize(serializer.Serialize(d).Peek()!.ToString()!);
        back.Context = ctx;
        await Assert.That(back.Type.Name).IsEqualTo("number");
        await Assert.That(back.Kind).IsEqualTo("int");
    }

    // The value-slot lift machinery is demolished: a single json-entry parse
    // decodes the value slot in ONE pass (scalar wrapper / native container with
    // raw slots / reconstructed Data for a marked slot). The behavior the lift
    // used to provide — a nested marked Data round-trips as a Data — is covered
    // by the two tests below; the old per-shape lift no longer exists.
    [Test] public async Task LiftDataIfShaped_IsDemolished()
    {
        var m = typeof(global::app.data.Wire).GetMethod("LiftDataIfShaped",
            BindingFlags.NonPublic | BindingFlags.Static);
        await Assert.That(m).IsNull();
    }

    // Retired: nested Data (Data-as-a-value) is not a supported shape — only the
    // SetValueDirect courier produced it, and that bypass is now guarded. The
    // wire never carries a nested-Data envelope.
}
