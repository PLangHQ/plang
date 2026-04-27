# Code Analysis — Action Modifiers Branch

## PLang/App/modules/IModifier.cs

### OBP Violations
None.

### Simplifications
None — single-method interface, minimal and clean.

### Readability
Clean. Good XML doc. The signature `Func<Task<Data.@this>> Wrap(...)` is clear.

### Verdict: CLEAN
Perfect interface. One method, one responsibility.

---

## PLang/App/modules/ModifierAttribute.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. `Order` property is self-documenting with the doc comment.

### Verdict: CLEAN

---

## PLang/App/modules/IDataWrappable.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean.

### Verdict: CLEAN

---

## PLang/App/modules/IStatic.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Good doc explaining the purpose and auto-wiring. Clean.

### Verdict: CLEAN

---

## PLang/App/Goals/Goal/Steps/Step/Actions/Action/Modifiers/this.cs

### OBP Violations
None. This is a textbook smart collection — owns the fold, delegates to each action for self-resolution via `WrapAround`.

### Simplifications
None — the right-to-left fold is as simple as it can be.

### Readability
Clean. The `RunAsync` method is easy to follow: early return for empty, fold loop, execute.

### Verdict: CLEAN
Excellent OBP compliance. Smart collection owns its domain operation.

---

## PLang/App/modules/timeout/after.cs

### OBP Violations
None.

### Simplifications
None — the cancellation logic is necessarily complex due to the parent-vs-self distinction.

### Readability
1. **Line 24: Comment block** — The three comments (lines 23-24, 33-34, 37-38) are valuable here since the cancellation semantics are subtle. Good.

### Behavioral Reasoning
1. **Line 39: `cts.IsCancellationRequested && !result.Success`** — This is the correct detection path. The coder's summary explains that `ExecuteAsync` wraps OCE into ServiceError, so `await next()` returns a failed Data rather than throwing. The `when` clause in the catch (line 45-46) correctly distinguishes parent cancellation from our timeout. Solid.

2. **Line 28: `context.PushCancellation(cts)`** — Push/Pop is balanced in try/finally. If `next()` throws an unexpected exception (not OCE), the `finally` still pops. Good.

### Verdict: CLEAN
Well-reasoned cancellation handling with proper parent-vs-self distinction.

---

## PLang/App/modules/cache/wrap.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. Clear cache-check-then-execute pattern.

### Behavioral Reasoning
1. **Line 24: `Key?.Value`** — If `Key` is a `Data<string>?` that's null, `Key?.Value` is null. If `Key` is a `Data<string>` with empty string value, `string.IsNullOrEmpty` catches it. Falls back to `DefaultKey`. Correct.

2. **Line 35: `cached.Name = "__data__"`** — Mutating the cached Data's Name. This is fine because the cache stores by key, not by Name. But if the same Data is shared across contexts... MemoryStepCache stores the reference directly. If two concurrent requests hit the cache, both would mutate Name on the same object. **Minor concern**: `Name = "__data__"` is idempotent (always the same value), so this is safe in practice.

3. **Line 49-53: DefaultKey** — Uses `step?.Goal?.Path` and `step?.Index`. If context.Step is null, key is `"step:unknown::"`. This shouldn't happen in practice since modifiers run inside Action.RunAsync which sets context.Step.

### Verdict: CLEAN

---

## PLang/App/modules/error/handle.cs

### OBP Violations
None.

### Simplifications
1. **Line 106-108: GoalCall parameter mutation** — `goalCall.Parameters.RemoveAll(...)` then `Add(...)` mutates the GoalCall deserialized from the .pr file. This is the **shared deserialized object**. If the action retries and fails again, `CallErrorGoal` is called again on the same `goalCall`. The RemoveAll+Add cycle is idempotent, so no functional bug. But it's worth noting that this mutates shared state. A cleaner approach would clone the GoalCall or build a new parameter list. **Low severity.**

### Readability
1. **Lines 39-64: GoalFirst/RetryFirst branching** — Two parallel branches with similar logic, differing in order. Each is 10 lines. Readable enough, though the symmetry makes it easy to miss the subtle differences.

### Behavioral Reasoning

1. **Line 48: Silent success after goal+retry both fail (GoalFirst)** — When `GoalFirst` is configured:
   - Goal runs and fails (line 44 check fails, falls through)
   - Retry runs and fails (line 47, retryResult is null)
   - Line 48: `if (goal != null) return Ok()` — returns success even though BOTH goal and retry failed
   
   **This is questionable.** The semantics seem to be "if you configured a goal, we consider the error handled regardless of outcome." Compare with `RetryFirst` path (lines 54-58): same behavior — if retry fails and goal is called, returns `Ok()` regardless of goal outcome (line 57 discards the result). This means configuring ANY error goal makes the error unconditionally handled.
   
   **Recommendation**: Either document this as intentional design ("error goals = unconditional handling") or return the goal's result so failures propagate. **Medium severity.**

