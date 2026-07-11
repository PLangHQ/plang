using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.LazyDeserialize.AccessResolutionTests;

// Property access (`%x!prop%`) reads from Data.Properties; the value is
// never touched. Status checks on an http response, for example, must
// not materialise the body.
public class PropertyAccessTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create(
        "/tmp/PropertyAccessTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Test] public async Task PropertyRead_ReadsFromProperties_NotValue()
    {
        var ctx = _app.User.Context;
        var d = global::PLang.Tests.Shared.Make.FromRaw("{\"big\":\"body\"}", type.Create("object", "json", context: ctx), ctx, "cfg");
        d.Properties["status"] = 200;
        var status = await d.Get("!status");
        await Assert.That((await status.Value())?.ToString()).IsEqualTo("200");
    }

    [Test] public async Task PropertyRead_NeverMaterialisesValue()
    {
        var ctx = _app.User.Context;
        var d = global::PLang.Tests.Shared.Make.FromRaw("{\"big\":\"body\"}", type.Create("object", "json", context: ctx), ctx, "cfg");
        d.Properties["status"] = 200;
        _ = (await (await d.Get("!status")).Value());       // read the property
        await Assert.That(d.MaterializeCount()).IsEqualTo(0); // body untouched
    }
}
