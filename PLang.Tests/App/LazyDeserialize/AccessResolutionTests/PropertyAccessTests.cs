using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.LazyDeserialize.AccessResolutionTests;

// Property access (`%x!prop%`) reads from Data.Properties; the value is
// never touched. Status checks on an http response, for example, must
// not materialise the body.
public class PropertyAccessTests
{
    [Test] public async Task PropertyRead_ReadsFromProperties_NotValue()
    {
        var d = data.FromRaw("{\"big\":\"body\"}", type.Create("object", "json"));
        d.Properties["status"] = 200;
        var status = d.GetChild("!status");
        await Assert.That((await status.Value())).IsEqualTo((object)200);
    }

    [Test] public async Task PropertyRead_NeverMaterialisesValue()
    {
        var d = data.FromRaw("{\"big\":\"body\"}", type.Create("object", "json"));
        d.Properties["status"] = 200;
        _ = (await d.GetChild("!status").Value());       // read the property
        await Assert.That(d.MaterializeCount).IsEqualTo(0); // body untouched
    }
}
