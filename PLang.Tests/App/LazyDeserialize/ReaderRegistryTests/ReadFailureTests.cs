using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

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
    [Test] public async Task Read_OfMalformedJson_ProducesError_NotThrow() { throw new System.NotImplementedException("not implemented"); }

    // Registry-level "no entry" path: Of(...) returns null and the caller
    // surfaces `TypeUnknown` (or whatever the chosen error key is). The
    // dispatch itself does not throw; the caller chooses to convert null
    // into a typed error.
    [Test] public async Task Read_OfTypeUnknownToReader_ReturnsNullDelegate() { throw new System.NotImplementedException("not implemented"); }

    // The end-to-end shape: a Read failure inside a courier (variable
    // memory rewriting a Data on its way through) reaches the leaf as a
    // Data.Error, never as a thrown exception bubbling out of the courier.
    [Test] public async Task Read_WrappedAsTaskFailure_NeverEscapesToCourier() { throw new System.NotImplementedException("not implemented"); }
}
