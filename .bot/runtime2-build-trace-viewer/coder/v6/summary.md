# v6 — Silent-error swallowing + formal rendering fix

## What this is

Two root-cause bugs that v5 identified as symptoms and worked around. Both
surfaced during the ApplyStep.goal self-rebuild investigation. Together they
explain why the builder couldn't self-rebuild and why earlier debugging
chased symptoms for weeks.

1. **`loop.foreach` was swallowing errors when the body action returned
   `Handled=true` with `Success=false`.** The `Handled` flag's real purpose
   is "I consumed remaining step siblings" (control-flow for `Step.RunAsync`).
   But `loop/foreach.cs:50` was reading it as "error is fine, keep looping",
   conflating two orthogonal concepts. Any foreach whose body went through
   `condition.if` orchestration — which correctly stamps `Handled=true` —
   would silently ignore inner errors.

2. **The `formal` filter in the Fluid provider emitted `Fluid.Values.ObjectDictionaryFluidIndexable\`1[System.Object]`
   in place of dict values.** `FluidValue.Create(dict)` wraps a .NET dictionary
   in a `DictionaryValue`; `ToObjectValue()` returns the wrapper, not the raw
   dict. The filter checked `v is IDictionary or IList` (wrapper is neither),
   fell through to `v.ToString()`, and the Fluid class-name leaked into the
   prompt's `@known:` section. The LLM faithfully echoed that string back as
   a goal name on save, so `goal.call` ended up with `Name = "Fluid.Values..."`.

The silent-swallowing bug is why nobody noticed (1) before — the
`GoalNotFound` 404 that the bad `goal.call` produced was suppressed by
`loop.foreach`, so rebuilds looked like they succeeded. Without this
diagnostic silence, the Fluid leak would have screamed on the very first
rebuild.

## What was done

**Primary fix — `loop/foreach.cs:50`**

```csharp
- if (!result.Success && !result.Handled) return result;
+ if (!result.Success) return result;
```

Errors always propagate regardless of `Handled`. Full audit of all 7
`Handled` sites in the codebase confirmed that this was the only misuse:
`condition.if`, `loop.foreach`, and event bindings correctly set
`Handled=true` to tell `Step.RunAsync` "don't re-iterate siblings"; all
other read sites use it for control flow, not error suppression.

**Secondary fix — scalar-or-JSON in both formal renderers**

- `PLang/App/modules/ui/providers/FluidProvider.cs` — `FormatFormalValue`
  now calls `UnwrapFluid` first (recursively walks `IFluidIndexable` and
  enumerable values back to plain dicts/lists), then scalar-or-JSON.
- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` —
  `FormatValue` uses the same scalar-or-JSON rule; no unwrap needed (it
  sees raw CLR objects, not Fluid wrappers).

The shared rule:
```
null                  → "null"
string starts with %  → bare
string with space/,   → "quoted"
string                → bare
bool                  → true/false
IConvertible          → ToString()
anything else         → JsonSerializer.Serialize
```

**Tests — regression lock**

- `PLang.Tests/App/Modules/loop/ForeachErrorPropagationTests.cs` (3 tests)
  - `Foreach_BodyGoalCallFails_PropagatesError` — direct error, sanity.
  - `Foreach_BodyInnerGoalFailsInsideConditionIf_PropagatesError` — the
    exact regression: inner goal uses `condition.if` + bad `goal.call`,
    orchestration returns `{Success=false, Handled=true}`, outer foreach
    must propagate. **This test fails before the fix and passes after.**
  - `Foreach_BodySucceeds_CompletesAllIterations` — happy path.
- `PLang.Tests/App/Modules/condition/IfErrorOrchestrationTests.cs` (2 tests)
  - `If_OrchestratedBranchAction_ReturnsError_PropagatesThroughStep` — pins
    that the step-level error propagation works (it does; bug is downstream).
  - `If_OrchestratedSuccess_MarksResultHandled` — pins the `Handled=true`
    contract so future refactors don't drop it and break `Step.RunAsync`.

**Data fix — `system/builder/.build/applystep.pr`**

Two `goal.call` parameters had `Name = "Fluid.Values..."` stuck from prior
poisoned rebuilds. Hand-edited to the correct names. After the code fixes,
re-running the build produces clean `@known:` and clean output.

## Verification

Proof that the fix surfaces previously-silent failures — I ran the builder
on `/builder/ApplyStep.goal` twice with the same `.pr` starting state:

**Pre-fix (code reverted via `git stash`, same `.pr`):**
```
Building goal: ApplyStep
  Building sub-goal: ApplyKeptStep
  Kept prior mapping for step 0
  Building sub-goal: ApplyBuiltStep
  Kept prior mapping for step 1
  Building sub-goal: HandleValidationError
  Saved ApplyStep (18.9s)     ← "success" despite broken internals
