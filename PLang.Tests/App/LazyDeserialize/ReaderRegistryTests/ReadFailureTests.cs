using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.LazyDeserialize.ReaderRegistryTests;

// Failure shape. A `Read` failure must produce an error rather than throw
// into a courier — the OBP courier rule says only leaves touch `.Value`,
// and a thrown exception from inside the reader would propagate up through
// every courier that holds the Data on its way back. Errors travel as
// `Data.Error`; the boundary materialisation surfaces them.
public class ReadFailureTests
{
    // Malformed JSON, malformed iso8601 duration, etc. — each must produce
    // a typed Data error, not an uncaught exception.
    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-readfail-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test] public async Task Read_OfMalformedJson_ProducesError_NotThrow()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = data.FromRaw("{not valid json", type.Create("object", "json", context: ctx), ctx, "bad");
        // Touch must NOT throw — the failure is cached as Data.Error.
        var v = d.Value;
        await Assert.That(v).IsNull();
        await Assert.That(d.Error).IsNotNull();
        await Assert.That(d.Error!.Message).Contains("bad"); // names the source
    }

    // Registry-level "no entry" path: Of(...) returns null and the caller
    // surfaces `TypeUnknown` (or whatever the chosen error key is). The
    // dispatch itself does not throw; the caller chooses to convert null
    // into a typed error.
    [Test] public async Task Read_OfTypeUnknownToReader_ReturnsNullDelegate()
    {
        // Registry-level "no entry" path: Of(...) returns null; the dispatch
        // itself does not throw. The caller turns null into a typed error.
        var r = new global::app.type.reader.@this();
        await Assert.That(r.Of("no-such-type", "json")).IsNull();
    }

    // The end-to-end shape: a Read failure inside a courier (variable
    // memory rewriting a Data on its way through) reaches the leaf as a
    // Data.Error, never as a thrown exception bubbling out of the courier.
    [Test] public async Task Read_WrappedAsTaskFailure_NeverEscapesToCourier()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = data.FromRaw("{not valid json", type.Create("object", "json", context: ctx), ctx, "bad");

        // A courier (variable memory) holds and relays the Data without touching
        // its value — no parse, so no throw escapes the courier.
        ctx.Variable.Set("bad", d);
        var relayed = ctx.Variable.Get("bad")!;
        await Assert.That(relayed.MaterializeCount).IsEqualTo(0); // courier never materialized

        // Only the leaf touch materializes — and it surfaces an error, never throws.
        await Assert.That(relayed.Value).IsNull();
        await Assert.That(relayed.Error).IsNotNull();
    }
}
