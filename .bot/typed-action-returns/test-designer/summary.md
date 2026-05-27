# test-designer summary — typed-action-returns

**Version:** v1

## What this is

Failing-test contract for Branch A of `typed-action-returns` — five stages that take the LLM builder out of the "(object)" trap by making `Run()` returns strongly typed on the C# side. Architect plan (v3.3) and `stages.md` are locked; this is the test contract the coder bot makes green stage by stage.

## What was done

11 batches presented at the high-level (Stage 0×4, Stage 1×1, Stage 2×2, Stage 3×2, Stage 4×2); user approved all in one pass. Materialized as 11 C# test files under `PLang.Tests/App/TypedReturnsTests/` and 12 PLang `.test.goal` files under `Tests/TestModule/TypedReturns/Stage{0..4}/`. All C# bodies are `Assert.Fail("Not implemented");`; all PLang goal bodies are `- throw "not implemented"`. Build of `PLang.Tests` passes (0 errors, pre-existing warnings only) — signatures compile.

### Files added

- `PLang.Tests/App/TypedReturnsTests/Stage0_PlangTypeRemovalTests.cs` (6)
- `PLang.Tests/App/TypedReturnsTests/Stage0_BuildMethodTests.cs` (8)
- `PLang.Tests/App/TypedReturnsTests/Stage0_NamedChannelsTests.cs` (9)
- `PLang.Tests/App/TypedReturnsTests/Stage0_DataMaterializationTests.cs` (7)
- `PLang.Tests/App/TypedReturnsTests/Stage1_TesterFileRenameTests.cs` (6)
- `PLang.Tests/App/TypedReturnsTests/Stage2_MechanicalTypings_Part1Tests.cs` (10)
- `PLang.Tests/App/TypedReturnsTests/Stage2_MechanicalTypings_Part2Tests.cs` (9)
- `PLang.Tests/App/TypedReturnsTests/Stage3_HttpResponseTests.cs` (6)
- `PLang.Tests/App/TypedReturnsTests/Stage3_HttpContentTypeDispatchTests.cs` (8)
- `PLang.Tests/App/TypedReturnsTests/Stage4_BuildMethodImplsTests.cs` (11)
- `PLang.Tests/App/TypedReturnsTests/Stage4_TypeHintPrecedenceTests.cs` (8)
- 12 `Tests/TestModule/TypedReturns/Stage{0..4}/*.test.goal`

Total: 88 C# unit signatures + 12 PLang behaviour goals = 100 failing tests.

### What is done, what is next

- **Done:** v1 contract for all of Branch A. Coder is unblocked to start Stage 0.
- **Next:** coder works stage by stage, commits per-stage, hands off to auditor. test-designer revisits if review feedback flags missing coverage.

### Decisions made

- One C# file per batch (per stage area), one PLang `.test.goal` per behaviour. Keeps `.pr` snapshots scoped to one assertion per file (avoids the "multiple goals in one file → overwrite on build" gotcha).
- All PLang `.test.goal` files live under `Tests/TestModule/TypedReturns/Stage<N>/` — separate from the existing `TestModule/Discover/`/`Run/`/`Report/` folders, since these tests are about the typed-returns *plumbing* not the tester domain.

## Code example

C# pattern (Stage 0 build method):
```csharp
[Test]
public async Task IClass_HasOptionalBuildMethod_ReturningTaskOfData()
    // Reflection: app.modules.IClass declares Build() : Task<Data>.
    => Assert.Fail("Not implemented");
```

PLang pattern (Stage 4 user-visible contract):
```plang
TestFileReadCsvLiteralAnnotatesDownstreamAsCsv
/ Build: `- read file 'foo.csv', write to %x%` followed by a downstream step.
/ The downstream "Variables in scope" snapshot must read %x%(csv), driven by file.read.Build().
- throw "not implemented"
```

## Hand-off to coder

Start at Stage 0 (`Stage0_*.cs`). The four files are independent — `PlangTypeRemoval` is the most invasive (touches the source generator and every `[PlangType]` site). The named-channels file documents the no-listener-no-op contract that all later Build() warnings rely on. Stage 1 (rename) can land in parallel.
