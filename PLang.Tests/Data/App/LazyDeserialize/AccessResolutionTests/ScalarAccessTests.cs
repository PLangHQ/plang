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
    // A binary value is NOT guessed as text, even when the bytes happen to be
    // valid UTF-8 — its face stays bytes (decode to text is the explicit `as text`).
    [Test] public async Task Scalar_BytesValue_StaysBytes_NotGuessedAsText()
    {
        var d = data.FromRaw(Encoding.UTF8.GetBytes("héllo"), type.Create("bytes"), global::PLang.Tests.TestApp.SharedContext); // 6 valid-UTF-8 bytes
        await Assert.That(d.Peek()?.ToString()).IsEqualTo("(6 bytes)");
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
    }

    // Bytes stay bytes regardless of content — no UTF-8 guess either way.
    [Test] public async Task Scalar_BytesValue_StaysBytes_WhenInvalidUtf8()
    {
        byte[] invalid = { 0xFF, 0xFE, 0x00, 0x80 };
        var d = data.FromRaw(invalid, type.Create("bytes"), global::PLang.Tests.TestApp.SharedContext);
        await Assert.That(d.Peek()?.ToString()).IsEqualTo("(4 bytes)");
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
    }

    [Test] public async Task Scalar_TextValue_ReturnsString_NoStructuredParse()
    {
        const string json = "{\"port\":8080}";
        var d = data.FromRaw(json, type.Create("object", "json"), global::PLang.Tests.TestApp.SharedContext);
        await Assert.That(d.Peek()?.ToString()).IsEqualTo(json); // the raw string, not a dict
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);       // never parsed
    }

    // Variable interpolation/output uses the scalar form: a bare `%cfg%` of a
    // lazily-read object renders the raw json string (no parse), while a dotted
    // `%cfg.port%` navigates and materializes the field.
    [Test] public async Task Scalar_VariableInterpolation_BareVarIsRaw_DottedNavigates()
    {
        await using var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-scalarvar-" + System.Guid.NewGuid().ToString("N")[..8]));
        var ctx = app.User.Context;
        ctx.Variable.Set("cfg", data.FromRaw("{\"port\":8080}", type.Create("object", "json", context: ctx), ctx, "cfg"));

        await Assert.That(await ctx.Variable.Resolve("%cfg%")).IsEqualTo("{\"port\":8080}");
        await Assert.That(await ctx.Variable.Resolve("%cfg.port%")).IsEqualTo("8080");
    }
}
