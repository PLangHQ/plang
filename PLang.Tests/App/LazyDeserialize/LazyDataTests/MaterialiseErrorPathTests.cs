using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.LazyDeserialize.LazyDataTests;

// "Parse errors move from read-time to touch-time." A malformed payload no
// longer errors when it's read into a raw-backed Data — it errors at first
// touch of `.Value`, surfaced as a Data.Error that names the source.
public class MaterialiseErrorPathTests
{
    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-materr-" + System.Guid.NewGuid().ToString("N")[..8]));

    private static data MalformedJson(global::app.actor.context.@this ctx, string name)
        => data.FromRaw("{ this is not valid json", type.Create("object", "json", context: ctx), ctx, name);

    [Test] public async Task MalformedJson_ErrorsAtFirstTouch_NotAtRead()
    {
        await using var app = NewApp();
        // Constructing the raw-backed Data does NOT throw (read-time is clean)…
        var d = MalformedJson(app.User.Context, "cfg");
        await Assert.That(d.Error).IsNull();
        // …the error fires at first touch of .Value.
        _ = d.Value;
        await Assert.That(d.Error).IsNotNull();
    }

    // Independent #17 — the error names the source variable.
    [Test] public async Task MalformedJson_ErrorNamesTheSource()
    {
        await using var app = NewApp();
        var d = MalformedJson(app.User.Context, "cfg");
        _ = d.Value;
        await Assert.That(d.Error!.Message.Contains("cfg")).IsTrue();
    }

    // OBP rule #9 — the failure is a Data.Error, not a thrown exception out of
    // the courier (reading .Value does not throw).
    [Test] public async Task Materialise_Failure_SurfacedAs_DataError_NotThrown_ToCourier()
    {
        await using var app = NewApp();
        var d = MalformedJson(app.User.Context, "cfg");
        object? v = d.Value; // must not throw
        await Assert.That(v).IsNull();
        await Assert.That(d.Error).IsNotNull();
    }
}
