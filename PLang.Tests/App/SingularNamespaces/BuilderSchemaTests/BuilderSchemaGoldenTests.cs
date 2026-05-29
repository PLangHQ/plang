using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

// Batch G — Integration cut 3 (Stage 4): the builder LLM schema must be byte-identical
// before vs after Entry-fold.  Stage 4 was NOT executed in coder v1 — these stay deferred.
public class BuilderSchemaGoldenTests
{
    [Test] public async Task BuilderCatalog_ForFixedTypeSet_RendersByteIdentical_BeforeAndAfterEntryFold()
        => Assert.Fail("Stage 4 deferral — Entry fold");

    [Test] public async Task BuilderRender_ReadsFromTypeEntity_NotFromParallelEntryStruct()
        => Assert.Fail("Stage 4 deferral — Render reads off entity");
}
