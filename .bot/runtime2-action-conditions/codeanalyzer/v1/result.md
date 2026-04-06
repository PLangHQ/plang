# Code Analysis — Action-Based Conditions (Coder v1)

## PLang/App/modules/condition/providers/IEvaluator.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
None — 7 lines, clean interface.

### Verdict: CLEAN
Minimal interface, correct abstraction.

---

## PLang/App/modules/condition/providers/DefaultEvaluator.cs

### OBP Violations
None. DefaultEvaluator is a pure logic class with no dependency on the object graph.

### Simplifications

1. **Line 130: `WiderNumericType` allocates array every call** — The `order` array is created on every invocation. Should be a `static readonly` field since it never changes.
   ```csharp
   // Current (line 130)
   var order = new[] { typeof(byte), typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) };

   // Simplified
   private static readonly Type[] NumericOrder = { typeof(byte), typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) };
   ```

2. **Class is not `sealed`** — DefaultEvaluator has no virtual methods and is not designed for extension. It should be `sealed` to express intent and prevent accidental inheritance. The pluggability point is `IEvaluator`, not subclassing.

### Readability

1. **Line 59: Dense ternary in `Compare`** — `left == null && right == null ? 0 : (left == null ? -1 : 1)` packs three branches into one line. Correct but takes a moment to parse. Minor — not worth changing given its private scope.

### Behavioral Reasoning

1. **Line 28: `throw new NotSupportedException` in `Evaluate`** — This throw propagates through `If.Run()` and `Compare.Run()`, which return `Task<Data>`. The source generator wraps `Run()` in a try/catch that converts exceptions to `ServiceError`, so this is **safe at runtime**. However, it's a fragile contract: the exception becomes a generic ServiceError instead of a descriptive `Data.Fail`. If the generator's catch behavior ever changes, this breaks silently. **Low risk** — the generator is stable, and unsupported operators are a builder bug, not a user error.

2. **Line 61: `IComparable.CompareTo` with mismatched types** — After `NormalizeTypes`, both sides should be the same type. But if they aren't (e.g., comparing a string to an int where string-to-number parsing failed), `CompareTo` will throw `ArgumentException`. This propagates the same way as finding #1. **Low risk** — NormalizeTypes handles the widening path correctly for the numeric case.

3. **Line 70: `IEnumerable.Cast<object>().Contains(right)` in `Contains`** — Uses reference equality for non-primitive types via `Enumerable.Contains`. For boxed numerics (int vs long), this could return false even when values are logically equal (e.g., `boxed int 5` vs `boxed long 5L`). NormalizeTypes isn't called for Contains since it operates on collections. **Medium risk** — a PLang user writing `if %list% contains 5` where the list has `5L` values would get false.

### Deletion Test

1. **Lines 90-96 (`IsEmpty`)** — Only reachable via the `"isempty"` operator branch. If no test sends `"isempty"` operator, these lines are dead. (49 tests exist; likely covered but worth verifying.)

2. **Lines 125-126 (`IsNumeric`)** — `short` and `byte` are listed but unlikely to appear in PLang (System.Text.Json boxes to int/long/double). These branches may be unreachable from real PLang data. Not a bug — defensive coding — but they're never exercised in practice.

### Verdict: NEEDS WORK
Two simplifications (seal the class, static readonly array). One medium-risk behavioral concern (Contains with boxed numerics).

---

## PLang/App/modules/condition/if.cs

