# Tester v8 Summary — data-envelope-architecture

## What this is
Review of coder v5+v6 changes (security hardening + code analyzer cross-concern fixes). Coder v5 added PLang integration tests and security hardening. Coder v6 fixed 4 cross-concern gaps found by code analyzer v2.

## Test run
- C# tests: **1390 pass, 0 fail, 0 skipped** (up from 1384)
- PLang tests: 17 integration tests added (hand-written .pr files) — not runnable without plang binary

## Resolution of v7 findings

### Finding #2 (Major: GetChild depth through Variables) — RESOLVED
`Get_DeeplyNestedPath_ReturnsErrorData` builds a 101-level dictionary, queries via Variables.Get, asserts `Success == false` and `Error.Key == "NavigationDepthExceeded"`. Exactly what I suggested. Honest test — would fail if the depth check were removed.

### Finding #3 (Minor: fromJson deep nesting) — RESOLVED
`FromJson_DeeplyNested_Fails` tests 200-level nested JSON through the action handler. Note: this actually hits STJ's own MaxDepth (64) before our limit (128), so it returns `"JsonParseError"` not `"JsonDepthExceeded"`. The distinct `catch (InvalidOperationException)` for `"JsonDepthExceeded"` is effectively unreachable today (STJ always throws first). Not a bug — defensive code that would activate if STJ's limit were raised.

`FromJson_DecimalNumber_PreservesPrecision` tests the decimal fix end-to-end through the action handler. Good.

### Finding #1 (Major: cycle detection) — STILL OPEN
**Zero tests for `_resolvingVars` cycle detection in `Variables.ResolveVariablesInPath`.** Grep confirms no test references `_resolvingVars`, `circular`, or `cycle` in the Variables test file. This security feature has been open since v7.

### Finding #4 (Minor: Clr boundary) — STILL OPEN
No boundary tests at depth 20/21.

## Code analyzer v2 cross-concern fixes — VERIFIED

All 4 fixes are clean and tested:

1. **Decimal precision**: `UnwrapJsonNumber` tries `TryGetInt64` → `TryGetDecimal` → `GetDouble`. Two tests verify `19.99` stays `decimal` and `42` stays `long`. Honest tests — would fail if order were wrong.

2. **Variables.Clone() context**: `clone.Context = Context` added at line 185. Test renamed from `Clone_DataHasNoContext` to `Clone_PreservesDataContext` — assertion flipped from `IsNull` to `IsEqualTo(context)`. Second test `Clone_PreservesContext` also added. Both honest.

3. **fromJson depth error key**: Separate `catch (InvalidOperationException)` returns `"JsonDepthExceeded"` instead of generic `"JsonParseError"`. Though unreachable today (STJ limit < ours), it's correct defensive code.

4. **GetChild depth integration**: See Finding #2 above.

## New observation

### Observation: fromJson `JsonDepthExceeded` catch is dead code

`fromJson.cs:19-22` catches `InvalidOperationException` and returns `"JsonDepthExceeded"`. But `JsonSerializer.Deserialize<JsonElement>` has MaxDepth=64 by default, and our `UnwrapJsonElement` limit is 128. STJ always throws `JsonException` first, hitting the general `catch (Exception)` block. The `InvalidOperationException` catch is only reachable if STJ's MaxDepth is explicitly raised above 128.

Not a bug — it's defensive. But the test `FromJson_DeeplyNested_Fails` asserts `"JsonParseError"` (general catch), not `"JsonDepthExceeded"` (the new catch). So the new error key has zero coverage.

**Impact:** None today. If STJ's default MaxDepth changes in a future .NET version, the catch would activate. Good to have.

## Verdict: **needs-fixes**

One major finding remains open from v7:

| # | Severity | Issue | Status |
|---|----------|-------|--------|
| 1 | **Major** | ResolveVariablesInPath cycle detection completely untested | Open since v7 |
| 2 | Minor | Clr() depth boundary not tested at 20/21 | Open since v7 |
| 3 | Minor | `JsonDepthExceeded` error key has zero coverage (dead code today) | New |

**Carry-forwards from v6:**
- Thread safety concurrent test (major) — still open
- Inner context in RehydrateNestedData (minor) — still open
- Numeric type widening through compress/decompress (minor) — still open

The cycle detection gap is the blocking item. It's a security feature that if removed, no test fails. Everything else is minor or acceptable.
