# Test-designer summary — purge `System.IO` from PLang

**Version:** v1
**Date:** 2026-05-25

## What this is

The architect's plan migrates PLang's filesystem typing — `string` → `app.types.path.@this` — across the Goal model, GoalCall, AppGoals, App, Builder snapshot, and ~13 action handlers. Every `string` path today is a place that misses `FilePath.AuthGate`. The branch's promise is that post-migration every filesystem reach routes through `AuthGate`.

This is **security work**. The type flip is the mechanism; the **denial paths** are the value. I designed the test contract accordingly.

## What was done

11 batches of tests covering all 13 architect design decisions:

| Batch | Area | Files |
|---|---|---|
| 1 | Path derivation verbs (D1) | `PLang.Tests/App/Types/PathTests/DerivationTests/PathDerivationVerbTests.cs` |
| 2 | PLNG002 analyzer (D12) | `PLang.Tests/Generator/Diagnostics/Plng002SystemIoBanTests.cs` |
| 3 | `.goal` MIME → Goal (D2) | `PLang.Tests/App/Types/PathTests/GoalMimeDeserializationTests.cs` |
| 4 | Goal/GoalCall typing + JSON converter (D3, C7/C11) | `PLang.Tests/App/Goals/PathTypingTests/GoalPathTypingTests.cs` |
| 5 | AppGoals + App.Load/Save (D4/D5/D6) | `PLang.Tests/App/Goals/AppGoalsMigrationTests/AppGoalsThroughPathVerbsTests.cs` |
| 6 | Execute verb + LoadAssemblyAsync (D8/C5) | `PLang.Tests/App/FileSystem/PermissionTests/ExecuteVerbTests/ExecuteVerbTests.cs` |
| 7 | Content-shape verbs `ReadAsBase64`/`ReadAsDataUri` (D9a) | `PLang.Tests/App/Types/PathTests/ContentShapeVerbTests.cs` |
| 8 | `.Absolute` discipline (D13) | `PLang.Tests/App/FileSystem/SurfaceTests/AbsoluteDisciplineTests.cs` |
| 9 | Ring-2 handler denial paths | `PLang.Tests/App/Modules/{Test,Http,Ui,Settings,Llm,Debug}/*DenialTests.cs` + 8 `Tests/Permission/<scenario>/Start.test.goal` |
| 10 | In-root silent fast-path | `PLang.Tests/App/FileSystem/SurfaceTests/InRootSilentFastPathTests.cs` |
| 11 | Equality / dict-keying under `RootComparison` | `PLang.Tests/App/Types/PathTests/RootComparisonKeyingTests.cs` |

**Totals:** ~80 C# test signatures, 8 PLang `.test.goal` scaffolds. All bodies are `Assert.Fail("Not implemented")` / `throw "not implemented"` — these are the spec for the coder.

## Key deliberate choices

1. **D13 (`.Absolute` discipline) got its own batch** instead of being folded into Stage 7 docs. The rule is only real if a test catches a missing `Authorize` before `.Absolute` — otherwise it's prose. Includes an explicit mutation-test placeholder so tester knows to exercise the pattern.
2. **Reused existing `CannedChannel`/`StatelessChannel` infrastructure** from `PLang.Tests/App/FileSystem/SurfaceTests/FileSystemPermissionFlowTests.cs` for actor + permission-prompt setup. No new fixture project needed.
3. **PLang test goals follow the repo convention** (`<Scenario>/Start.test.goal`) instead of the character's `Test<Name>` suggestion — the repo's discovery is by `.test.goal` extension and the existing tests under `Tests/Permission/Authorize/` use the same shape.
4. **Linux/Windows split tests on `RootComparison`** — the case-sensitivity behaviour is OS-dependent by design, and the migration arguably fixes a latent Linux bug. Pinned both sides explicitly.

## Code example — the dominant pattern

```csharp
[Test] public async Task LoadAssemblyAsync_OutOfRoot_DeniedAnswer_DoesNotLoadAssembly()
{
    // "n" answer → no Assembly.LoadFrom call; returns Data.Fail.
    await Task.CompletedTask; Assert.Fail("Not implemented");
}
```

```plang
Start
/ Batch 9 — http server serving static files: a request for /static/../../etc/passwd
/ must be denied by AuthGate (most adversarial surface: untrusted HTTP → FS).
- throw "not implemented"
```

## What's next

Coder bot picks this up. Stage order from the architect's plan (1 → 7) is the implementation order; the test files map 1:1 onto stages.

## Open questions for tester (downstream)

- The mutation-test placeholder in `AbsoluteDisciplineTests.MutationGuard_…` requires tester to perform an announced source mutation (per CLAUDE.md "Mutation Testing" rule). The test contract calls for it; the actual mutation pattern is tester's call.
- Out-of-root permission-prompt fixture shape (which channel reads the y/n/a script, and how it asserts the right downstream behaviour) is left open in my batch 9 tests — coder decides whether the existing `CannedChannel(answer)` pattern from `FileSystemPermissionFlowTests` extends cleanly or needs a new helper.
