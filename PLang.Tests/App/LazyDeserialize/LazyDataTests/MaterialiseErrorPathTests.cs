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
        _ = (await d.Value());
        await Assert.That(d.Error).IsNotNull();
    }

    // Independent #17 — the error names the source variable.
    [Test] public async Task MalformedJson_ErrorNamesTheSource()
    {
        await using var app = NewApp();
        var d = MalformedJson(app.User.Context, "cfg");
        _ = (await d.Value());
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

    // The error stamped during materialization is surfaced at the navigation
    // seam — `%cfg.host%` on malformed JSON returns the MaterializeFailed error,
    // not a generic NotFound. Without this, the developer chases a "not found"
    // ghost instead of the parse error that explains why.
    [Test] public async Task Navigation_OnMalformedJson_SurfacesMaterializeFailed_NotNotFound()
    {
        await using var app = NewApp();
        var d = MalformedJson(app.User.Context, "cfg");
        var child = d.GetChild("host");
        await Assert.That(child.Error).IsNotNull();
        await Assert.That(child.Error!.Key).IsEqualTo("MaterializeFailed");
        await Assert.That(child.Error!.Message.Contains("cfg")).IsTrue();
    }

    // The set-path twin of the navigation seam — `set %cfg.host% = ...` on a
    // malformed-JSON parent surfaces MaterializeFailed, not NotFound. Read and
    // write reach the parent through the same materialize; the fix shape is
    // identical, so without this the write path could regress with no red test.
    [Test] public async Task SetPath_OnMalformedJson_SurfacesMaterializeFailed_NotNotFound()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        ctx.Variable.Set(MalformedJson(ctx, "cfg"));

        var result = ctx.Variable.Set("cfg.host", "value");

        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("MaterializeFailed");
        await Assert.That(result.Error!.Message.Contains("cfg")).IsTrue();
    }

    // The deeper set-path — `set %cfg.a.host% = ...` reaches the parent through an
    // intermediate GetChild, which materializes the malformed root and arrives
    // already-failed. The same MaterializeFailed must surface, not NotFound.
    [Test] public async Task SetPath_NestedOnMalformedJson_SurfacesMaterializeFailed_NotNotFound()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        ctx.Variable.Set(MalformedJson(ctx, "cfg"));

        var result = ctx.Variable.Set("cfg.a.host", "value");

        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("MaterializeFailed");
        await Assert.That(result.Error!.Message.Contains("cfg")).IsTrue();
    }
}
