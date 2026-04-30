# Auditor v1 — runtime2-generator-obp

## What this is

First auditor pass on the `runtime2-generator-obp` branch. The branch went
through architect → coder (5 rounds) → codeanalyzer (CLEAN on coder/v3) →
tester (APPROVED on coder/v4) → security (PASS on coder/v4, 1 medium + 3
low) → coder/v5 (closes security #1 + #3). I'm the first reviewer who sees
the v5 delta in full context.

The branch is a major refactor: resolution moves from `Data.Value`'s
side-effect getter into a fresh-walk `Data.As<T>(context)`; the source
generator splits into Discovery + Emission/Action + Emission/Property/{Data,
Provider,Legacy}; build-time PLNG001 enforces `Data<T>` / `[Provider]` /
`[VariableName]` only on action properties. v5's surgical fixes plumb
`[Sensitive]` through Discovery → record → emission for snapshot masking,
and restructure `Data.AsT_Impl`'s cycle/depth-trip to return ServiceError
instead of swallowing.

**Verdict: PASS.** No critical/major findings. 1 minor + 3 nits + 1
observation. C# 2468/2468 green.

## What was done

1. Read codeanalyzer/v3, tester/v4, security/v1 reports + coder/v1–v5
   summaries. Built picture of what each bot covered.
2. Read the v5 diff (`47ba8a96..HEAD`): 8 files, 147+/40− lines across
   Discovery + 2 Property emitters + Data.AsT_Impl + 4 test files.
3. Ran `dotnet build` + `dotnet run --project PLang.Tests` — 2468/2468 green.
4. Spot-checked the generated `SensitiveSnapshot.Action.g.cs` to confirm
   masked PrValue/FinalValue expressions wire correctly. Confirmed:
   `PrValue = __pr?.Value != null ? "******" : null` for sensitive,
   `FinalValue = __ApiKey_set ? (object?)"******" : null` for sensitive.
5. Cross-file: traced who consumes `Data.As<T>` in production handlers
   (none check `.Success` — all read `.Value` directly).
6. Compared legacy emission's `__Resolve<T>` (sets `__resolutionError`,
   short-circuits before `Run()`) vs Data<T> emission's `__ResolveData(...).As<T>(Context)`
   (assigns directly to backing, no error propagation).
7. Wrote findings to `auditor-report.json` and `result.md`.

## Findings summary

| # | Severity | Category | File | Issue |
|---|---|---|---|---|
| 1 | minor | cross-file | `PLang.Generators/Emission/Property/Data/this.cs:32` | Data<T> emission buries As<T> ServiceErrors that legacy emission surfaces |
| 2 | nit | review-gap | `PLang.Tests/Generator/Matrix/Snapshot/SnapshotTests.cs:105` | "Unaccessed" test name is misleading — handler always accesses both props |
| 3 | nit | contract | `PLang.Generators/Emission/Property/Data/this.cs:60` | Sensitive FinalValue masks even when backing is null (over-masking, not security) |
| 4 | nit | architectural | `PLang.Generators/Discovery/this.cs:174` | SensitiveAttribute matched by short name, layer disagreement with runtime full-type match |

## Code example — the cross-file contract gap

The v5 fix to security #3 made `Data.AsT_Impl` return ServiceError on
cycle/depth instead of swallowing. The renamed cycle tests claim:

> "callers see the failure rather than an unresolved %var% leak"

This is true only for **legacy** property emission:

```csharp
// Legacy emission: __Resolve<T>
private T? __Resolve<T>(string name) {
    var typed = data.As<T>(Context);
    if (!typed.Success) { __resolutionError = typed; return default; }  // ← surfaces
    ...
}
// ExecuteAsync short-circuits: if (__resolutionError != null) return __resolutionError;
```

For **Data<T>** property emission (the forward-looking path):

```csharp
// Data<T> emission: line 32, 36, 40, 44 of Emission/Property/Data/this.cs
get { if (__ApiKey_backing == null) {
        __ApiKey_backing = __ResolveData("apikey").As<string>(Context);  // ← swallows
        __ApiKey_set = true;
      }
      return __ApiKey_backing!;
    }
```

The FromError lives on the backing field. The handler's `Run()` reads
`ApiKey.Value` (default(T) = null), the error is invisible, and the handler
proceeds with a null parameter. None of the current production handlers
(file/read, llm/query, etc.) check `.Success` before using `.Value`.

Practical impact is small (cycles in user variables are rare), but the
contract is inconsistent between the two emission paths in the same
codebase. Suggested fix is a single-line addition per Data<T> emission
branch — mirror the legacy `__resolutionError` route.

## Production code 5-pass on the v5 delta

Three surgical changes:
- `Data.AsT_Impl` cycle/depth restructure: cycle returns FromError before
  try (outer frame owns entry, no Remove); depth returns FromError inside
  try (this frame owns entry, finally Removes). HashSet lifecycle correct,
  ThreadStatic cleanup at root finally is preserved.
- `Discovery.BuildProperty` adds `isSensitive` scan, threads through both
  record constructors. Records auto-include the new field in equality
  (incremental cache contract preserved).
- `Emission/Property/{Data,Legacy}/this.cs` `EmitSnapshotEntry` branches on
  `IsSensitive` to emit masked vs raw expressions. Single point of mask
  decision, mirrors the existing PrValue/FinalValue shapes.

OBP: clean. Simplification: clean. Readability: clean. Behavioral
reasoning: see Finding 1. Deletion test: every line earns its place
EXCEPT the SetFlag guard on sensitive FinalValue (Finding 2).

## What's next

**docs.** No production fixes blocking the merge. If Finding 1 is
prioritized, send to coder for the single-line surfacing addition + a
Data<T>-emitted-handler test that asserts a cyclic `%var%` parameter
causes the handler to return ServiceError to the caller.

## Files written

- `.bot/runtime2-generator-obp/auditor/v1/plan.md` — plan (created)
- `.bot/runtime2-generator-obp/auditor/v1/result.md` — full findings (created)
- `.bot/runtime2-generator-obp/auditor/v1/verdict.json` — pass verdict (created)
- `.bot/runtime2-generator-obp/auditor/v1/summary.md` — this file (created)
- `.bot/runtime2-generator-obp/auditor/summary.md` — bot-root summary (created)
- `.bot/runtime2-generator-obp/auditor-report.json` — structured findings (created)
- `.bot/runtime2-generator-obp/report.json` — session entry (modified)

No production code or test code changed. Read-only review pass.
