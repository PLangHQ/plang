# v6 — Plan: Fix silent-error swallowing + formal rendering of complex values

## Background

v5 left the builder unable to self-rebuild `/builder/ApplyStep.goal`. Symptom:
ApplyBuiltStep step 0 saved with 0 actions. The original handoff blamed a
list<action> merge bug, then corrected to "Fluid-wrapper type-string
ends up in goal.call.Name". Both are symptoms.

The 3-whys trace (this session):

1. **Why does step 0 stay empty?** Apply chain never runs — `goal.call` has
   garbage `Name` → goal lookup fails.
2. **Why is `goal.call.Name` garbage?** LLM echoed it back from the prompt's
   `@known:` section. On save, `TypeMapping` line 533 accepts any string as a
   goal name.
3. **Why does the prompt contain garbage?** `FormatFormalValue` in
   `FluidProvider.cs` calls `input.ToObjectValue()` on a `Fluid.Values.DictionaryValue`
   — which returns an `ObjectDictionaryFluidIndexable<object>` wrapper, **not**
   the underlying dict. Verified empirically: the wrapper is not `IDictionary`,
   falls through to `v.ToString()` → Fluid class-name leaks into the prompt.

**But why didn't this scream the first time?** Because when the garbage
goal.call executes, `GoalCall.GetGoalAsync` DOES return a proper 404
`GoalNotFound` error. The error gets **swallowed** by `loop/foreach.cs:50`:

```csharp
if (!result.Success && !result.Handled) return result;
```

`condition.if` correctly stamps `Handled=true` on its orchestrated result
(this is needed so `Step.RunAsync` doesn't re-iterate the branch actions
it already ran). But `loop.foreach` conflates `Handled` with "error is fine"
and silently continues. Every `foreach, call ApplyStep` run loops through
all items reporting success while every inner call errored.

This is the priority. Fixing it means any future version of this class of
bug gets caught on the first build.

## Scope

Three fixes, in order:

### 1. Propagate errors from `loop.foreach` body

`PLang/App/modules/loop/foreach.cs:50`

```csharp
- if (!result.Success && !result.Handled) return result;
+ if (!result.Success) return result;
```

