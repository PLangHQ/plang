using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.IntegrationCutsTests;

// Cut 4 — `get http` against a json endpoint. Status reads via properties
// (eager); the body materialises only on navigation (lazy). And
// `http.response.@this` is gone — the result is plain Data (Decision 6).
public class Cut4_HttpBodyLazyMetadataEager
{
    [Test] public async Task Cut4_StatusRead_DoesNotMaterialiseBody() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Cut4_FieldRead_MaterialisesBody() { throw new System.NotImplementedException("not implemented"); }

    // The strict deletion probe applied end-to-end: after a real
    // `http.get` runs, the returned Data is `app.data.@this` (or a
    // generic of it), never `app.http.response.@this`.
    [Test] public async Task Cut4_HttpResponseTypeIsGone() { throw new System.NotImplementedException("not implemented"); }
}
