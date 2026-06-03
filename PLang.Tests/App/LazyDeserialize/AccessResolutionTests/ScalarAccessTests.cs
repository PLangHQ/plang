using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.AccessResolutionTests;

// Stage 5 — the kind of access decides materialisation. Scalar / output
// access (`%x%`, `write out %x%`):
//   if `_raw` is bytes, decode utf-8 (stay bytes if it doesn't decode);
//   if text, the string. No structured parse on scalar access.
public class ScalarAccessTests
{
    [Test] public async Task Scalar_BytesValue_DecodesUtf8_WhenValidUtf8() { throw new System.NotImplementedException("not implemented"); }

    // Non-utf-8 bytes stay bytes — silently corrupting them into a
    // mojibake string is worse than leaving them as bytes.
    [Test] public async Task Scalar_BytesValue_StaysBytes_WhenInvalidUtf8() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task Scalar_TextValue_ReturnsString_NoStructuredParse() { throw new System.NotImplementedException("not implemented"); }
}
