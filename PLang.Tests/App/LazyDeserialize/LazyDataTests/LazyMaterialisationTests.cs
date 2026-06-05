using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

// Decision 2 — materialisation fires only when `_value` is null and `_raw`
// is set. Authored values populate `_value`, leave `_raw` null, never hit
// the byte path. Which field is set tells you the origin; no mode flag.
public class LazyMaterialisationTests
{
    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-lazymat-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test] public async Task Value_MaterialisesViaReader_WhenValueNull_AndRawSet()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = data.FromRaw("5", type.Create("number", "int", context: ctx), ctx, "n");
        await Assert.That(d.Value).IsEqualTo((object)5);
        await Assert.That(d.MaterializeCount).IsEqualTo(1);
    }

    [Test] public async Task Value_ReturnsValueDirectly_WhenValueSet_AndRawNull()
    {
        var d = data.Ok(5);
        await Assert.That(d.Value).IsEqualTo((object)5);
        await Assert.That(d.MaterializeCount).IsEqualTo(0);
    }

    // Independent #6 — probe-counted negative: authored .Value never materializes.
    [Test] public async Task Value_AuthoredPath_NeverInvokesReader()
    {
        var d = data.Ok("plain string");
        _ = d.Value; _ = d.Value;
        await Assert.That(d.MaterializeCount).IsEqualTo(0);
    }

    [Test] public async Task Value_RawSurvivesMaterialisation()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = data.FromRaw("5", type.Create("number", "int", context: ctx), ctx, "n");
        _ = d.Value;               // materialize
        await Assert.That(d.HasRaw).IsTrue();   // _raw survives
        await Assert.That(d.Raw).IsEqualTo((object)"5");
    }

    // app/data/this.cs — ConvertValue folded into the materialize path; the
    // named method is gone.
    [Test] public async Task ConvertValue_IsRemoved()
        => await Assert.That(typeof(data).GetMethod("ConvertValue", BindingFlags.Public | BindingFlags.Instance)).IsNull();

    [Test] public async Task Navigation_ReadsValueWhichMaterialises()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = data.FromRaw("{\"port\":8080}", type.Create("object", "json", context: ctx), ctx, "cfg");
        d.ForceMaterialize();      // the navigation seam (was ConvertValue)
        await Assert.That(d.Value).IsTypeOf<app.type.dict.@this>();
        var dict = (app.type.dict.@this)d.Value!;
        await Assert.That(dict.Has("port")).IsTrue();
    }

    // Unchanged contract — `%var%` in an authored value is RAW per read.
    [Test] public async Task VarReference_InAuthoredValue_StillResolvesFreshPerRead()
    {
        var d = data.Ok("%x%");
        await Assert.That(d.Value).IsEqualTo((object)"%x%");
        await Assert.That(d.Value).IsEqualTo((object)"%x%");
        await Assert.That(d.MaterializeCount).IsEqualTo(0);
    }
}
