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
    // The raw slot dissolved off Data — the undecoded source form lives ON the
    // type that owns it (source/file/url), private there. Data carries one
    // typed instance and nothing beside it.
    [Test] public async Task Data_HasRawField_String_Or_ByteArray()
    {
        var f = typeof(data).GetField("_raw", BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(f).IsNull();
        var sourceRaw = typeof(global::app.type.item.source)
            .GetField("_raw", BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(sourceRaw).IsNotNull();
        await Assert.That(sourceRaw!.IsPrivate).IsTrue();
    }

    // Independent #4 — the source's raw slot carries no wire-shaping
    // attribute; the renderer's Normalize gate must never pick it up.
    [Test] public async Task Data_RawField_IsPrivate_NotPublicNotOut()
    {
        var f = typeof(global::app.type.item.source)
            .GetField("_raw", BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(f!.IsPrivate).IsTrue();
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

    // Factory-lazy on Data is gone — lazy is the type's job. A computed value
    // is an item whose own door answers fresh at every use and is never kept.
    [Test] public async Task Data_PreservesExistingValueFactory_AndDynamicData()
    {
        var factoryOverload = typeof(data).GetMethods()
            .FirstOrDefault(m => m.Name == "SetValue"
                && m.GetParameters() is [{ ParameterType.IsGenericType: true } p]
                && p.ParameterType.GetGenericTypeDefinition() == typeof(System.Func<>));
        await Assert.That(factoryOverload).IsNull();

        int calls = 0;
        var d = new data("f", new global::app.type.item.computed(() => { calls++; return 42; }));
        await Assert.That((await d.Value())?.ToString()).IsEqualTo("42");
        await Assert.That((await d.Value())?.ToString()).IsEqualTo("42");
        // Fresh at every use — a computed answer is never kept.
        await Assert.That(calls).IsEqualTo(2);
    }
}
