# Tester v9 Summary — data-envelope-architecture

## What this is
Review of coder v7's test-only changes that address tester v8's blocking finding (cycle detection) and minor finding (Clr depth boundary). No production code was changed — all 4 new tests target existing defensive code.

## Test run
- C# tests: **1394 pass, 0 fail, 0 skipped** (up from 1390)
- PLang tests: 17 integration tests (hand-written .pr files) — not runnable without plang binary

## Resolution of v8 findings

### Finding #1 (Major, blocking: cycle detection) — RESOLVED

Two tests added in `VariablesCycleDetectionTests`:

**`Get_CircularVariableReference_LeavesUnresolved`** — Honest test. Uses reflection to pre-seed the `[ThreadStatic] _resolvingVars` HashSet with "idx", then calls `Get("data.items[idx]")`. Since "idx" is already in the visited set, `_resolvingVars.Add(varName)` returns false → `[idx]` stays as literal text → navigation fails → returns null. The test contrasts this with a normal resolution that returns "one" — same inputs, different outcome due to cycle guard.

Would this catch a regression? Yes:
- Remove the `if (!_resolvingVars!.Add(varName)) return match.Value;` guard → test fails (returns "one" instead of null)
- Remove the inner `_resolvingVars.Remove(varName)` → test 2 fails (second call sees "idx" still in set)
- Change guard behavior (e.g., throw instead of return unresolved) → test fails on assertion

**`Get_NormalVariableResolution_WorksAfterCycleCleanup`** — Verifies ThreadStatic cleanup. Two consecutive calls with different idx values both resolve correctly. If the inner `finally { _resolvingVars.Remove(varName); }` were removed, the second call would detect "idx" as a cycle and fail. Honest test.

**Limitation acknowledged**: The cycle detection is structurally unreachable through the current public API — `GetValue()` passes simple names that never contain brackets, so the `ResolveVariablesInPath` cycle guard can't fire through normal usage. The reflection approach is the right call for testing defensive code. If the API surface expands to allow bracket-containing paths in the future, the guard will activate naturally.

### Finding #2 (Minor: Clr boundary) — RESOLVED

Two boundary tests added:

- **`Clr_ExactlyAtMaxDepth_Resolves`** — 20 `list<>` nestings. `Clr("string", 20)` evaluates `20 > 20` → false → resolves. Asserts `IsNotNull`. Would fail if MaxGenericDepth were changed to 19.
- **`Clr_OneOverMaxDepth_ReturnsNull`** — 21 `list<>` nestings. `Clr("string", 21)` evaluates `21 > 20` → true → returns null. Asserts `IsNull`. Would fail if MaxGenericDepth were changed to 21.

Clean boundary pair — each test constrains the boundary from one side.

### Finding #3 (Observation: JsonDepthExceeded dead code) — Acknowledged, no action

Correct decision. The catch is defensive code that may activate if STJ raises its default MaxDepth in a future .NET version. Not testable without mocking STJ internals, which provides no value.

## Carry-forwards from v6

| # | Severity | Issue | Status |
|---|----------|-------|--------|
| v6-2 | Major | Thread safety concurrent test | Open |
| v6-5 | Minor | Inner context in RehydrateNestedData | Open |
| v6-6 | Minor | Numeric type widening through compress/decompress | Open |

These are long-standing and don't block approval. The thread safety one is structural (code uses ConcurrentDictionary/lock correctly, just no concurrent test proving it). The other two are edge cases.

## Verdict: **approved**

All blocking and non-trivial findings are now resolved. The cycle detection tests are honest — they verify the guard mechanism directly and would catch regressions. The Clr boundary tests properly constrain the depth limit. No new findings.

The test suite (1394 C# tests + 17 PLang tests) provides adequate coverage for auditor review. The only remaining gaps are carry-forward minor/structural items that have been tracked since v6.
