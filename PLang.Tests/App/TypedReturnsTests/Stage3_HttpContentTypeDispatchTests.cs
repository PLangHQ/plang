namespace PLang.Tests.App.TypedReturnsTests;

// Stage 3 — HTTP runtime Content-Type body dispatch.
// Architect: .bot/typed-action-returns/architect/stages.md (Stage 3, item 3)
// Plan: .bot/typed-action-returns/architect/plan.md (A.3)

public class Stage3_HttpContentTypeDispatchTests
{
    [Test]
    public async Task BodyDispatch_ApplicationJson_YieldsJsonNode()
        // Mock provider returns Content-Type "application/json" with `{"a":1}` — Response.Body is JsonNode where ["a"]==1.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BodyDispatch_TextHtml_YieldsString()
        // Content-Type "text/html" → Response.Body is a string with the raw body.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BodyDispatch_ImagePng_YieldsByteArray()
        // Content-Type "image/png" → Response.Body is byte[] verbatim.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BodyDispatch_MissingContentType_FallsBackToByteArray()
        // No Content-Type header → Body is byte[].
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BodyDispatch_TextUnknownSubtype_FallsBackToString()
        // Content-Type "text/x-unknown" → Body is string (text/* fallback rule).
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BodyDispatch_TextCsv_YieldsCsv_IfMaterializerRegistered()
        // Content-Type "text/csv" — if a Csv materializer is registered, Body is the materialized Csv type; else string fallback.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BodyDispatch_UsesSerializerRegistry_GetByContentType()
        // Verify dispatch path goes through Serializers.GetByContentType — stub the registry, expect the lookup.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task HttpDownload_BodyDispatch_NotApplied()
        // http.download writes to disk; the Content-Type dispatch path is NOT triggered.
        => Assert.Fail("Not implemented");
}
