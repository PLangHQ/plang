using System.Text;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.LazyDeserialize.AccessResolutionTests;

// Stage 5 — the kind of access decides materialisation. Scalar / output
// access (`%x%`, `write out %x%`):
//   if `_raw` is bytes, decode utf-8 (stay bytes if it doesn't decode);
//   if text, the string. No structured parse on scalar access.
public class ScalarAccessTests
{
    [Test] public async Task Scalar_BytesValue_DecodesUtf8_WhenValidUtf8()
    {
        var d = data.FromRaw(Encoding.UTF8.GetBytes("héllo"), type.Create("bytes"));
        await Assert.That(d.ScalarValue).IsEqualTo((object)"héllo");
        await Assert.That(d.MaterializeCount).IsEqualTo(0);
    }

    // Non-utf-8 bytes stay bytes — silently corrupting them into a
    // mojibake string is worse than leaving them as bytes.
    [Test] public async Task Scalar_BytesValue_StaysBytes_WhenInvalidUtf8()
    {
        byte[] invalid = { 0xFF, 0xFE, 0x00, 0x80 };
        var d = data.FromRaw(invalid, type.Create("bytes"));
        await Assert.That(d.ScalarValue is byte[]).IsTrue();
    }

    [Test] public async Task Scalar_TextValue_ReturnsString_NoStructuredParse()
    {
        const string json = "{\"port\":8080}";
        var d = data.FromRaw(json, type.Create("object", "json"));
        await Assert.That(d.ScalarValue).IsEqualTo((object)json); // the raw string, not a dict
        await Assert.That(d.MaterializeCount).IsEqualTo(0);       // never parsed
    }
}
