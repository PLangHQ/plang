# Test Plan — Action-Based Conditions

## Overview

Tests for the architect's design: structured `condition.if` (Left/Operator/Right), `condition.compare`, `DefaultEvaluator`, sub-step execution in `Steps.RunAsync`, and full PLang pipeline integration.

Five C# batches, one PLang batch.

---

## C# Tests

### Batch 1: DefaultEvaluator.Evaluate() — All Operators (~22 tests)

Every operator with a positive and at least one negative/edge case. Focus: does the operator work correctly with valid inputs?

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `Evaluate_Equals_SameInts_ReturnsTrue` | `==` matching ints |
| 2 | `Evaluate_Equals_DifferentInts_ReturnsFalse` | `==` non-matching |
| 3 | `Evaluate_NotEquals_DifferentValues_ReturnsTrue` | `!=` positive |
| 4 | `Evaluate_NotEquals_SameValues_ReturnsFalse` | `!=` negative |
| 5 | `Evaluate_GreaterThan_LeftBigger_ReturnsTrue` | `>` positive |
| 6 | `Evaluate_GreaterThan_Equal_ReturnsFalse` | `>` on equal values |
| 7 | `Evaluate_LessThan_LeftSmaller_ReturnsTrue` | `<` positive |
| 8 | `Evaluate_LessThan_Equal_ReturnsFalse` | `<` on equal values |
| 9 | `Evaluate_GreaterOrEqual_EqualValues_ReturnsTrue` | `>=` boundary |
| 10 | `Evaluate_GreaterOrEqual_LeftSmaller_ReturnsFalse` | `>=` negative |
| 11 | `Evaluate_LessOrEqual_EqualValues_ReturnsTrue` | `<=` boundary |
| 12 | `Evaluate_Contains_SubstringPresent_ReturnsTrue` | `contains` positive |
| 13 | `Evaluate_Contains_SubstringAbsent_ReturnsFalse` | `contains` negative |
| 14 | `Evaluate_Contains_CaseInsensitive_ReturnsTrue` | `contains "WORLD"` on `"hello world"` |
| 15 | `Evaluate_StartsWith_MatchingPrefix_ReturnsTrue` | `startswith` positive |
| 16 | `Evaluate_StartsWith_NonMatchingPrefix_ReturnsFalse` | `startswith` negative |
| 17 | `Evaluate_EndsWith_MatchingSuffix_ReturnsTrue` | `endswith` positive |
| 18 | `Evaluate_In_ValueInList_ReturnsTrue` | `in` with list collection |
| 19 | `Evaluate_In_ValueNotInList_ReturnsFalse` | `in` negative |
| 20 | `Evaluate_IsEmpty_EmptyList_ReturnsTrue` | `isEmpty` on empty list |
| 21 | `Evaluate_IsEmpty_NonEmptyList_ReturnsFalse` | `isEmpty` negative |
| 22 | `Evaluate_IsEmpty_NullValue_ReturnsTrue` | `isEmpty` on null (empty = nothing there) |

### Batch 2: DefaultEvaluator — Logical, Unary, Edge Cases (~14 tests)

Logical operators, NOT, type normalization, null handling, unsupported operator.

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `Evaluate_Not_TruthyValue_ReturnsFalse` | `NOT` on truthy → false |
| 2 | `Evaluate_Not_FalsyValue_ReturnsTrue` | `NOT` on falsy → true |
| 3 | `Evaluate_And_BothTrue_ReturnsTrue` | `AND` both truthy |
| 4 | `Evaluate_And_OneFalse_ReturnsFalse` | `AND` short-circuit |
| 5 | `Evaluate_Or_BothFalse_ReturnsFalse` | `OR` both falsy |
| 6 | `Evaluate_Or_OneTrueOneFalse_ReturnsTrue` | `OR` one truthy |
| 7 | `Evaluate_IntVsLong_NormalizesAndCompares` | JSON boxing: `(int)5 == (long)5` |
| 8 | `Evaluate_IntVsDouble_NormalizesAndCompares` | `(int)5 > (double)4.5` |
| 9 | `Evaluate_StringVsInt_ConvertsStringAndCompares` | `"5" == 5` |
| 10 | `Evaluate_NullEqualsNull_ReturnsTrue` | `null == null` |
| 11 | `Evaluate_NullNotEqualsValue_ReturnsTrue` | `null != 5` |
| 12 | `Evaluate_NullGreaterThan_ReturnsFalse` | `null > 5` — can't compare, false |
| 13 | `Evaluate_StringEquality_CaseInsensitive` | `"Hello" == "hello"` |
| 14 | `Evaluate_UnsupportedOperator_ThrowsNotSupported` | Unknown op throws `NotSupportedException` |

