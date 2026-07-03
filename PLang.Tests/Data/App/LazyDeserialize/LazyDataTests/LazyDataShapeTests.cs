using System.Linq;
using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

// Stage 3 adds a raw backing slot to Data and materialises through the
// reader registry on first touch.
public class LazyDataShapeTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create(
        "/tmp/LazyDataShapeTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    // Independent #4 companion — a serialized Data has no `raw` key; the wire
    // shape stays Data's own four fields.
    [Test] public async Task Data_RawField_NotPickedUpByRendererNormalize()
    {
        var d = _app.Ok("hello");
        d.Name = "greeting";
        var json = (await new global::app.channel.serializer.plang.@this(global::PLang.Tests.TestApp.SharedContext).Serialize(d).Value())!.Clr<string>()!;
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
        var d = _app.Data("f", new global::app.type.item.computed(() => { calls++; return 42; }, _app.User.Context));
        await Assert.That((await d.Value())?.ToString()).IsEqualTo("42");
        await Assert.That((await d.Value())?.ToString()).IsEqualTo("42");
        // Fresh at every use — a computed answer is never kept.
        await Assert.That(calls).IsEqualTo(2);
    }
}
