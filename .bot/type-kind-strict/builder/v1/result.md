# Builder v1 — coder handoff: build-time error-handling failures

**From:** builder
**To:** coder
**Branch:** type-kind-strict @ fd7ee4812
**Severity:** F1 (error.throw contract) — real bug; F2 (error message quality) — quality, high value
**Type-kind-strict status:** the type work itself PASSES review. These two findings are
**pre-existing** failures surfaced while building the builder; they are NOT regressions
from this branch.

---

## How this was found

Ran `plang build` from `/workspace/plang/os/system` (builds the system tree, including the
builder). The build of **`os/system/Run.goal`** failed. `Run.goal` is:

```
Run
- run before app start event
- call goal %goalName% %parameters%
- run end app event
```

The planner/compile failed on step 0 (see F3 note below), the error propagated to the
build's error handler `HandleBuildFailure` (`os/system/builder/BuildGoal/Start.goal`), and
the **error handler itself then threw a second, more confusing error** — masking the
original failure.

---

## F1 — `error.throw` must accept an Error object in `Message`, not coerce to string

### What happened (PLang level)
`HandleBuildFailure`'s last step is:

```
- throw %!error%
```

`%!error%` is the **whole Error object** produced by the upstream failure:

```
Error {
  Key:        "BuilderPlannerFailed",
  Message:    "The LLM couldn't produce a usable plan for this goal …",
  StatusCode: 500
}
```

`error.throw`'s `Message` parameter is typed as a **string** (`data.@this<string>`). So the
runtime tried to bind an **Error object** into a **string slot**, the conversion failed, and
instead of re-raising the original `BuilderPlannerFailed`, the developer saw an
`InvalidCastException`-derived message about converting an object to String.

### How the data looked vs. how it should have looked
- **Looked:** `%!error%` = `Error` object (Key/Message/StatusCode).
- **Slot expected:** a `string`.
- **Should be:** `error.throw` accepts the `Error` object directly and **re-raises it as-is**,
  preserving Key, Message, StatusCode, and the error chain. Re-throwing an existing error is a
  first-class, intended pattern — `- throw %!error%` is **valid by design** and must stay valid.

### Where it threw (PLang)
- Goal: `os/system/builder/BuildGoal/Start.goal`
- Step: `- throw %!error%` (the `HandleBuildFailure` sub-goal, last step)
- Action: `error.throw`, binding the `Message` parameter.

### Ask for coder
- `error.throw` should accept a non-string `Message` value:
  - If the value **is** an `Error` (`app.error.*`), re-throw it as-is (preserve
    Key/Message/StatusCode/chain) — do not stringify.
  - The goal `- throw %!error%` must build and run without a conversion error.
- This is the runtime/engine layer (assume the C# engine contract is what's wrong here, not the
  goal). The goal is correct.

### Crash detail (for digging only — not the headline)
For reference, the underlying throw was `System.Convert.ChangeType(errorObject, typeof(string))`
in the conversion path (`PLang/app/type/list/Conversion.cs`, the `IsPrimitive` branch — string is
treated as primitive, `Convert.ChangeType` to String requires `IConvertible`, the Error object
isn't `IConvertible`). But the **fix is the `error.throw` contract**, not patching ChangeType —
see F2 for the conversion-message side.

---

## F2 — conversion-failure error messages must name target type, variable, and content

### The problem
The message a developer actually saw was effectively:

```
Cannot convert '[BuilderPlannerFailed] The LLM couldn't …' (ActionError) to String:
Object must implement IConvertible.
```

Two faults:
1. It **leaks the C# internal** (`Object must implement IConvertible`) — meaningless to a PLang
   developer.
2. It **omits the parameter name** — the single most useful fact ("which slot in my step
   failed?"). The conversion layer doesn't currently receive the parameter/variable name, so it
   can't name it.

### What we want instead (plain-language, three facts)
Every parameter-binding/conversion failure message must state:
1. **the target type** (what we tried to convert *to*),
2. **the variable / parameter name** (where in the goal to look),
3. **the actual content and/or its type** (what we tried to convert *from*).

Template (illustrative — adapt to the codebase's voice):

> Could not bind `%!error%` to parameter `Message`: expected **text** (string) but the value is
> an **Error** object (`BuilderPlannerFailed: The LLM couldn't produce a usable plan…`).

or, for a primitive miss:

> Could not bind `%count%` to parameter `Times`: expected **number (int)** but the value
> `"abc"` is text that isn't a number.

Keep the full stack trace available for digging, but **lead with type + variable + content**;
do not surface the raw C# exception text as the headline.

### Implementation note for coder
- The conversion site (`TryConvertTo`) currently has only `value` + `targetType`; it does **not**
  know the **parameter name**. That name needs to be threaded in from the binding layer (the
  action-parameter resolution path) so the message can name the slot. Without that, the message
  can name type+content but not "where" — and "where" is the most useful of the three.
- Also: an object that is not `IConvertible` targeting a `string` slot currently produces an
  `InvalidCastException` inside `Convert.ChangeType`. Whatever the chosen semantic (clean typed
  error vs. `.ToString()` fallback), it must never surface as a raw `InvalidCastException`.

---

## F3 — (builder-side observation, NOT a coder action / NOT a goal edit)

The original step-0 failure on `Run.goal` was: `- run before app start event` mapped (correctly)
to `event.on`, but `event.on` requires `GoalToCall`, and the step text names no goal to call, so
the step compiled to zero actions (`Step[0]: no actions`).

- The `event.on` **teaching files are fine** (`os/system/modules/event/on.{description,examples}.md`
  clearly teach "before/after/on X → event.on with a GoalToCall"). **Do not change them for this.**
- The mismatch is intent: `run before app start event` reads as "**raise/fire** the BeforeApp
  lifecycle event," but the only event action is `event.on` ("**register** a handler"). There may
  be a genuinely **missing action** (fire/emit a lifecycle event) that `Run.goal` needs — or
  `Run.goal`'s step is under-specified.
- **This is flagged for the team to decide**, not for the builder to fix via teaching files and
  **not** a `.goal` edit by any bot without direction. `Run.goal` was read-only here.

---

## Summary for coder
- **F1 (do):** make `error.throw` accept an `Error` object in `Message` and re-throw as-is.
  `- throw %!error%` must be valid.
- **F2 (do):** rewrite conversion-failure messages to lead with target type + parameter/variable
  name + actual content; thread the parameter name into the conversion layer; never surface raw
  `InvalidCastException`.
- **F3 (decide, not now):** `Run.goal`'s `run before app start event` likely needs a
  fire-lifecycle-event action that doesn't exist — team decision, not a teaching-file or goal edit.
