using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.ReaderRegistryTests;

// Stage 1's "no behavior change" pin. Each type's new `Read` must produce
// the *same* value the old incumbent produced. The parity rows are the
// floor: Stage 1 is a refactor, not a behaviour change, so the parity must
// hold byte/value-identically for the canonical inputs each old converter
// handled.
public class TypeOwnedReadParityTests
{
    [Test] public async Task PathRead_MatchesPriorJsonConverterRead()
    {
        // Canonical inputs: absolute path string, relative path string,
        // http:// path scheme, file:// path scheme. The new path.Read must
        // produce the same path subclass + same scheme + same raw form as
        // app.type.path.JsonConverter.Read did.
        throw new System.NotImplementedException("not implemented");
    }

    [Test] public async Task NumberRead_MatchesPriorConvertOutput()
    {
        // Parametric across int/long/decimal/double/float. Stage 1 keeps the
        // pre-Stage-2 number model; Stage 2 then extends the tower. Parity
        // here is against the current model only.
        throw new System.NotImplementedException("not implemented");
    }

    [Test] public async Task HashRead_MatchesPriorFromWireOutput()
    {
        // app/module/crypto/type/hash/this.cs:72 — old FromWire.
        throw new System.NotImplementedException("not implemented");
    }

    [Test] public async Task ErrorRead_MatchesPriorErrorWireOutput()
    {
        // app/error/IError.Wire.cs:33 — old ErrorWire.
        throw new System.NotImplementedException("not implemented");
    }

    [Test] public async Task TimeSpanRead_MatchesPriorTimeSpanIso8601Output()
    {
        throw new System.NotImplementedException("not implemented");
    }

    [Test] public async Task ObjectJsonRead_MatchesPriorPlangJsonReaderOutput()
    {
        // The existing System.Text.Json plumbing inside the plang json
        // reader is re-housed, not rewritten (Decision 1). Parity is
        // verbatim for canonical { key: value, list: […], nested: {…} }.
        // Architect 829785fbe — json keeps today's shape (`object`); the
        // entry key is `(object, json)`.
        throw new System.NotImplementedException("not implemented");
    }
}
