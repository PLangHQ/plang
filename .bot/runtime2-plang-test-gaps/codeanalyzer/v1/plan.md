# Code Analyzer v1 Plan — runtime2-plang-test-gaps

## Scope

6 modified C# files in engine infrastructure:

| File | Change |
|------|--------|
| `PLang/Executor.cs` | Removed explicit `DiscoverAsync` call (now internal to `RunAsync`) |
| `PLang/App/Engine/Goals/Goal/Methods.cs` | Return `stepsResult` instead of `Data.Ok()` |
| `PLang/App/Engine/Goals/Goal/Steps/this.cs` | Track `lastResult`, return it instead of `Data.Ok()` |
| `PLang/App/Engine/Goals/Setup/this.cs` | Convention-based discovery (private), integrated into `RunAsync` |
| `PLang/App/Engine/Goals/this.cs` | Key goals by PrPath, add name-based search fallback |
| `PLang/App/Engine/Test/this.cs` | Per-test engine root, Data-based result tracking, setup before tests |

Plus 33 new PLang test suites (`.test.goal` + `.goal` + `.pr` files) — tests are out of scope for code analysis but inform behavioral reasoning.

## Analysis Plan

1. **Pass 1: OBP Compliance** — Check all 6 files against the 5 OBP rules
2. **Pass 2: Simplification** — Dead code, duplication, over-abstraction
3. **Pass 3: Readability** — Naming, method length, flow clarity
4. **Pass 4: Behavioral Reasoning** — Return value semantics, setup flow, test isolation, data flow tracing
5. **Pass 5: Deletion Test** — What code can be removed without test failure?

## Pre-reading completed

- `Documentation/App/plang_object_based_pattern.md`
- `Documentation/App/README.md`
- `Documentation/App/good_to_know.md`
- `Documentation/App/modules.md`
- Engine `this.cs` constructor and RunGoalAsync
- Step `Methods.cs` for full execution flow
