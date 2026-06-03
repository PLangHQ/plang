using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// The http channel — bidirectional: write the request, read the response.
// Body becomes the lazy value (type/kind from Content-Type); status,
// headers, duration become Data properties. `http.response.@this` deletes
// (Decision 6).
public class HttpChannelTests
{
    [Test] public async Task HttpChannel_IsBidirectional() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task HttpGet_OpensHttpChannel_StopsContentTypeDeserialize() { throw new System.NotImplementedException("not implemented"); }

    // Independent #12 — the strict deletion probe by absolute name.
    // `Assembly.GetType("app.http.response.@this")` returns null. A
    // surface-level rename that left the type in place would slip past
    // behaviour-only tests.
    [Test] public async Task HttpResponse_TypeDeleted_ByAbsoluteName() { throw new System.NotImplementedException("not implemented"); }

    // Independent #13 — http.get's `Run` signature no longer references
    // `app.http.response.@this`. Reflection on the action handler's
    // method signature. Catches the case where the type deletes but a
    // dispatch metadata still references it via Task<…>.
    [Test] public async Task HttpGet_Run_ReturnTypeIsData_NotHttpResponse() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task HttpResponse_BodyIsLazyValue_StatusHeadersDurationAreProperties() { throw new System.NotImplementedException("not implemented"); }

    // Independent #19 — the per-action C#-side pin for the body-untouched
    // contract on property reads. Probe-based: the body's `_value` stays
    // null after `%response!status%`.
    [Test] public async Task HttpStatusRead_DoesNotMaterialiseBody() { throw new System.NotImplementedException("not implemented"); }
}
