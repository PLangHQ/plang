using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

// Batch G — Integration cut 3 (Stage 4): the builder LLM schema must be byte-identical before vs after Entry-fold.
// Coder captures the rendered catalog for a fixed type set BEFORE the fold, embeds it as the baseline,
// then re-runs AFTER the fold and asserts equality. If the schema can't be made deterministic, raise to Ingi
// before Stage 4 lands.
//
// IMPORTANT (test-designer note): plang-types shipped `Tests/Types/` and the math/cut suites.
// Coder MUST check for an existing golden to extend before writing a fresh one (architect: test-strategy §3).
public class BuilderSchemaGoldenTests
{
    // The catalog walk stays on type.list.@this (BuildTypeEntries / ComplexSchemas) — its OUTPUT is the contract.
    [Test] public async Task BuilderCatalog_ForFixedTypeSet_RendersByteIdentical_BeforeAndAfterEntryFold()
        => Assert.Fail("Not implemented");

    // After the fold: builder/type/Render.cs reads off the entity, not a parallel Entry struct.
    [Test] public async Task BuilderRender_ReadsFromTypeEntity_NotFromParallelEntryStruct()
        => Assert.Fail("Not implemented");
}