`Handled` stays for its real purpose (telling `Step.RunAsync` "don't iterate
remaining step actions") but never suppresses error propagation.

### 2. Simplify `formal` filter + `FormatValue` — scalar-or-JSON

Both `FluidProvider.FormatFormalValue` (`PLang/App/modules/ui/providers/FluidProvider.cs:128`)
and `DefaultBuilderProvider.FormatValue` (`PLang/App/modules/builder/providers/DefaultBuilderProvider.cs:414`)
share the same bug pattern: they fall through to `v.ToString()` for anything
not string/bool/IDict/IList.

New rule, both places:

```
null                  → "null"
string starts with %  → bare
string with space/,   → "quoted"
string                → bare
bool                  → true/false
IConvertible (numbers, enums with ToString)
                      → v.ToString()
anything else         → JsonSerializer.Serialize(v)
```

The key change: the final fallback becomes JSON serialize, not `v.ToString()`.
No Fluid wrappers, no POCO type-names, no surprises. Goal.call values,
dict-typed params, any nested structure — all render as JSON.

For the `formal` filter specifically: continue to call `input.ToObjectValue()`
(Fluid's unwrapping API), but the scalar-vs-JSON rule above handles the
case where ToObjectValue returns a wrapper rather than the raw dict.

### 3. Clear the poisoned .pr and rebuild

`system/builder/.build/applystep.pr` has `Fluid.Values...` strings in its
`goal.call` params. With #1 and #2 in place, the next rebuild will:
- Render `p.Value | formal` correctly (JSON → `{"name":"ApplyKeptStep","parameters":[]}`)
- LLM sees correct goal name in `@known`, echoes it back
- TypeMapping creates `GoalCall { Name = "ApplyKeptStep" }`
- Self-rebuild works

But the stored .pr is already poisoned — the first rebuild still sends
`Fluid.Values...` via `@known:` unless the .pr is fixed first. Manually
edit `applystep.pr` to replace the two bad `value: {...}` blobs with
`value: "ApplyKeptStep"` / `value: "ApplyBuiltStep"`. Then rebuild.

## Unit tests

### New: `PLang.Tests/App/Modules/loop/ForeachErrorPropagationTests.cs`

Three tests for the regression — all hit the real path, no mocks:

1. **`Foreach_BodyActionReturnsError_PropagatesError`** — foreach over 3 items,
   body is `goal.call NonExistentGoal`. Assert the overall result is `!Success`,
   `Error.Key == "GoalNotFound"`, and only the first iteration ran (not 3).
2. **`Foreach_BodyActionReturnsErrorWithHandled_StillPropagates`** — the exact
   scenario from this bug: body is a `condition.if + goal.call NonExistentGoal`
   chain. condition.if stamps `Handled=true` on the error. Assert error still
   propagates from the outer foreach.
3. **`Foreach_BodyActionSucceeds_CompletesAllIterations`** — sanity check that
   the fix didn't break the happy path.

### New: `PLang.Tests/App/Modules/condition/IfErrorOrchestrationTests.cs`

Two tests:

1. **`If_OrchestratedBranchActionErrors_ReturnsError`** — step with
   `[condition.if (true), goal.call NonExistentGoal]`. Call `step.RunAsync()`.
   Assert `!Success`, `Error.Key == "GoalNotFound"`.
2. **`If_OrchestratedResult_IsMarkedHandled_OnSuccess`** — happy path: assert
   successful orchestration sets `Handled=true` (so Step.RunAsync breaks).
   This pins the existing correct behavior so it doesn't regress.

### PLang-level test: `tests/v0.2/loop/ForeachErrorPropagation.test.goal`

End-to-end check with a real goal file — foreach over items, inner call
to a missing goal. Assert the build terminates with the 404 error, not
a silent success. This catches any future regression in the
builder→LLM→.pr→runtime chain that re-introduces silent swallowing.

## Why not just `return await Orchestrate(...)` in condition.if?

Answered: because `Step.RunAsync` iterates `Step.Actions` one-by-one
(`Step/this.cs:127-132`). `Orchestrate` already runs all branch actions.
Without `Handled=true`, `Step.RunAsync` continues to the next sibling and
re-executes everything `Orchestrate` already ran. The `Handled` flag is
condition.if's signal to Step: "I consumed the siblings, don't iterate
them again."

`loop.foreach.cs:57` sets the same flag for the same reason (its body is
"remaining step actions"). Both writes are correct. The **read** in
`loop.foreach.cs:50` is the only misuse.

## All Handled sites audited

**Writes (3, all legitimate):**
- `condition/if.cs:54` — orchestrated branch consumed siblings
- `loop/foreach.cs:57` — foreach body consumed siblings
- `Events/Lifecycle/Bindings/Binding/this.cs:60` — event override replacing action

**Reads (4 correct, 1 buggy):**
- `Step/this.cs:122, 131` — correct: break step loop
- `Goal/this.cs:280` — correct: event override short-circuits goal
- `Bindings/this.cs:39` — correct: stop running more bindings
- `loop/foreach.cs:50` — **BUG** (conflates Handled with "error is fine")

## Out of scope

- `keep:true` integrity check (`GoalCall.Name` validation). Once errors
  propagate correctly (fix #1), a bad Name causes a 404 on the very next
  rebuild. No additional validation layer needed — the runtime is enforcing
  "goal exists" already. Drop the earlier #5 suggestion.
- Any further builder/prompt cleanup. One fix at a time.
- The `tests/simple/.build/traces/` and `os/.build/traces/` dirty files in
  the working tree — unrelated to this bug, leave alone.

## Order of execution

1. Write both C# test files — they should FAIL against current code, proving
   the bug exists.
2. Apply fix #1 (foreach one-liner). Re-run tests — should pass.
3. Apply fix #2 (formal filter + FormatValue scalar-or-JSON).
4. Fix the poisoned applystep.pr manually (fix #3).
5. Full rebuild from `/workspace/plang/system`. Verify ApplyBuiltStep step 0
   now has 2 actions and builder self-rebuild is idempotent.
6. Write v6/summary.md, commit (including .bot/), generate changes.patch,
   push.

## Risks

- Fix #1 (foreach error propagation) might surface existing "silently passing"
  tests/builds that were relying on the bug. Run the full test suite
  (`dotnet run --project PLang.Tests`) and `plang build` from `/workspace/plang`
  before and after; compare. Any test that breaks probably had a real bug
  masked — investigate, don't paper over.
- Fix #2's JSON fallback means some existing prompts that rendered
  POCOs-via-ToString will switch to JSON. Acceptable — JSON is richer and
  more useful for the LLM.
- Manual .pr edit (fix #3) is a one-shot migration. After rebuild, the
  correct value sticks.
