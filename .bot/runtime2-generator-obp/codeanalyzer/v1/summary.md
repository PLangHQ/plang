# codeanalyzer v1 — runtime2-generator-obp

## What this is

Code-simplicity review of the v4 implementation: resolution moved from `Data.Value` getter side-effects to `Data.As<T>(context)` per call; `Data` is stateless w.r.t. resolution; `App.Run(action, context)` owns scaffolding (callstack, save/restore Step/Goal/Event, try/catch/finally with ServiceError translation, frame.SnapshotVariables); generator restructured into Discovery + Emission folder hierarchy; build-time PLNG001 diagnostic enforces Data<T> / [Provider] / [VariableName] property kinds. Coder reported 2427/2427 tests green.

## What was done

5-pass review (OBP / Simplifications / Readability / Behavioral / Deletion-test) across 14 changed production files: 8 generator files + 6 runtime files (`Data`, `App`, `Action`, `ICodeGenerated`, `Debug`, `Variables`).

Output:
- `result.md` — 38 findings (10 MAJOR, 19 MINOR, 9 NIT) with file:line, current code, and fix.
- `verdict.json` — `fail` (NEEDS WORK).
- `summary.md` (this file).

The v4 design is sound and a real architectural improvement. The flagged issues are concrete and surgical, not structural — almost all are surface-level cleanup. **Verdict: NEEDS WORK.**

## Top findings (load-bearing — fix before merge)

1. **`ActionClassInfo` is `sealed class`, not `record`** (`Discovery/this.cs:281`). The IIncrementalGenerator pipeline carries this type from `CreateSyntaxProvider` into `RegisterSourceOutput`. Without value equality (and with `List<T>` collections that compare by reference), the cache always misses on this carrier. Architect's plan and the comment at line 280 promise incremental-safety the structure does not deliver. Fix: convert to `public sealed record ActionClassInfo(...)` and replace `List<T>` with an `EquatableArray<T>` or `ImmutableArray<T>` + `SequenceEqual`.

2. **`__variables` field is dead emission** (`Emission/Action/this.cs:79,122`). Set in ExecuteAsync, never read anywhere across `PLang/`, `PLang.Tests/`, `os/`. Delete the field and the assignment.

3. **`__paramData` + `ParamData()` accessor are dead emission** (`Emission/Action/this.cs:91-97,230`). The dictionary is filled by the legacy `__Resolve<T>` helper, but the protected `ParamData(string)` accessor has zero callers. Pure waste — delete the dict, the writer, and the accessor.

## Behavioral concerns (latent bugs)

- **`AsT_Impl` recursion has no cycle detection** (`Data/this.cs:412`). `%a%↔%b%` cycles stack-overflow. `Variables.Resolve` has cycle protection via thread-static `_resolvingVars`, but `As<T>`'s full-match path bypasses it. Low practical risk, but the design promise of "fresh resolution every call" only holds when the variable graph is acyclic.
- **`SubstitutePrimitive` only walks generic typed shapes** (`Data/this.cs:500-501`). Non-generic `IList`/`IDictionary` (e.g., `ArrayList`, `Hashtable`) silently pass through with no substitution. Today's rehydration normalizes everything to typed forms, so practical risk is low — but worth a one-line shape-contract comment at minimum.
- **`As<T>` ignores `_type.Convert`** (`Data/this.cs:383-388`). A Parameter Data with `Type="json"` bypasses JSON deserialization in the resolution path. Pre-existing — v4 didn't change this.
- **`App.Run` catch deliberately swallows `OperationCanceledException`** (`App/this.cs:411`). This is intentional and load-bearing per `App/modules/timeout/after.cs:39-40` ("Inner action's generated ExecuteAsync swallows OCE into a ServiceError result, so we detect the timeout via CTS state + failed result"). But the inconsistency with `Step.RunAsync`'s catch (which excludes OCE) invites a future "fix" that breaks timeout silently. **Add a comment in App.Run explaining the choice and the dependency.**

## Code example — the dead-emission pattern

```csharp
// Emission/Action/this.cs lines 79, 122 — declared and assigned, never read:
private global::App.Variables.@this? __variables;       // declared
__variables = context.Variables;                         // assigned
// (no readers anywhere)

// Lines 91-97 — protected method emitted on every action partial:
private System.Collections.Generic.Dictionary<string, global::App.Data.@this?>? __paramData;
protected global::App.Data.@this? ParamData(string paramName)
    => __paramData != null && __paramData.TryGetValue(paramName, out var d) ? d : null;
// __paramData is filled at line 230 by __Resolve<T>; ParamData() has 0 callers across the repo.
```

**Pattern: emission slots that survived the v3→v4 simplification.** Phase 5 will sweep the legacy helper family (`__Resolve<T>`, `__StripPercent`, `__HasParam`); these dead slots can go on the same pass.

## What's still in progress / what to do next

- **Send back to coder** for finding cleanup. The three top findings (1 / 11 / 12) are mechanical fixes; the rest are simplification opportunities or readability polish.
- **For tester / auditor next:** the cycle-detection gap (finding 27) and the non-generic-IList shape-contract (finding 28) deserve explicit matrix entries to nail down behavior before Phase 5 lands.
- **No CLAUDE.md proposals** — codeanalyzer is a reviewer bot (read-only on CLAUDE.md per the workflow).

## Files

Reviewed (production code, no edits):
- Generator: `PLang.Generators/this.cs`, `PLang.Generators/Discovery/this.cs`, `PLang.Generators/Emission/Action/this.cs`, `PLang.Generators/Emission/Property/this.cs`, `PLang.Generators/Emission/Property/Data/this.cs`, `PLang.Generators/Emission/Property/Provider/this.cs`, `PLang.Generators/Emission/Property/Legacy/this.cs`, `PLang.Generators/IsExternalInit.cs`
- Runtime: `PLang/App/Data/this.cs`, `PLang/App/this.cs`, `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs`, `PLang/App/modules/ICodeGenerated.cs`, `PLang/App/Variables/this.cs`, `PLang/App/Debug/this.cs`

Cross-checks (read-only confirmations):
- `PLang/App/Goals/Goal/Steps/Step/this.cs` (catch-path consistency with `App.Run`)
- `PLang/App/modules/timeout/after.cs` (verifies the OCE-catch dependency)
- `PLang.Tests/Generator/Matrix/Resolution/ResolutionTests.cs`, `PLang.Tests/App/DataTests/DataAsTResolutionTests.cs`, `PLang.Tests/App/AppRunScaffoldingTests.cs`, `PLang.Tests/Generator/SnapshotParamsTests.cs`, `PLang.Tests/Generator/GeneratorValidationTests.cs` (matrix coverage check)
