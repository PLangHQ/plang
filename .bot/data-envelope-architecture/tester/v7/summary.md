# Tester v7 Summary — Coder v5 Security Hardening

## What this is

Test quality analysis of coder v5: depth limits on 5 recursive methods, cycle detection on variable resolution, fromJson dedup, Verified → private set. This was a security hardening pass responding to security audit findings + tester v6 findings.

## Test suite results

- **1384 pass, 0 fail, 0 skipped** — no regressions. 12 new tests from v5.

## v6 findings resolved

| v6 Finding | Coder Response | Status |
|---|---|---|
| #1 Critical: Zip bomb untested | `Decompress_ExceedsSizeLimit_ReturnsError` — 110MB zeros, asserts DecompressError + StatusCode 500 | **Resolved** |
| #2 Major: Thread safety untested | Not addressed | **Open (carry-forward)** |
| #3 Major: Merge() untested | 3 tests — combine by name, null other, non-list silent drop | **Resolved** |
| #4 Minor: StatusCode unasserted | 4 dedicated StatusCode 500 tests | **Resolved** |
| #5 Minor: Inner context | Not addressed | **Open (carry-forward, minor)** |
| #6 Minor: Numeric widening | Not addressed | **Open (carry-forward, minor)** |

## New v7 findings: 2 major, 2 minor

### Finding #1 — MAJOR: ResolveVariablesInPath cycle detection completely untested

**File**: `PLang/Runtime2/Engine/Memory/MemoryStack.cs:203-237`
**Test file**: `PLang.Tests/Runtime2/Memory/MemoryStackTests.cs`

The cycle detection logic uses a `[ThreadStatic] HashSet<string>` to detect circular variable references in bracket-index paths (e.g., `array[x]` where `x` resolves to `y` and `y` resolves to `x`). Zero tests exist for this feature.

**Why this matters**: This is a security hardening feature — preventing infinite loops from circular variable references. If someone removes the `_resolvingVars.Add()` check, no test fails. The entire protection silently disappears.

**What "if subtly wrong" looks like**:
- If the `finally { _resolvingVars.Remove(varName); }` were removed, the set would accumulate and block legitimate re-use of variables across separate Get() calls
- If `isRoot` cleanup (`_resolvingVars = null`) were removed, the ThreadStatic set would leak between calls
- None of these would be caught

**Suggestion**: Add tests for:
- (a) Direct circular reference: `stack.Set("x", "[y]"); stack.Set("y", "[x]");` — verify `stack.Get("items[x]")` terminates and leaves `[x]` or `[y]` unresolved
- (b) Self-reference: variable whose resolution path references itself
- (c) After cycle detection, verify normal variable resolution still works (no HashSet leak)

### Finding #2 — MAJOR: GetChild depth error not tested through MemoryStack integration

**File**: `PLang/Runtime2/Engine/Memory/MemoryStack.cs:90`
**Test file**: `PLang.Tests/Runtime2/Memory/MemoryStackTests.cs`

`MemoryStack.Get()` at line 90 calls `root.GetChild(remaining)` and returns the result directly. GetChild now returns `Data.FromError(ServiceError("NavigationDepthExceeded"))` when depth exceeds 100. But `MemoryStack.Get()` returns `Data?` — callers expect null for "not found" and a valid Data for "found."

**The integration gap**: A caller doing `stack.Get("a.b.c...(101 dots)")` gets a non-null Data with `Success == false`. If they check `result != null` (standard pattern), they proceed and access `.Value` on an error Data — getting null or garbage instead of the expected value. No test exercises this path through MemoryStack.

**Why this matters**: The depth limit is tested in isolation (DataTests.cs line 1052) but never through the integration point where users actually hit it. The Data-level test proves GetChild returns an error; no test proves MemoryStack callers handle that error correctly.

**Suggestion**: Add `MemoryStackTests.Get_DeeplyNestedPath_ReturnsErrorData()` — 101+ dot path, assert `result!.Success == false` and `result.Error!.Key == "NavigationDepthExceeded"`. This documents the contract: MemoryStack.Get can return error Data, not just values or null.

### Finding #3 — MINOR: FromJson not tested with deep nesting

**File**: `PLang/Runtime2/actions/convert/fromJson.cs:12-24`
**Test file**: `PLang.Tests/Runtime2/Modules/convert/ConvertTests.cs`

`fromJson.Run()` calls `Data.UnwrapJsonElement()` which throws `InvalidOperationException` at depth 128. The action's `catch (Exception ex)` wraps this as `ValidationError("Invalid JSON: ...")`. But ConvertTests only tests simple JSON (1-level object) and invalid JSON. No test exercises the depth limit through the action.

**Impact**: Low. The depth limit itself IS tested at the Data level. But the error message through the action would be misleading: "Invalid JSON: JSON nesting exceeds maximum depth (128)" — the JSON IS valid, it's just too deep. The error key "JsonParseError" is also wrong for this case.

**Suggestion**: Add a test in ConvertTests with 200-level nested JSON, verify the error message distinguishes depth exceeded from parse failure.

### Finding #4 — MINOR: Clr() depth limit not boundary-tested

**File**: `PLang/Runtime2/Engine/Types/this.cs`
**Test file**: `PLang.Tests/Runtime2/Types/EngineTypesTests.cs:634`

The test uses 25 levels (exceeds MaxGenericDepth=20) and asserts null. No test for exactly 20 (should succeed) or 21 (should fail). Off-by-one bugs in `depth > MaxGenericDepth` vs `depth >= MaxGenericDepth` would go undetected.

**Impact**: Low. The code is correct today (`>` is right for 0-based counting where depth starts at 0).

**Suggestion**: Add boundary tests: `list<...(20 deep)...>` → should return `List<List<...>>`, `list<...(21 deep)...>` → should return null.

## Carry-forward from v6

| Finding | Severity | Notes |
|---|---|---|
| Thread safety (ConcurrentDictionary/lock) no concurrent test | Major | Still open. Code is correct but regression undetectable. |
| Inner context not propagated in RehydrateNestedData | Minor | By design — Unwrap/GetChild stamp context lazily |
| Numeric widening (int → long) through compress/decompress | Minor | Mitigated by GetValue<T>() using Convert.ChangeType |

## Verdict: needs-fixes

The security hardening code is correct and well-structured. The depth limit pattern is consistent across all 5 locations. The new tests cover the critical paths (zip bomb, depth limits, Merge). But two security features have zero test coverage: cycle detection in variable resolution (entirely untested) and the GetChild→MemoryStack integration path (depth error not tested through the caller). For a security hardening release, the security features should be the most thoroughly tested code.
