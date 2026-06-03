using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

// Stage 3 adds a raw backing slot to Data and materialises through the
// reader registry on first touch. The slot's *shape* is what these rows
// pin; materialisation behaviour sits in LazyMaterialisationTests.
public class LazyDataShapeTests
{
    // Reflection probe of the new backing slot. Name is flex (`_raw`,
    // `_bytes`, whatever) — the contract is "a private slot of type
    // `object?` that admits `string` and `byte[]`."
    [Test] public async Task Data_HasRawField_String_Or_ByteArray() { throw new System.NotImplementedException("not implemented"); }

    // Independent #4 — verbatim passthrough demands `_raw` isn't a wire
    // property. The field must be private (not public, not annotated
    // `[Out]`, not picked up by the renderer's Normalize gate). A leak
    // there grows a new key on the wire shape that nothing else knows.
    [Test] public async Task Data_RawField_IsPrivate_NotPublicNotOut() { throw new System.NotImplementedException("not implemented"); }

    // Independent #4 companion — end-to-end: Wire.Write of an authored
    // value omits a `raw` key. The renderer's Normalize gate (Data is not
    // enveloped) must not start emitting one.
    [Test] public async Task Data_RawField_NotPickedUpByRendererNormalize() { throw new System.NotImplementedException("not implemented"); }

    // Two-laziness preservation: `_valueFactory`/`DynamicData` is *another*
    // laziness — recompute-on-every-access — and must stay alongside
    // materialise-once-and-cache. Decision: keep both, name the difference.
    [Test] public async Task Data_PreservesExistingValueFactory_AndDynamicData() { throw new System.NotImplementedException("not implemented"); }
}
