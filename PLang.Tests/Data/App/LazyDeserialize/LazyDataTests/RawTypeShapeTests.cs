using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

// Decision 3 — raw is `bytes` only where the source is genuinely bytes.
// Text stays text; no utf-8 encode/decode tax on the common path.
public class RawTypeShapeTests
{
    [Test] public async Task Raw_ForTextSource_StoredAsString_NotUtf8Encoded()
    {
        var d = global::PLang.Tests.Shared.Make.FromRaw("hello world", type.Create("text"), global::PLang.Tests.TestApp.SharedContext);
        await Assert.That(d.Raw).IsTypeOf<string>();
        await Assert.That((string)d.Raw!).IsEqualTo("hello world");
    }

    [Test] public async Task Raw_ForBinarySource_StoredAsByteArray()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var d = global::PLang.Tests.Shared.Make.FromRaw(bytes, type.Create("image", "png"), global::PLang.Tests.TestApp.SharedContext);
        await Assert.That(d.Raw).IsTypeOf<byte[]>();
    }

    // No round-trip through UTF8.GetString(UTF8.GetBytes(s)) — the source
    // string is held verbatim (reference-equal).
    [Test] public async Task Raw_NoUtf8EncodeTax_OnTextRoundTrip()
    {
        const string json = "{\"a\":1}";
        var d = global::PLang.Tests.Shared.Make.FromRaw(json, type.Create("object", "json"), global::PLang.Tests.TestApp.SharedContext);
        await Assert.That(object.ReferenceEquals(d.Raw, json)).IsTrue();
    }
}
