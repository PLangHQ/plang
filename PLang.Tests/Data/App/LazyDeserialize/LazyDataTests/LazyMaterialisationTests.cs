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
        var d = global::PLang.Tests.Shared.Make.FromRaw("5", type.Create("number", "int", context: ctx), ctx, "n");
        await Assert.That(global::app.type.item.@this.Lower<long>(await d.Value())).IsEqualTo(5L);
        await Assert.That(d.MaterializeCount()).IsEqualTo(1);
    }

    [Test] public async Task Value_ReturnsValueDirectly_WhenValueSet_AndRawNull()
    {
        await using var app = NewApp();
        var d = app.Ok(5);
        await Assert.That(global::app.type.item.@this.Lower<long>(await d.Value())).IsEqualTo(5L);
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
    }

    // Independent #6 — probe-counted negative: authored .Value never materializes.
    [Test] public async Task Value_AuthoredPath_NeverInvokesReader()
    {
        await using var app = NewApp();
        var d = app.Ok("plain string");
        _ = (await d.Value()); _ = (await d.Value());
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
    }

    // Single storage: the parse MOVES the value — the source form is gone once
    // the Data rebinds to the parsed instance. Verbatim passthrough holds only
    // while untouched.
    [Test] public async Task Value_RawSurvivesMaterialisation()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = global::PLang.Tests.Shared.Make.FromRaw("5", type.Create("number", "int", context: ctx), ctx, "n");
        await Assert.That(d.HasRaw).IsTrue();   // untouched — source-backed
        var v = await d.Value();                // parse rebinds
        await Assert.That(v is global::app.type.item.number.@this).IsTrue();
        await Assert.That(d.HasRaw).IsFalse();  // single storage — raw moved
    }

    // app/data/this.cs — ConvertValue folded into the materialize path; the
    // named method is gone.
    [Test] public async Task ConvertValue_IsRemoved()
        => await Assert.That(typeof(data).GetMethod("ConvertValue", BindingFlags.Public | BindingFlags.Instance)).IsNull();

    [Test] public async Task Navigation_ReadsValueWhichMaterialises()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = global::PLang.Tests.Shared.Make.FromRaw("{\"port\":8080}", type.Create("object", "json", context: ctx), ctx, "cfg");
        await Assert.That((await d.Value())).IsTypeOf<app.type.item.dict.@this>();
        var dict = (app.type.item.dict.@this)(await d.Value())!;
        await Assert.That(dict.Has("port")).IsTrue();
    }

    // Unchanged contract — `%var%` in an authored value is RAW per read.
    [Test] public async Task VarReference_InAuthoredValue_StillResolvesFreshPerRead()
    {
        await using var app = NewApp();
        var d = app.Ok("%x%");
        await Assert.That((await d.Value())?.ToString()).IsEqualTo("%x%");
        await Assert.That((await d.Value())?.ToString()).IsEqualTo("%x%");
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
    }
}
