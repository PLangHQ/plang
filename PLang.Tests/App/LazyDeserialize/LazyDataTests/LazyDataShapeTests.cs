using System.Linq;
using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

// Stage 3 adds a raw backing slot to Data and materialises through the
// reader registry on first touch.
public class LazyDataShapeTests
{
    private static FieldInfo? RawField()
        => typeof(data).GetField("_raw", BindingFlags.NonPublic | BindingFlags.Instance);

    [Test] public async Task Data_HasRawField_String_Or_ByteArray()
    {
        var f = RawField();
        await Assert.That(f).IsNotNull();
        // object? backing — admits both string (text) and byte[] (binary).
        await Assert.That(f!.FieldType).IsEqualTo(typeof(object));
    }

    // Independent #4 — the raw slot is private and carries no wire-shaping
    // attribute; the renderer's Normalize gate must never pick it up.
    [Test] public async Task Data_RawField_IsPrivate_NotPublicNotOut()
    {
        var f = RawField();
        await Assert.That(f!.IsPrivate).IsTrue();
        // No wire-shaping attribute (Out/Store) — the renderer must never pick it up.
        // (A compiler-emitted [Nullable] on `object?` is fine; only Out/Store matter.)
        bool hasWireAttr = f.GetCustomAttributes(false)
            .Any(a => a.GetType().Name.Contains("Out") || a.GetType().Name.Contains("Store"));
        await Assert.That(hasWireAttr).IsFalse();
    }

    // Independent #4 companion — a serialized Data has no `raw` key; the wire
    // shape stays Data's own four fields.
    [Test] public async Task Data_RawField_NotPickedUpByRendererNormalize()
    {
        var d = data.Ok("hello");
        d.Name = "greeting";
        var json = (await global::app.channel.serializer.plang.@this.ContextLessFallback.Serialize(d).Value())!;
        await Assert.That(json.Contains("\"raw\"")).IsFalse();
        await Assert.That(json.Contains("\"_raw\"")).IsFalse();
    }

    // Two-laziness preservation — the recompute-on-access factory still works
    // alongside materialise-once-and-cache.
    [Test] public async Task Data_PreservesExistingValueFactory_AndDynamicData()
    {
        var d = new data("f");
        int calls = 0;
        d.SetValue(() => { calls++; return 42; });
        await Assert.That((await d.Value())).IsEqualTo((object)42);
        await Assert.That((await d.Value())).IsEqualTo((object)42); // cached after first compute
        await Assert.That(calls).IsEqualTo(1);
    }
}
