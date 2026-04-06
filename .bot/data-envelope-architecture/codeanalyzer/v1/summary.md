# Code Analyzer v1 — Summary

## What this is
Code simplicity analysis of the `data-envelope-architecture` branch — 16 changed files covering Engine.Types (Phase 1), Data context/lazy derivation (Phase 2), Data partial split + Out view (Phase 3), envelope pipeline (Phase 4), and security hardening (v5).

## What was done
Three-pass analysis (OBP compliance, simplification, readability) of all changed production code and test code.

**Result: 12 CLEAN, 3 NEEDS WORK (minor), 0 MAJOR ISSUES.**

No OBP violations found. The architecture correctly follows all 5 OBP rules:
- Data owns its own envelope behavior (Wrap/Compress/Encrypt)
- Engine.Types consolidates type knowledge on Engine (navigate, don't pass)
- Data keeps object references (parent Data, context)
- Context is per-request, propagated through Variables
- Return list iteration in Action.Methods is acceptable (simple list, not a smart collection)

Seven findings, all low-to-medium:

| # | Severity | What | File |
|---|----------|------|------|
| 1 | Medium | Duplicated `CleanName` in Data + Variables | Variables.cs, Data.cs |
| 2 | Low | Duplicated system variable check in Clear/Clone | Variables.cs |
| 3 | Low | Inverse dictionary sync risk (_nameToClr / _clrToName) | Types/this.cs |
| 4 | Low | Inconsistent concurrency (lock vs ConcurrentDictionary) | Types/this.cs |
| 5 | Low | RehydrateNestedData heuristic could false-positive | Data.Envelope.cs |
| 6 | Low | Merge silently drops non-list values | Data.Result.cs |
| 7 | Info | Generic type parsing limited to single nesting | Types/this.cs |

## Code example
The cleanest pattern in this branch — Data's envelope pipeline (Data.Envelope.cs):

```csharp
// Outbound: each method returns self if no-op, new Data if transformed
var outbound = original.Wrap().Compress().Encrypt();

// Inbound: reverse
var inbound = outbound.Decrypt().Decompress().Unwrap();
```

Each pipeline method follows the same structure: check preconditions → return self → do work → return result. Self-contained, no external dependencies, fluent chain. This is OBP done right — Data owns its own transport transformation.

## Files analyzed
- `PLang/App/Engine/Memory/Data.cs` (+ .Result.cs, .Navigation.cs, .Envelope.cs)
- `PLang/App/Engine/Memory/Variables.cs`
- `PLang/App/Engine/Types/this.cs`
- `PLang/App/Engine/View.cs`
- `PLang/App/Engine/this.cs`
- `PLang/App/Engine/Context/PLangContext.cs`
- `PLang/App/Engine/Goals/Goal/Steps/Step/Actions/Action/Methods.cs`
- `PLang/App/actions/convert/fromJson.cs`
- `PLang/App/GlobalUsings.cs`
- `PLang.Tests/` (DataTests, VariablesTests, EngineTypesTests, GlobalUsings)
