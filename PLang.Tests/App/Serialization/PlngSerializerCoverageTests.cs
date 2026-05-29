namespace PLang.Tests.App.Serialization;

// plang-types — Stage 2
// PLNG_SerializerCoverage build gate (error severity): every [PlangType] must have
// a Default.cs serializer OR cover every registered format token. The gate makes the
// runtime writer lookup TOTAL for built-in types — no runtime "unknown format" branch.
//
// DEFERRED — implementing the build gate needs a Roslyn analyzer / source-generator
// diagnostic with error severity in PLang.Generators. Stage 2 ships the runtime
// dispatch + reflection-driven discovery; the build-time enforcement is a
// follow-up. The runtime safety is currently the writer's "RendererLookupMissed"
// throw (see app/channels/serializers/json/writer.cs case TypedValueNode), which
// makes a missing renderer surface immediately on first emit rather than silently
// dropping the value — but the architect's intent is the BUILD fails before that.

public class PlngSerializerCoverageTests
{
    [Test] public async Task Coverage_PlangTypeWithDefaultCs_PassesGate()
    {
        // path has app/types/path/serializer/Default.cs ⇒ the would-be gate
        // accepts it. Today's runtime equivalent: renderers.Has("path") == true.
        var r = new global::app.type.renderer.@this();
        await Assert.That(r.Has("path")).IsTrue();
    }

    // Build-time gate placeholders (PLNG003 analyzer, per-format coverage,
    // synthetic-fixture diagnostics) were removed; the deferral is tracked in
    // Documentation/v0.2/todos.md "PLNG003: build-time serializer-coverage gate".
    // When the analyzer ships, the real tests land alongside it.
}
