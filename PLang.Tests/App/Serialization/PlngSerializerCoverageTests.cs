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
        var r = new global::app.types.renderers.@this();
        await Assert.That(r.Has("path")).IsTrue();
    }

    [Test, Skip("deferred: needs fixture type with all-format serializer folder once PLNG003 gate ships")]
    public async Task Coverage_PlangTypeWithEveryFormatFile_PassesGate_NoDefaultCs()
        => await Assert.That(true).IsTrue();

    [Test, Skip("deferred: needs Roslyn analyzer with PLNG003 diagnostic")]
    public async Task Coverage_PlangTypeWithNoSerializer_FailsBuild()
        => await Assert.That(true).IsTrue();

    [Test, Skip("deferred: needs Roslyn analyzer with PLNG003 diagnostic")]
    public async Task Coverage_PlangTypeMissingOneFormat_WithoutDefault_FailsBuild()
        => await Assert.That(true).IsTrue();

    [Test, Skip("deferred: PLNG003 analyzer not yet shipped")]
    public async Task Coverage_DiagnosticIdIsPLNG_ErrorSeverity()
        => await Assert.That(true).IsTrue();
}
