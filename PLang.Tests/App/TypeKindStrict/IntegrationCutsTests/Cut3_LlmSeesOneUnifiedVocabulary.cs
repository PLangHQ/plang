using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.IntegrationCutsTests;

// Integration cut 3 — the LLM sees one unified type vocabulary.
// Three surfaces collapse into one cached, generated block; the per-step user
// message stops carrying its own primitive-types line; the `type` parameter is
// taught as a constructor. Verified by capturing a fresh build trace and reading
// the actual prompt the LLM received.

public class Cut3_LlmSeesOneUnifiedVocabulary
{
    [Test] public async Task FreshCompile_CachedSystemPromptContainsAdvertisedKindsBlock()
    {
        // Force `plang '--build={"files":["Tests/.../Some.goal"],"cache":false}'`,
        // read the trace under `.build/traces/<id>/`, find the cached system
        // prompt sent on the Compile step, and assert it contains:
        //   - "number — kinds: int | long | decimal | double"
        //   - "text — kind = extension"
        //   - "type(name, kind?, strict?)" (or the rendered constructor signature)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task FreshCompile_PerStepUserMessage_DoesNotContainPrimitiveTypesLine()
    {
        // Same trace; the per-step user message must NOT contain the literal
        // "Primitive types:" line — that content lives in the cached system
        // prompt now.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task FreshCompile_PerStepUserMessage_CarriesOnlyDomainTypes()
    {
        // The per-step `Catalog types referenced by this step's actions:`
        // block still appears, but only carries domain/record/enum types
        // (e.g. `path`, `llmmessage`), not primitives.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
