using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;

namespace PLang.Tests.App.LazyDeserialize.AccessResolutionTests;

// `as <type>` reads toward that type. This is how a developer resolves a
// type-unknown value when navigation would otherwise error.
public class AsCastTests
{
    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-ascast-" + System.Guid.NewGuid().ToString("N")[..8]));

    // A type-unknown json string `as object` (the object/json shape) reads
    // toward the tree and becomes navigable by key.
    [Test] public async Task AsJson_OnTypeUnknownValue_ReadsTowardJson()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = data.Ok("{\"port\":8080}");
        var asObj = d.As("object/json", ctx); // explicit cast reads toward json via the reader
        await asObj.IsSuccess();
        await Assert.That((await (await asObj.GetChild("port")).Value())?.ToString()).IsEqualTo("8080");
    }

    // Already-typed value: `as object` on a value that's already a tree
    // returns a navigable tree (no corruption). Contract chosen: re-reads
    // toward the type idempotently — an already-correct value survives.
    [Test] public async Task AsType_OnAlreadyTypedValue_NoOp_OrRetypes()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var dict = new System.Collections.Generic.Dictionary<string, object?> { ["port"] = 8080L };
        var d = data.Ok(dict);
        var r = d.As("object", ctx);
        await r.IsSuccess();
        await Assert.That((await (await r.GetChild("port")).Value())?.ToString()).IsEqualTo("8080");
    }
}