### Batch 3: DefaultEvaluator.IsTruthy() (~10 tests)

Separate batch because IsTruthy is the foundation for truthy checks, NOT, AND, OR.

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `IsTruthy_Null_ReturnsFalse` | null is falsy |
| 2 | `IsTruthy_BoolTrue_ReturnsTrue` | bool true |
| 3 | `IsTruthy_BoolFalse_ReturnsFalse` | bool false |
| 4 | `IsTruthy_ZeroInt_ReturnsFalse` | `0` is falsy |
| 5 | `IsTruthy_NonZeroInt_ReturnsTrue` | `42` is truthy |
| 6 | `IsTruthy_ZeroLong_ReturnsFalse` | `0L` is falsy |
| 7 | `IsTruthy_ZeroDouble_ReturnsFalse` | `0.0` is falsy |
| 8 | `IsTruthy_EmptyString_ReturnsFalse` | `""` is falsy |
| 9 | `IsTruthy_WhitespaceString_ReturnsFalse` | `"  "` is falsy (uses IsNullOrWhiteSpace) |
| 10 | `IsTruthy_NonEmptyString_ReturnsTrue` | `"hello"` is truthy |
| 11 | `IsTruthy_EmptyCollection_ReturnsFalse` | empty `List<object>` is falsy |
| 12 | `IsTruthy_NonEmptyCollection_ReturnsTrue` | populated list is truthy |
| 13 | `IsTruthy_ArbitraryObject_ReturnsTrue` | non-null object with no special handling is truthy |

### Batch 4: condition.if Handler (~8 tests)

Tests for the `If` action record's `Run()` method. Handler branching logic, evaluator delegation, goal calls, sub-step mode.

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `Run_NoOperator_TruthyLeft_ReturnsTrue` | Truthy check when Operator is null |
| 2 | `Run_NoOperator_FalsyLeft_ReturnsFalse` | Falsy check when Operator is null |
| 3 | `Run_WithOperator_DelegatesToEvaluator` | Evaluator called with Left/Op/Right |
| 4 | `Run_ConditionTrue_GoalIfTrue_CallsGoal` | Calls GoalIfTrue on true |
| 5 | `Run_ConditionFalse_GoalIfFalse_CallsGoal` | Calls GoalIfFalse on false |
| 6 | `Run_ConditionTrue_NoGoalIfTrue_ReturnsTrueNoCall` | True with no goal → just returns true (sub-step mode) |
| 7 | `Run_ConditionFalse_NoGoals_ReturnsFalse` | False with no goals → returns false (sub-step mode) |
| 8 | `Run_GoalExecutionFails_PropagatesError` | Goal returns error → If propagates it |

### Batch 5: condition.compare Handler (~3 tests)

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `Run_GreaterThan_ReturnsDataWithTrue` | Returns `Data.Ok(true)` |
| 2 | `Run_GreaterThan_Fails_ReturnsDataWithFalse` | Returns `Data.Ok(false)` |
| 3 | `Run_ResultValueIsBool` | `Data.Value` is specifically `bool`, not boxed int or string |

### Batch 6: Steps.RunAsync Sub-Step Logic (~9 tests)

Tests for `skipBelowIndent` logic. These need mock steps that return specific Data values.

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `RunAsync_FalseCondition_SkipsIndentedChildren` | Steps at higher indent skipped when condition false |
| 2 | `RunAsync_TrueCondition_ExecutesIndentedChildren` | Indented steps run when condition true |
| 3 | `RunAsync_FalseCondition_ResumesAtSameIndent` | Step at same indent after skipped block runs normally |
| 4 | `RunAsync_NestedConditions_InnerFalseSkipsOnlyInner` | Outer true + inner false → only inner block skipped |
| 5 | `RunAsync_NoIndentedChildren_FalseDoesNotSkip` | False step with no children below doesn't skip the next step |
| 6 | `RunAsync_TwoConsecutiveConditions_EachControlsOwnBlock` | Two conditions at indent 0, each with their own indented block — false first, true second |
| 7 | `RunAsync_DeeplyNested_ThreeLevels` | Indent 0 → 4 → 8, all true → all execute |
| 8 | `RunAsync_NonConditionStep_FalseValue_DoesNotSkip` | A non-condition step returning false should NOT trigger skip logic |
| 9 | `RunAsync_HasIndentedChildren_CorrectDetection` | Helper method: true when next step has higher indent, false otherwise |

---

## PLang Tests

### Batch 7: PLang Pipeline Integration (~18 tests)

