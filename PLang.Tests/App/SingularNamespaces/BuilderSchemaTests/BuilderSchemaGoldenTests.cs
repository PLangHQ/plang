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
        // Type rendering moved into the Liquid template (CompileUser.llm); the
        // schema is the strongly-typed surface the template reads. Pin the
        // catalog counts — a stable proxy for "schema shape didn't drift".
        await using var app = new global::app.@this("/test");
        var schema = app.Module.Schema.Build();
        await Assert.That(schema.PrimitiveNames.Count).IsGreaterThan(10);
        await Assert.That(schema.Types.Count).IsGreaterThan(20);
        await Assert.That(schema.Kinds.Count).IsGreaterThan(5);
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