2. **Line 91: Integer division** — `RetryOverMs.Value / count.Value` is integer division. 1000ms / 3 retries = 333ms per retry (999ms total, not 1000ms). Not a bug, but `RetryOverMs` semantically means "spread retries over this many ms" — the last 1ms is lost. **Negligible.**

3. **Line 96: Task.Delay with cancellation** — Correctly passes `context.CancellationToken`. If a timeout modifier wraps this, the timeout's CTS propagates through the pushed cancellation stack. A timeout during retry delay would cancel the delay immediately. Correct.

4. **Line 121: `goalCall.Action ??= context.Step?.Actions.FirstOrDefault()`** — This stamps the first action of the step onto the GoalCall, which is needed for sub-goal resolution. If a step has multiple actions and the error is on the second action, this incorrectly stamps the first. **However**, the modifier is attached to a specific action, and `context.Step?.Actions.FirstOrDefault()` is the typical single-action case. **Low severity** — only affects multi-action steps where the error action isn't the first.

### Deletion Test
- Lines 111-118 (callstack error recording): If deleted, no functional change — only debugging/callstack visibility is affected. But tests likely assert callstack behavior, so this is fine.

### Verdict: NEEDS WORK
The GoalFirst silent-success-after-double-failure (finding #1) is the main concern. The parameter mutation (#1 under Simplifications) is minor.

---

## PLang/App/modules/error/throw.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. Simple error factory.

### Verdict: CLEAN

---

## PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs

### OBP Violations
None. Excellent OBP:
- `RunAsync` delegates fold to `Modifiers.RunAsync` (Rule 1: behavior on the owner)
- `WrapAround` resolves its own handler (Rule 1: self-resolution)
- Navigation via `context.App!.Modules` (Rule 2: navigate, don't pass)

### Simplifications
None.

### Readability
Clean. `RunAsync` is 15 lines, clear flow: lifecycle → dispatch → modifiers → __data__ → lifecycle.

### Behavioral Reasoning
1. **Line 84-85: `result.Name = "__data__"; context.Variables.Put(result)`** — This publishes the action result as `%__data__%` for the next action in the step. But this only happens on success. If the modifier fold returns a failed result (e.g., timeout), `__data__` is NOT updated. This means the next action in the step would see the previous action's `__data__` or nothing. This is correct — a failed action shouldn't publish its result.

2. **Line 109: `await handler.ExecuteAsync(this, context)`** — This calls `ExecuteAsync` on the modifier handler, which populates its source-generated properties from this action's parameters. The `this` here is the modifier action (the one in the Modifiers list), not the parent action. Correct — each modifier action carries its own parameters.

### Verdict: CLEAN

---

## PLang/App/Goals/Goal/Steps/Step/Actions/this.cs (Actions collection)

### OBP Violations
None. Smart collection owns GroupModifiers — correct OBP.

### Simplifications
1. **Line 71-79: Sorting modifiers** — Creates a sorted copy, clears, re-adds. Could use a simpler approach but the current code is clear and correct.

### Readability
1. **Line 42: `public List<Action.@this> Value => _items;`** — Exposes the internal list as a mutable property. This is a data leak — callers can bypass the smart collection's methods. However, this exists for serialization and is an existing pattern. **Pre-existing, not introduced by this branch.**

### Behavioral Reasoning
1. **Line 56-68: Leading modifier handling** — "A leading modifier with no preceding executable is dropped" (line 50 doc). The code silently drops it (`current` starts null, so `current?.Modifiers.Add(action)` no-ops). No error, no warning. This is correct for robustness — a malformed LLM output shouldn't crash the builder.

### Verdict: CLEAN

---

## PLang/App/Goals/Goal/Steps/Step/this.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. The simplified `RunAsync` (no more timeout/error/cache handling) is much cleaner than before.

### Behavioral Reasoning
1. **Line 125: Exception filter** — `when (ex is not (OutOfMemoryException or StackOverflowException or OperationCanceledException))` — correctly re-throws OOM, SOE, and OCE. OCE is important because timeout modifiers rely on cancellation propagating. Good.

### Clone Family Audit
**Line 152-181: Step.Clone()** copies:
- Index, Text, LineNumber, Indent, Comment ✓
- Actions (deep copies each Action including Module, ActionName, Parameters) ✓
- **Modifiers** (copies each modifier with Module, ActionName, Parameters) ✓
- WaitForExecution, Goal, Intent, Errors, Warnings ✓

**Missing from modifier clone**: Defaults, Cacheable, Examples, ReturnType, ParameterSchema, Errors, Warnings. These are build-time properties, not needed at runtime. `Clone()` is used at runtime for step re-execution (e.g., foreach), so this is acceptable. **No action needed.**

### Verdict: CLEAN

---

## PLang/App/Modules/this.cs (AppModules registry)

### OBP Violations
None.

### Simplifications
None — the reflection-based Describe() is necessarily complex.

### Readability
Clean. Methods are well-organized: discovery → registration → resolution → queries.

### Behavioral Reasoning
1. **Line 130-133: IsModifier** — `type?.GetCustomAttribute<ModifierAttribute>() != null`. If the type is null (action not found), returns false. Correct — unknown actions are not modifiers.

2. **Line 140-144: GetModifierOrder** — Returns `int.MaxValue` for non-modifiers. Used by the sort in `GroupModifiers`. Non-modifier actions sort to the end, which is correct since they'd never be in the modifiers list anyway.

3. **Lines 184-186: Describe() — EqualityContract + Context skip** — Correctly filters out `EqualityContract` (record-generated protected property) and `Context` (IContext property). Without this filter, the builder prompt would include these as action parameters.

### Verdict: CLEAN

---

## PLang/App/modules/builder/providers/DefaultBuilderProvider.cs

### OBP Violations
None.

### Simplifications
1. **Line 352-365: ToStepList** — Three type checks (`List<object>`, `List<object?>`, `IList`). The `List<object?>` path filters nulls and converts. This is necessary because JSON deserialization may produce different list types. Acceptable complexity.

2. **Line 376-382: SetValue with JsonElement** — Writes to `Console.Error` when trying to set a value on a JsonElement. This is a graceful degradation for an edge case that shouldn't happen (steps should be deserialized as dictionaries). Fine.

### Readability
1. **Line 281: `public async Task<Data.@this> AppSave(...)`** — Marked async but only has a single await. No issue — the interface requires Task return.

### Behavioral Reasoning
1. **Line 159: GroupModifiers call in GoalsSave** — `step.Actions.GroupModifiers(app.Modules)` runs for every step in every goal on save. This is efficient since it's a single pass through the flat action list. Correct placement — after LLM output, before serialization.

2. **Lines 390-427: NormalizeParameterTypes** — Runs at build time. Stamps types from schema, converts string values to their declared types. Correctly skips variable references (`%var%`). The `TryConvertTo` on line 422 silently does nothing if conversion fails (destructured result ignores error). This is intentional — the LLM might produce values that don't convert, and the runtime handles them.

### Verdict: CLEAN

---

## PLang/App/Actor/Context/this.cs (changes only)

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean.

### Behavioral Reasoning
1. **Line 275-278: GetOrCreate wrapper cache** — `_wrapperCache.GetOrAdd(key, _ => factory())`. Uses reference equality (default for `ConcurrentDictionary<object, ...>`). For domain objects like Goal, Step, Action — these are reference types, so same instance → same wrapper. Correct.

2. **Clone family audit**: `Clone()` at line 250-265 copies `_data` but NOT `_wrapperCache`. This is correct — cloned contexts should build their own wrapper caches since the wrappers capture the context reference via `data.Context = context`. Sharing wrappers across contexts would be wrong.

3. **GetModuleStatic scoping** (lines 222-237): The `scope` parameter switch only handles `"app"` and default (context-local). The `"step"` and `"goal"` scopes mentioned in the doc are not implemented — they both fall to the default case. **Low severity** — the only current consumer is the timer module, which uses context-scoped (default). But the API promises more than it delivers.

### Verdict: CLEAN

---

## PLang/App/Data/this.cs (changes only)

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean.

### Behavioral Reasoning
1. **Line 162: IsVariable** — `_value is string s && s.StartsWith('%') && s.EndsWith('%') && s.Length > 2`. The `Length > 2` check ensures `"%%"` is not considered a variable. Correct.

2. **Line 169: HasVariableReference** — Uses `Regex.IsMatch(s, @"%[^%]+%")`. This regex is compiled on every access. For hot paths, this could be a performance concern. However, this is used at build-time validation, not runtime hot path. Fine.

3. **Line 467: NeedsResolution in ShallowClone** — `clone.NeedsResolution = NeedsResolution`. Correctly copies the resolution flag so the clone behaves the same as the original. Good.

### Clone Family Audit
**ShallowClone** (line 453-468): Copies Error, Handled, Returned, ReturnDepth, Warnings, Signature, Properties, _valueFactory, Context, NeedsResolution. ✓
**Clone** (line 474-491): Same as ShallowClone but deep-clones the value and Properties. ✓
Both methods correctly do NOT copy event handlers (OnChange, OnCreate, OnDelete) — documented as intentional.

### Verdict: CLEAN

---

## PLang/App/Errors/Error.cs (changes only)

### OBP Violations
None.

### Simplifications
1. **Lines 188-207: Verbose variable dump** — Navigates from error to app to context to variables. The fallback chain `errorContext ?? fallbackContext` is reasonable. The code is necessarily verbose because it's a diagnostic dump.

### Readability
Clean. The format is well-structured with emoji markers.

### Behavioral Reasoning
1. **Line 188: `error.Goal?.App`** — Goal has an `App` property. This navigates the object graph, which is OBP-correct. But if the error was created without a goal, `error.Goal?.App` is null, and it falls back to `error.Step?.Goal?.App`. Both could be null, in which case the verbose dump is skipped. Correct degradation.

### Verdict: CLEAN

---

## PLang/App/Cache/MemoryStepCache.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. Straightforward MemoryCache wrapper.

### Verdict: CLEAN

---

## PLang/App/Goals/Goal/Steps/Step/CacheSettings.cs

### OBP Violations
None.

### Simplifications
None — retained as needed by ICache.SetAsync signature.

### Verdict: CLEAN

---

## PLang/App/Goals/Goal/Steps/Step/ErrorOrder.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clear enum with good doc comment.

### Verdict: CLEAN

---

## PLang/App/modules/timer/sleep.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. Minimal action.

### Verdict: CLEAN

---

## PLang.Generators/LazyParamsGenerator.cs (changes only)

### OBP Violations
None — source generators operate at a meta level.

### Simplifications
None.

### Readability
The generated code is necessarily complex but follows the existing patterns.

### Behavioral Reasoning
1. **IStatic auto-provision** — Module namespace extraction: `info.Namespace.StartsWith(prefix) ? ... .Split('.')[0] : moduleNs`. For `App.modules.timer`, this gives `"timer"`. For a deeply nested module like `App.modules.foo.bar`, this gives `"foo"` (just the first segment). This means all actions in `App.modules.foo.*` share the same static dictionary keyed to `"foo"`. This is the designed behavior — module-scoped, not action-scoped.

2. **__ResolveData method** — Returns `Data` directly rather than extracting `.Value`. This is OBP-correct (Data.Value extraction only at system boundaries). When the resolved value is a `%variable%` reference, it resolves via `__variables.Get(...)` and returns the Data. When it's a literal, it returns the parameter Data with `NeedsResolution = true` and `Context` set. Good.

3. **ExecuteAsync reset** — The new code resets all backing fields when `action != null` (line "if (action != null) { ... }"). This ensures each execution resolves fresh from the action's parameters. The `action != null` guard preserves init-set values for direct construction (testing). Correct.

4. **Validation skip for Data<T>** — `if (... || prop.IsDataWrapped) continue;` skips null validation for Data<T> properties. This means a non-nullable `Data<T>` property without `[IsNotNull]` could receive a `Data.NotFound` value silently. The `[IsNotNull]` attribute is the explicit opt-in for validation. **This is by design** — Data<T> properties are always initialized (they get a Data wrapper even if the parameter is missing), so null-checking the Data itself is wrong. The `[IsNotNull]` checks the inner value.

### Verdict: CLEAN

---

## PLang/App/modules/builder/promoteGroups.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean — thin delegation to provider.

### Verdict: CLEAN

---

## PLang/App/modules/builder/validate.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean.

### Verdict: CLEAN

---

## PLang/App/modules/builder/merge.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean.

### Verdict: CLEAN

---

## PLang/App/modules/Attributes.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Well-documented attributes with clear XML docs.

### Verdict: CLEAN

---

# Summary of Findings

## Critical (0)
None.

## Medium (1)

1. **error.handle GoalFirst silent success** (`error/handle.cs:48,57`) — When error handling is configured with a goal AND retries, if both fail, the error is silently treated as success. This is either intentional design ("configuring a goal = error handled") or a bug. Needs explicit documentation or a fix. The behavior is symmetric in both GoalFirst and RetryFirst paths.

## Low (3)

1. **error.handle GoalCall parameter mutation** (`error/handle.cs:106-108`) — Mutates the deserialized GoalCall's parameters list. Idempotent in practice but violates immutability expectations on deserialized data.

2. **error.handle Action stamping** (`error/handle.cs:121`) — `goalCall.Action ??= context.Step?.Actions.FirstOrDefault()` stamps the first action, which may not be the failing action in multi-action steps.

3. **Context.GetModuleStatic scope overloading** (`Context/this.cs:222-237`) — The `scope` parameter promises step/goal/context scopes but only implements "app" and default. Dead code path for future expansion.

## Informational (2)

1. **Data.HasVariableReference regex** (`Data/this.cs:169`) — Regex.IsMatch on every access without caching. Fine for build-time usage, but would be a problem if used in a runtime hot path.

2. **Context._wrapperCache not in Clone()** — Intentionally excluded (correct behavior), but undocumented.
