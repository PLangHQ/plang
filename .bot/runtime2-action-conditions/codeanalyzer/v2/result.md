# Code Analysis — Action-Based Conditions (v2 Re-review)

## V1 Fix Verification

| # | Finding | Status | Evidence |
|---|---------|--------|----------|
| 1 | Seal DefaultEvaluator | FIXED | Line 6: `public sealed class DefaultEvaluator` |
| 2 | Static readonly array | FIXED | Line 142-143: `private static readonly Type[] NumericOrder` |
| 3 | Contains boxing bug | FIXED | Lines 78-87: new `ContainsElement` with `NormalizeTypes` + `AreEqual` per element |
| 4 | HasIndentedChildren visibility | FIXED | Line 106: `internal bool HasIndentedChildren` |
| 5 | Duplicate summary on RunAsync | FIXED | Lines 24-30: single merged `<summary>` block |

All 5 fixes confirmed.

---

## PLang/App/modules/condition/providers/DefaultEvaluator.cs

### OBP Violations
None.

### Simplifications
None remaining. The `WiderNumericType` local alias (`var order = NumericOrder`) on line 147 is fine — local reads of a static field are idiomatic.

### Readability
Clean. The new `ContainsElement` method (lines 78-87) is well-named, has a clear doc comment, and follows the same pattern as `AreEqual`.

### Behavioral Reasoning

1. **Lines 82-83: NormalizeTypes per element in ContainsElement** — Each collection element is normalized against the target before comparison. This correctly handles `List<long>{5L}` contains `int 5` (both widen to long). Also handles `List<int>{5}` contains `long 5L`. The `In` operator (line 100) reuses `ContainsElement`, so both `contains` and `in` are fixed consistently. Good.

2. **Line 147: `var order = NumericOrder`** — Local alias of a static readonly field. The array itself is immutable in practice (no code writes to it), but `Type[]` is technically mutable. If any future code did `NumericOrder[0] = typeof(string)`, it would corrupt globally. `ReadOnlySpan<Type>` or `IReadOnlyList<Type>` would prevent this. **Negligible risk** — this is internal code with no external consumers. Not worth flagging as actionable.

### Deletion Test

1. **Lines 78-87 (`ContainsElement` with NormalizeTypes)** — No test passes `List<long>` with an `int` target or vice versa. The existing Contains tests only cover string substrings (lines 78-92 of test file). `ContainsElement`'s `NormalizeTypes` call could be removed (reverting to plain `AreEqual` without normalization) and no test would fail. The fix is correct but **unproven by tests**. Recommend adding: `Evaluate(new List<long>{5L}, "contains", (int)5)` should return `true`.

### Verdict: CLEAN (with one test gap)
All v1 fixes applied correctly. One unproven code path in ContainsElement (mixed numeric types in collections).

---

## PLang/App/Engine/Goals/Goal/Steps/this.cs

### OBP Violations
None.

### Simplifications
None remaining.

### Readability
Clean. Single `<summary>` block covers all concerns (run-once, sub-step logic, thread safety). Comments in the loop body are concise and explain *why*, not *what*.

### Behavioral Reasoning
No new concerns. The `internal` visibility of `HasIndentedChildren` is correct — tests access it via `InternalsVisibleTo` (confirmed: `StepsSubStepTests.cs:272` calls it directly).

### Deletion Test
No new gaps.

### Verdict: CLEAN

---

## PLang/App/modules/condition/if.cs

No changes from v1. Verdict remains: **CLEAN**.

---

## PLang/App/modules/condition/compare.cs

No changes from v1. Verdict remains: **CLEAN**.

---

## Overall Verdict: CLEAN

All 5 v1 findings are fixed correctly. One test gap remains: `ContainsElement`'s NormalizeTypes path for mixed numeric types in collections has no test coverage. This is a test gap, not a code bug — the implementation is correct. Recommend the tester add a test for `Evaluate(new List<long>{5L}, "contains", (int)5)`.
