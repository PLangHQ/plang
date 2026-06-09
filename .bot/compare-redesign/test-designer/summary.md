# test-designer summary — compare-redesign

**Version:** v1

## What this is

The typed value model branch (`compare-redesign`, still named that for non-cosmetic reasons). Architect rewrote the design around a single principle — **the type holds the value, the type decides** — with comparison as the first consumer. Seven stages, where 2–6 land as one green unit and 7 rides behind a warning-then-error gate. My job here: translate the architect's `plan/test-strategy.md` (5 integration cuts) + `plan/test-coverage.md` (per-stage matrix, failure matrix, new-surfaces inventory) into a stubbed test suite that defines the behavioural contract for the coder.

## What was done

Wrote **125 C# TUnit tests** across 14 files in `PLang.Tests/App/CompareRedesign/` and **15 PLang `.goal` tests** under `Tests/CompareRedesign/`. All stubs (`Assert.Fail("Not implemented")` / `throw "not implemented"`). `dotnet build PLang.Tests` clean (0 errors).

Decisions while writing:
- Approach **interactive batches** per the character file — drafted 7 batches up-front in chat, presented Batch 1 in full, got Ingi's blanket approval ("write up all batches"), then wrote everything in one pass.
- Picked layer per row strictly from `test-coverage.md` ("C#" / "goal" / "int" columns). Did not move rows between layers.
- Folder named `CompareRedesign/` to match the branch (rename to `typed-value-model` deferred per the architect's plan §1).
- C# tests assume `Data.MaterializeCount` already exists (the architect references it as an existing surface). Flagged in the test plan for the coder to confirm.
- Reflection-based tests (deletion assertions, gate probes) are signature-only stubs — coder picks MetadataLoadContext vs compile-time fixtures.

Files modified:
- `PLang.Tests/App/CompareRedesign/Stage1_ComparisonEnumTests.cs` (1 test)
- `PLang.Tests/App/CompareRedesign/Stage2_ValueDoorTests.cs` (13)
- `PLang.Tests/App/CompareRedesign/Stage2_PlaneResolverTests.cs` (8)
- `PLang.Tests/App/CompareRedesign/Stage2_GetParameterLazyTests.cs` (7)
- `PLang.Tests/App/CompareRedesign/Stage2_NavigationAsyncTests.cs` (6)
- `PLang.Tests/App/CompareRedesign/Stage3_ReferenceNarrowTests.cs` (15)
- `PLang.Tests/App/CompareRedesign/Stage3_PathDemolitionTests.cs` (15)
- `PLang.Tests/App/CompareRedesign/Stage4_RankTests.cs` (4)
- `PLang.Tests/App/CompareRedesign/Stage4_PerTypeCompareTests.cs` (17)
- `PLang.Tests/App/CompareRedesign/Stage5_DataCompareEntryTests.cs` (4)
- `PLang.Tests/App/CompareRedesign/Stage6_ConsumersTests.cs` (17)
- `PLang.Tests/App/CompareRedesign/Stage6_DiffRenameTests.cs` (2)
- `PLang.Tests/App/CompareRedesign/Stage7_SurfaceGateTests.cs` (10)
- `PLang.Tests/App/CompareRedesign/Stage7_PathGrowthTests.cs` (4)
- `Tests/CompareRedesign/` — 15 `.test.goal` files (6 integration cuts + 5 failure-matrix + 4 plane/narrow surface).

Status: complete. Next: **coder** to implement.

## Code example — TUnit stub

```csharp
[Test]
public async Task BangFileBangPath_ResolvesOnUnNarrowed_AND_Narrowed_Branches()
{
    // chain-wide ! — %config!file!path% resolves whether or not the value narrowed;
    // no flow-dependent crash
    Assert.Fail("Not implemented");
    await Task.CompletedTask;
}
```

## Code example — PLang goal stub

```
Start
/ Cut 5 — enum boundary + membership. %d% a dict, %n% = 5.
/ if %d% > %n% → error; if %d% == %n% → error; if %x% == null → works;
/ if %d% == %d2% → works; [%d%] contains %n% → false, no error.
/ Proves Incomparable/NotEqual boundary, null carve-out, membership-never-errors.
- throw "not implemented"
```

## What to do next

Run **coder** to implement Stages 2–6 as one green unit, then Stage 7 behind the warning-then-error gate. The coverage matrix in `architect/plan/test-coverage.md` and the test plan in `.bot/compare-redesign/test-designer/v1/test-plan.md` are the contract.