```

**Post-fix (same .pr, fixes restored):**
```
Building goal: ApplyStep
  Building sub-goal: ApplyKeptStep
  Kept prior mapping for step 0
  Building sub-goal: ApplyBuiltStep
  Building sub-goal: HandleValidationError
🔴  ValidationErrors(400)      ← real error now surfaces
Reason: Step[0]: has keep:true but also emitted actions...
```

The ValidationErrors that surfaced is a separate, older LLM-drift bug — the
LLM is emitting `keep:true` AND `actions:[...]` in the same step response,
which `validateResponse` correctly rejects. It was being silently swallowed
before. This is not fixed in v6; it's next session's work.

Test suite: 2282/2284 pass. The two failures (`Query_ToolCall_LlmRequestsToolAndHandlesError`,
`Name_HashSetOfString_ReturnsHashsetWithoutBacktick`) pre-date the fix —
verified by reverting the code and re-running.

## Code example — the anchor pattern

The cleaner path now is symmetric between condition.if and loop.foreach:
both mark `Handled=true` to tell their parent `Step.RunAsync` "I consumed
the siblings". Neither writes `Handled` to mean "ignore errors".

```csharp
// condition/if.cs:53-55 (unchanged — this was always correct)
var result = await Orchestrate(actions, conditionResult);
result.Handled = true;   // ← "I consumed siblings", NOT "success"
return result;

// loop/foreach.cs:50 (fixed)
if (!result.Success) return result;   // ← errors always propagate
```

And the formal rendering — one rule, applied everywhere:

```csharp
// Both FluidProvider.FormatFormalValue and DefaultBuilderProvider.FormatValue
if (v is IConvertible) return v.ToString() ?? "";
try { return System.Text.Json.JsonSerializer.Serialize(v); }
catch { return v.ToString() ?? ""; }
```

No special cases for IDictionary/IList; no `v.ToString()` fallback that leaks
class names. Complex values become JSON so the LLM sees real structure.

## Out of scope / next

- The LLM-drift issue (`keep:true + actions`) that now surfaces loudly —
  builder prompt needs a clearer rule or the LLM response schema needs to
  make the two mutually exclusive. Separate investigation; v7 territory.
- Module-scoped `--debug` instrumentation idea from v5 — not touched.
- Any prompt/template cleanup beyond the `formal` filter fix.

## What changed (paths)

- `PLang/App/modules/loop/foreach.cs` — one-line error-propagation fix
- `PLang/App/modules/ui/providers/FluidProvider.cs` — `FormatFormalValue` + `UnwrapFluid` helper
- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` — `FormatValue` simplified
- `PLang.Tests/App/Modules/loop/ForeachErrorPropagationTests.cs` — new, 3 tests
- `PLang.Tests/App/Modules/condition/IfErrorOrchestrationTests.cs` — new, 2 tests
- `system/builder/.build/applystep.pr` — hand-edited + subsequently rebuilt cleanly
- `system/.build/app.pr` — timestamp update from rebuild