Full pipeline: `.goal` → builder → `.pr` → runtime. One test suite per directory.

**Basic operators:**

| # | Directory | Test goal | What it verifies |
|---|-----------|-----------|-----------------|
| 1 | `ConditionTruthy/` | `if %flag%, call HandleTrue` | Truthy check branches to GoalIfTrue |
| 2 | `ConditionFalsy/` | `if %flag%, call HandleTrue, else call HandleFalse` where flag is false | Else branch on falsy value |
| 3 | `ConditionGreaterThan/` | `if %x% > 5, call WhenGreater` | `>` with goal call |
| 4 | `ConditionLessThan/` | `if %y% < 10, call WhenLess` | `<` with goal call |
| 5 | `ConditionEquals/` | `if %z% == 5, call WhenEqual` | `==` equality |
| 6 | `ConditionNotEquals/` | `if %a% != 3, call WhenNotEqual` | `!=` inequality |
| 7 | `ConditionGTE/` | `if %x% >= 5, call WhenGTE` | `>=` boundary |
| 8 | `ConditionLTE/` | `if %x% <= 5, call WhenLTE` | `<=` boundary |

**String operators:**

| # | Directory | Test goal | What it verifies |
|---|-----------|-----------|-----------------|
| 9 | `ConditionContains/` | `if %name% contains "world", call WhenContains` | String contains |
| 10 | `ConditionStartsWith/` | `if %name% startswith "hello", call WhenStarts` | String startswith |
| 11 | `ConditionEndsWith/` | `if %name% endswith "world", call WhenEnds` | String endswith |

**Sub-step execution:**

| # | Directory | Test goal | What it verifies |
|---|-----------|-----------|-----------------|
| 12 | `ConditionSubStepsTrue/` | `if %flag%` with indented `set` + `assert` | Sub-steps execute on true |
| 13 | `ConditionSubStepsFalse/` | `if %flag%` (false) with indented steps → assert they didn't run | Sub-steps skipped on false |
| 14 | `ConditionNested/` | Nested `if` blocks, inner false → only inner block skipped | Nested sub-step control |

**Compound & logical:**

| # | Directory | Test goal | What it verifies |
|---|-----------|-----------|-----------------|
| 15 | `ConditionCompoundAnd/` | `if %a% > 1 and %b% < 10, call DoThing` | AND via compare actions |
| 16 | `ConditionCompoundOr/` | `if %a% == 1 or %b% == 2, call DoThing` (first false, second true) | OR via compare actions |
| 17 | `ConditionNot/` | `if not %flag%, call WhenFalse` | Negation |

**Else branch:**

| # | Directory | Test goal | What it verifies |
|---|-----------|-----------|-----------------|
| 18 | `ConditionElseBranch/` | `if %x% > 100, call A, else call B` — verify B is called | Both true and false goal paths in one step |

**Action-based conditions (multi-action steps):**

| # | Directory | Test goal | What it verifies |
|---|-----------|-----------|-----------------|
| 19 | `ConditionFileExists/` | `if file.txt exists, call ProcessFile` — create file, verify goal runs | Multi-action: `file.exists` → `condition.if` (truthy check on exists result) |
| 20 | `ConditionFileNotExists/` | `if missing.txt exists, call A, else call B` — verify B runs | Multi-action: `file.exists` returns false → else branch |
| 21 | `ConditionFileExistsSubSteps/` | `if file.txt exists` with indented steps | Multi-action + sub-step mode combined |

---

## What is NOT Tested (and why)

| Pattern | Reason |
|---------|--------|
| Action + comparison (`if select count(*) from users > 0`) | Depends on db module. The multi-action pattern is tested via `file.exists` and compound conditions. |
| `Libraries.GetProvider<T>()` | New method on Libraries — needs its own test when implemented, but it's generic infrastructure, not condition-specific. |
| Builder prompt changes (`BuildGoal.llm`) | Can't unit-test LLM behavior. PLang pipeline tests validate the builder produces correct .pr files. |
| Concurrent sub-step execution | Thread safety is architectural (local variable) — can't meaningfully test without a concurrent runtime harness. The design guarantees it by construction. |

---

## Totals

| Area | Count |
|------|-------|
| C# — DefaultEvaluator operators | 22 |
| C# — DefaultEvaluator logical/edge | 14 |
| C# — DefaultEvaluator IsTruthy | 13 |
| C# — condition.if handler | 8 |
| C# — condition.compare handler | 3 |
| C# — Steps sub-step logic | 9 |
| PLang — Pipeline integration | 21 |
| **Total** | **90** |
