using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using type = global::app.type.@this;

namespace PLang.Tests.App.LazyDeserialize.ReaderRegistryTests;

// Case 2b — re-typing an ALREADY-BUILT value to a declared type (the one thing
// that does not dissolve into the from-raw source path). Three live sites hand
// a materialized, wrong-typed value to construction (Declare, validateResponse,
// set's type-differs fall-through). The engine is the type's 2-arg Convert hook
// applied to the built item. These pins guard the behavior the ctor flip
// (Stage 3) and the caller reroute (Stage 4) depend on:
//   - a built value that CAN become the type → converts.
//   - a built value that CANNOT → fails (an Error Data), NOT a silent hold.
// The failure pin is the build-time safety net: validateResponse relies on this
// convert failing so "abc" as number is caught at build, never held-and-passed.
public class ConvertBuiltValueTests
{
    private static global::app.@this NewApp() => new(System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "plang-convert2b-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test] public async Task ConvertBuiltText_ToNumber_Succeeds()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var built = new global::app.type.item.text.@this("5");
        // Create re-types a built leaf directly (leaf branch) — eager, no Data wrapper.
        var result = type.Create("number", null, context: ctx).Create(built, ctx);
        await Assert.That(result).IsTypeOf<global::app.type.item.number.@this>();
        await Assert.That(((global::app.type.item.number.@this)result).Clr<long>()).IsEqualTo(5L);
    }

    [Test] public async Task ConvertBuiltText_BadNumber_Throws_NotHeld()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var built = new global::app.type.item.text.@this("abc");
        // The build-time safety net: a bad literal must THROW (the throw boundary),
        // never be silently held as the original text (which would pass validation).
        // A throw rides MaterializeFailed at the source boundary; validateResponse
        // catches it to record a build error.
        await Assert.That(() => type.Create("number", null, context: ctx).Create(built, ctx))
            .Throws<System.Exception>();
    }

    [Test] public async Task ConvertBuiltValue_AlreadyType_RoundTrips()
    {
        // A built value re-typed to its OWN type round-trips to an equal value
        // (Stage 3's case 2a holds this without a re-convert; the engine must at
        // least not corrupt it).
        await using var app = NewApp();
        var ctx = app.User.Context;
        var built = ((global::app.type.item.number.@this)(5L));
        var result = type.Create("number", null, context: ctx).Create(built, ctx);
        await Assert.That(((global::app.type.item.number.@this)result).Clr<long>()).IsEqualTo(5L);
    }
}