### OBP Violations
None. Navigation through `Context.Engine!`, `Context.Variables` is correct. GoalCall is passed as-is to RunGoalAsync (navigate, don't pass).

### Simplifications

1. **Line 18: `new DefaultEvaluator()` on every call** — Creates a new instance per execution. DefaultEvaluator is stateless — it could be a static field or a shared instance. Both `if.cs` and `compare.cs` duplicate this instantiation.
   ```csharp
   // Current (duplicated in if.cs:18 and compare.cs:15)
   var evaluator = new DefaultEvaluator();

   // Simplified: static readonly on DefaultEvaluator itself
   // (or a shared constant, since it's stateless)
   private static readonly IEvaluator Evaluator = new DefaultEvaluator();
   ```
   **Note:** The architect plan envisions `Libraries.GetProvider<IEvaluator>()` — which is the OBP-correct long-term form (navigate through Engine). The `new` is a known placeholder. Flagging to ensure it doesn't become permanent.

### Readability
Clean. 38 lines, clear flow: evaluate → signal → branch → return.

### Behavioral Reasoning

1. **Line 32: `Context.Engine!` null-forgiving** — Engine is set by the source generator before `Run()` is called. The `!` is safe because the generator guarantees initialization. Still, if Engine were null, you'd get a NullReferenceException instead of a descriptive error. Acceptable — this is an internal invariant, not a user-facing boundary.

2. **Line 26: `__condition__` signal timing** — Set *before* GoalIfTrue/GoalIfFalse runs. If the goal itself contains conditions with sub-steps, those would operate on a child context (via RunGoalAsync), so the parent's `__condition__` survives untouched. **Thread-safe.** The signal lifecycle is: If.Run sets → Actions.RunAsync returns merged result → Steps.RunAsync reads and removes. This is sequential within a single context.

3. **Line 36: `Data.Ok(result)` where result is bool** — The coder documented that `Data.Merge` loses this bool (casts Value to `List<Data>`). The `__condition__` Variables signal bypasses Merge entirely. The returned `Data.Ok(true)` is still useful for callers who don't go through Merge (e.g., direct `await action.RunAsync()`). Correct design.

### Deletion Test
All 8 IfHandlerTests cover the paths here. No untested code paths visible.

### Verdict: CLEAN
One simplification note (static evaluator), but it's a known placeholder per architect plan.

---

## PLang/App/modules/condition/compare.cs

### OBP Violations
None.

### Simplifications

1. **Line 15: Duplicate `new DefaultEvaluator()`** — Same as if.cs finding. Both should share a single evaluator instance.

### Readability
Clean. 19 lines, single-purpose.

### Behavioral Reasoning

1. **Line 11: `string Operator` is non-nullable** — The source generator enforces non-nullable parameters. If the builder doesn't provide an operator, the generated code throws before `Run()` is called. Correct — `compare` always requires an operator (unlike `if`, where null operator means truthy check).

### Deletion Test
3 CompareHandlerTests cover the happy paths. No explicit test for the generator's null-operator validation, but that's the generator's responsibility, not compare's.

### Verdict: CLEAN

---

## PLang/App/Engine/Goals/Goal/Steps/this.cs

### OBP Violations
None. Steps owns its iteration loop (OBP rule 5). Sub-step logic lives where the loop is — correct placement.

### Simplifications

1. **Lines 24-35: Duplicate `<summary>` XML comments** — Two `<summary>` blocks on `RunAsync`. The first (lines 24-28) describes run-once semantics. The second (lines 29-35) describes sub-step logic. These should be merged into one `<summary>`.

### Readability

1. **Lines 24-35: Duplicate summary** — Creates confusion about which is authoritative. Merge them.

2. **Line 73: `conditionSignal.Value is not true`** — Uses C# 9 pattern matching. Reads clearly: "if the signal's value is not boolean true, skip children." The `is not true` form handles null, false, and non-bool values correctly. Good.

### Behavioral Reasoning

1. **Lines 67-76: `__condition__` consumption order** — The signal is checked *after* `step.RunAsync` completes and only when `HasIndentedChildren(i)` is true. If a non-condition step happens to set `__condition__` (a future bug or a plugin that writes to the same key), it would be consumed here and affect sub-step flow. **Mitigation:** The key name `__condition__` is internal convention. The double-underscore prefix signals "reserved." Low risk.

2. **Line 69: `context.Variables.Get("__condition__")` returns `Data?`** — The Get method returns a `Data` object. `conditionSignal.Value` extracts the inner value. If someone stored `Data.Ok(true)` instead of raw `true`, the Value would be `true`. But `If.Run()` stores via `Variables.Set("__condition__", result)` where result is `bool`. The Variables wraps it in `Data` internally. So `conditionSignal.Value` is the raw `bool`. Correct.

3. **Line 51: `step.Indent > skipBelowIndent`** — Uses strict greater-than. If two consecutive conditions are at the same indent level, the second one correctly resets `skipBelowIndent = null` (line 54) because its indent is NOT greater. This handles consecutive if-blocks at the same level correctly.

4. **Line 111: `HasIndentedChildren` is `public`** — This method is only called from within `RunAsync` (line 67). Making it public exposes an implementation detail of the sub-step algorithm. Should be `private`. However, tests might call it directly — if so, make it `internal` with `[InternalsVisibleTo]`.

### Deletion Test

1. **Line 111-114: `HasIndentedChildren`** — If made private and the sub-step tests only test through `RunAsync`, this method is tested implicitly. But if tests call `HasIndentedChildren` directly, making it private would break them. Check test files.

### Verdict: NEEDS WORK
Duplicate summary comments. `HasIndentedChildren` should not be public.

---

## Cross-File Findings

### Finding 1: DefaultEvaluator instantiation is duplicated
Both `if.cs:18` and `compare.cs:15` do `new DefaultEvaluator()`. Since DefaultEvaluator is stateless, a single `static readonly` instance would suffice. Long-term, this should navigate through `Engine.Libraries.GetProvider<IEvaluator>()` per architect plan. **Priority: low** — known placeholder, in todos.

### Finding 2: DefaultEvaluator.Contains with boxed numerics
`Contains` (line 65-73) checks `IEnumerable.Cast<object>().Contains(right)`. For boxed value types, this uses reference equality (via `Object.Equals`). For `int` vs `long`, `int.Equals(long)` returns `false` even if values are equal. `NormalizeTypes` is NOT called for collection operations. A PLang step like `if %myList% contains 5` where myList has `5L` values would incorrectly return false.

**Severity: medium** — This will surface when the builder generates `contains` operations with mixed numeric types. Fix: call `NormalizeTypes` inside `Contains` for each element, or use the `AreEqual` method for element comparison instead of `Enumerable.Contains`.

### Finding 3: `__condition__` signal is robust
The signal pattern follows established conventions (`__stepResult`, `__error__`). It's set synchronously, consumed immediately, and cleaned up. Child goals run in child contexts, so no cross-contamination. Thread-safe by design.

### Finding 4: Source generator compatibility confirmed
Both `If : IContext` and `Compare : IContext` follow the same pattern the generator handles. The generator auto-provisions `Context`, generates `CodeGeneratedExecuteAsync`, and wraps `Run()` in try/catch. No compatibility issues.

---

## Overall Verdict: NEEDS WORK

**Key issues (coder should fix):**
1. Duplicate `<summary>` on Steps.RunAsync — merge into one
2. `HasIndentedChildren` should be `private` (or `internal` if tests need it)
3. `DefaultEvaluator` should be `sealed`
4. `WiderNumericType` array should be `static readonly`

**Design concern (track, don't block):**
5. `Contains` with boxed numerics returns incorrect results for mixed types — medium severity, will surface when builder generates `contains` with numeric collections

**Acknowledged known gaps (in todos, don't block):**
6. `new DefaultEvaluator()` hardcoding — placeholder until `Libraries.GetProvider<T>()` exists
