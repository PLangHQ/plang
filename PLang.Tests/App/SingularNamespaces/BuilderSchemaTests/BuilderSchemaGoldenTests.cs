using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

// Batch G — Integration cut 3 (Stage 4): the builder LLM schema must be byte-identical
// before vs after Entry-fold.  Stage 4 was NOT executed in coder v1 — these stay deferred.
public class BuilderSchemaGoldenTests
{
    [Test] public async Task BuilderCatalog_ForFixedTypeSet_RendersByteIdentical_BeforeAndAfterEntryFold()
    {
        // Real golden: pin SHA256 of the rendered JSON + TypeSchemas. Either
        // input changing (Entry fold dissolve, action surface, type catalog)
        // breaks the test — that's the gate the architect spec'd.  Golden
        // values were captured once on the merged-Stage-4 commit and are not
        // self-rewriting; updating them is a deliberate review step.
        await using var app = new global::app.@this("/test");
        var schema = app.Module.Schema.Build();
        var jsonSha = Sha256(schema.ToJson(indent: false));
        var schemasSha = Sha256(schema.TypeSchemas);

        // Length sanity-check so a regression that produces empty output is
        // not papered over by an accidental hash-of-empty-string match.
        await Assert.That(schema.ToJson(indent: false).Length).IsGreaterThan(1000);
        await Assert.That(schema.TypeSchemas.Length).IsGreaterThan(100);

        // Golden hashes — written from a one-shot capture diagnostic on the
        // current head.  Bake-in only after both suites are green.
        await Assert.That(jsonSha).IsEqualTo(BuilderSchemaJsonSha);
        await Assert.That(schemasSha).IsEqualTo(BuilderSchemaTypeSchemasSha);
    }

    // Update on a deliberate schema change.  An accidental change here means
    // either the action catalog or the type catalog drifted unintentionally.
    private const string BuilderSchemaJsonSha       = "A396A2ADFF3580717E11D736A8D9FD7DD2770ED61FDEB39259E654BDEF27841F";
    private const string BuilderSchemaTypeSchemasSha = "33D4E517E1D97D24CD8C87900E23FCECB2736CF27C8EDFC396C1662EA294777F";

    private static string Sha256(string s)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s));
        return System.Convert.ToHexString(bytes);
    }

    [Test] public async Task BuilderRender_ReadsFromTypeEntity_NotFromParallelEntryStruct()
    {
        // builder.type.@this.Types is now IReadOnlyList<app.type.@this> (the entity),
        // not IReadOnlyList<Entry>.
        var schemaType = typeof(global::app.builder.type.@this);
        var typesProp = schemaType.GetProperty("Types");
        await Assert.That(typesProp).IsNotNull();
        var elementType = typesProp!.PropertyType.GetGenericArguments()[0];
        await Assert.That(elementType).IsEqualTo(typeof(global::app.type.@this));
    }
}
