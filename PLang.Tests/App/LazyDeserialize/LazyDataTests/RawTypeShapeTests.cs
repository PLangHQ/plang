using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

// Decision 3 — raw is `bytes` only where the source is genuinely bytes.
// Text sources stay text. No utf-8 encode/decode tax on the common path
// just to make binary uniform.
public class RawTypeShapeTests
{
    [Test] public async Task Raw_ForTextSource_StoredAsString_NotUtf8Encoded() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Raw_ForBinarySource_StoredAsByteArray() { throw new System.NotImplementedException("not implemented"); }

    // Perf-shape probe: read a text file's bytes off disk, then read it as
    // text — the resulting `_raw` must equal the source string, never a
    // round-trip of `Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(s))`.
    [Test] public async Task Raw_NoUtf8EncodeTax_OnTextRoundTrip() { throw new System.NotImplementedException("not implemented"); }
}
