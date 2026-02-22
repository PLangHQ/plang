# Tester v1 Summary — Engine.Types Phase 1

## What this is

Test quality analysis of the coder's Phase 1 implementation of `Engine.Types`. 62 TUnit tests were written for a new class consolidating PLang type knowledge. My job was to verify these tests catch real bugs, not just confirm the implementation.

## What was done

1. **Ran full C# test suite** — 1229 tests, all pass, 0 failures, no regressions.
2. **Analyzed each test group** for intent verification, edge cases, and false greens.
3. **Verified two suspected bugs experimentally** — both confirmed:
   - `Kind(null)` throws `ArgumentNullException` (Dictionary.TryGetValue with null key on OrdinalIgnoreCase comparer)
   - `Name(typeof(HashSet<string>))` produces `"hashset`1"` (CLR backtick-arity suffix leaks into PLang names)

## Findings: 8 total (1 critical, 3 major, 4 minor)

### Critical

| # | Issue | Type |
|---|-------|------|
| 1 | `Kind(null)` and `Mime(null)` throw unhandled `ArgumentNullException` — untested crash path | missing-coverage |

### Major

| # | Issue | Type |
|---|-------|------|
| 2 | `Name_UnknownType` test is a false green — tests `Uri` (no backtick) but misses `HashSet<string>` → `"hashset`1"` | false-green |
| 3 | `BuilderNames()` has zero tests — public API with dedup logic, feeds LLM builder | missing-coverage |
| 4 | `ComplexSchemas()` has zero tests — public API with reflection, couples to legacy TypeMapping | missing-coverage |

### Minor

| # | Issue | Type |
|---|-------|------|
| 5 | `Compressible()` doesn't test unknown kind boundary — unclear if deny-list is intentional | weak-assertion |
| 6 | No tests for PLang-specific types in Clr() — actor, goal.call, tstring | edge-case |
| 7 | No tests for nested generics or wrong-arity dicts in Clr() | edge-case |
| 8 | No PLang .goal tests — deferred to Phase 2, which is reasonable | missing-plang-test |

## What's good

- Add/Remove tests verify side effects (mapping actually changed), not just no-exception
- Clr() tests cover generics, nullable, MIME resolution, case insensitivity, null/empty — solid
- Kind() tests cover without-dot, case insensitivity, unknown extension, .key conflict
- Engine integration test verifies Types is wired to Engine

## Verdict: needs-fixes

Fix findings #1-#4 before passing to auditor. The null crash (#1) is the most urgent — it's a runtime exception path with no safety net.

## Code example — the false green

```csharp
// Current test (PASSES — false green):
[Test]
public async Task Name_UnknownType_ReturnsLowercaseName()
{
    await Assert.That(_types.Name(typeof(Uri))).IsEqualTo("uri");
    // Uri.Name = "Uri" → "uri" — no backtick, test passes
}

// Test that would CATCH the bug:
[Test]
public async Task Name_UnknownGenericType_ProducesValidName()
{
    // HashSet<string>.Name = "HashSet`1" → "hashset`1" — backtick!
    await Assert.That(_types.Name(typeof(HashSet<string>)))
        .DoesNotContain("`");  // This would FAIL, exposing the bug
}
```
