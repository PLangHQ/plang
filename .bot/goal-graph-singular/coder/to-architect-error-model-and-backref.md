# To architect — the error model, and the back-ref problem we walked into

**Branch:** goal-graph-singular
**From:** coder (session with Ingi, 2026-09)
**Status:** design settled with Ingi on the error model; **blocked** on a direct conflict with your
back-ref ruling. No error-model code written. Tree builds green.

---

## 1. Where we started

Stage D — the **Validate trilogy** you released (`goal.Validate → Step.Validate → action.list.Validate
→ action.Validate`). Your Q1 said not to build it against registry strings, so module-owns-action came
first. That landed in full:

- Stage A — `ActionName → Name`, qualified form composed from both objects (Ingi overruled the doc's
  `ToString()` = `{module}.{name}`)
- Stage B — `action.Module` is the element; `.pr` reader resolves it; legacy module-name repair deleted
- **module owns its actions** — registry index dissolved, `Mint()` gone, late stamp removed
- Stage C — `action.Requirement` (was `Requires`), `action.BuildError` → since redesigned, `action.Return`

Then `action.Validate` needed somewhere to put its verdicts — and that opened the error model.

---

## 2. What Ingi settled (design, not yet implemented)

| Decision | |
|---|---|
| `ErrorChain` → **`list`** | it was a public naked `List<IError>` **and** a container word. `list` is the existing convention (`module.list.@this.list`). Means **caused-by**, confirmed: *"'file.read is not valid' is caused by those missing-parameter errors"* |
| **`Error.Action`** | `Error` already holds `Step`/`Goal`; `Action` completes it. Also the correct form of the `ActionError.ActionModule`/`ActionName` strings deleted earlier (reference, not flat copy) |
| **`Validate` returns `IError?`** | one error, causes in its `list`: `new ActionError($"{Module}.{Name} is not valid") { Action = this, list = causes }`. **No error state stored on the node** |
| **`app.Error` deleted** | *"context always exists and only time error happens is on some specific context"* |
| **the run-wide `error.trail` deleted** | see finding 2 below — it is a stored-twice, not merely dead |
| **`%!error%` ← `CallStack.Error`** | Ingi's rule: *"where we had the last error"* — walk `Caller` from `Current` to the nearest unhandled frame with errors |
| wire key `errorChain` → `list` | approved |
| `RunRecovery` → `action.list.Run` | approved, see finding 9 |
| ignored errors stay swallowed | deferred deliberately; `on error ignore` currently drops the error with no log — the empty try/catch, present today |

**Rejected:** `error.list : IError` (a list pretending to be an error, to fit `Data.Error` being
singular) · `error.scope` (*"scope is behaviour, shouldn't be visible to the app in obp design"*) ·
`Fail` for the Data factory (hard no) · `context.Recovering(...)` (hard no) · `ReadContext` as the
carrier for a step (ambient — see §4).

---

## 3. What we found

1. **The current error is stored FOUR times.** Three collection types —
   `callstack.call.error` (per frame, `Call.Errors`), `callstack.audit` (run-wide, **read from plang as
   `%!callStack.Audit%`**), `error.trail` (run-wide, **zero readers**) — plus the `AsyncLocal` slot that
   `error.handle` pushes. On one failure the same error is written to the frame, the audit, and the slot.
   **The slot is the fourth home of the same fact**, which is why no name for it felt right.
2. **`error.trail` is the duplicate**, not just dead code. Its own doc admits it: *"Distinct from
   app.callstack.audit.@this which records errors observed at Call frames."* `callstack.audit` is the
   real one (3 production writers, read from plang).
3. **`CallStack` is already `AsyncLocal` and "fork-safe by construction"** — so the frame walk is
   parallel-safe for free. Flow-locality was the only thing the slot's own AsyncLocal bought.
4. **`action.Step` has no load-path writer.** The only production assignment in the codebase is the
   stamp in `error/handle.cs:178`. The condemned wiring loop sets `step.Goal`, never `action.Step`.
5. **But it is read** — `GoalCall.cs:182,207` (`Action?.Step?.Goal`, to resolve sibling goal paths) and
   the source generator (`IStep.cs`: *"the source generator wires Step = action.Step in ExecuteAsync"*).
6. **`context.Step` is not a safe substitute.** Ingi: it is *"whatever step is executing right now"*,
   only accidentally the action's own — wrong for anything created now and run later (events, callbacks,
   resumed snapshots). I proposed it; he was right to reject it.
7. **Actions cannot be born with their step today**: the step reader reads its actions **first** and
   constructs the step **last** (`return new step.@this { … Action = actions … }`).
8. **Recovery actions are never read by the step reader.** They are materialised lazily at run time
   (`row.Value<Action>()` in `RunRecovery`), so no read-time channel reaches them.
9. **One action reader, two callers.** Its own comment: *"the one reader for the .pr wire, value→slot
   materialization, and a nested modifier chain."* The step reader calls it **concretely** (could pass a
   step); `row.Value<Action>()` calls it **through the `ITypeReader` registry** (cannot). So a step
   parameter would make the same reader produce actions with a step or without, depending on caller.
10. **The root inconsistency — an action's nested actions are modelled two ways:**
    ```csharp
    // action/serializer/Reader.cs
    case "modifier": → read as REAL actions (modifier subtype), at load
    case "child":    → read as a REAL step.list,               at load
    case "parameter":→ Data rows … and error.handle's recovery chain lives HERE
    ```
    `on error call X` is structurally the same thing as `child` — a nested body of actions belonging to
    this action — but it is stored as a **parameter value**. That is why it stays `Data` until run time,
    why it has no step, and why the stamp exists at all.

---

## 4. What we walked into — the conflict

Ingi's position, arrived at independently and (I think) correctly:

> *"I feel like it should be set when we create the action instance… if we keep the stamp, we will
> forget to do this properly"* and *"having context.Step to get a step of an action just introduces
> danger"*

Your target picture says:

```csharp
// action — TARGET (after back-ref pass)
// DELETED: public Step? Step
```

**These are incompatible**, and the surrounding facts make neither side free:

- **It cannot simply be deleted** — two readers depend on it (finding 5), one of them generator-emitted.
- **It cannot be sourced from ambient state** — `context.Step` is unsafe (finding 6).
- **It cannot be born consistently** — the step is constructed last (finding 7), and the two reader
  entry points differ in what they can pass (finding 9).
- **A `ReadContext` field was rejected** and I agree with the rejection: it reintroduces the ambient
  problem one layer down, makes the step optional for every reader, and broadcasts instead of handing
  off. I originally proposed it only to avoid touching `ITypeReader` — a bad trade, and unnecessary
  anyway since the step reader holds a *concrete* reader field.

His proposed shape, which I think is right as far as it goes:

```csharp
var step = new step.@this();                       // exists first  (needs init → internal set)
…
actions.Add((action.@this)_action.Read(ref reader, kind, ctx, step));   // explicit hand-off
…
new action.@this { Step = ctx.Step }               // birth fact, not a stamp
```

It fixes every action read as part of a step — nearly all of them. It does **not** reach the lazily
materialised ones (finding 8), where the only non-ambient source is the owning action handing down its
own step — still a stamp, though an ownership-based one.

---

## 5. Questions for you

1. **Does `action.Step` survive as a birth fact, or die?** If it dies, where do `GoalCall` and the
   generator's `IStep` wiring get the step, given ambient `context.Step` is unsafe? (The call **frame**
   is the one candidate we saw: it is created per execution and knows which action it runs — "which step
   am I in" is arguably a run fact belonging to the frame, not to the program node.)
2. **Should the recovery chain be a structural slot** (like `child`/`modifier`) rather than a parameter
   value (finding 10)? That single change would make recovery actions read at load, born with their
   step, remove the lazy materialisation, remove the stamp, and remove the two-entry-point asymmetry.
   It is a wire-shape change to `error.handle`, so it is yours.
3. **`%!error%` via the frame walk** — confirm the rule: nearest frame outward from `Current` where
   `!Handled && Errors.Count > 0`, newest error on it. Does `Handled` correctly mean "stop being
   `%!error%`"? Does the walk reproduce today's nested-handler LIFO behaviour?
4. **Should `Call.Errors` (`callstack.call.error.@this`) and `Error.list` be the same type?** Three
   `IReadOnlyList<IError>` types exist; two survive our deletion.
5. Confirm `CallStack.Error` as the member name (Ingi approved; no collision).

---

## 6. Tree state

- Last code commit `340eb58cf`; everything after is docs (`a9ce795ac`, `9d22d1d32`).
- **17 files uncommitted, builds green.** Contains work that our decisions now supersede: the
  `error/scope` rename (name rejected), `action.Error` (to be deleted), the harvest loop in `Default.cs`
  (to be replaced). Keep: `ErrorChain → list`, `Error.Action`, `Requires → Requirement`.
- Full decision record with quotes: `error-model-decisions.md` (same folder), including the addendum on
  the four storage locations.

---

## 7. The code, as agreed with Ingi (reviewed line by line in console, not yet written)

### 7.1 The walk — this is what replaces the slot

```csharp
// app/callstack/this.cs   (NEW)
/// <summary>The error in play — the newest error on the nearest frame that failed and was not
/// recovered, walking Caller outward from Current. This is what %!error% resolves to.
/// Flow-local for free: Current is AsyncLocal, so parallel branches each walk their own chain.</summary>
public IError? Error
{
    get
    {
        for (var frame = Current; frame != null; frame = frame.Caller)
            if (!frame.Handled && frame.Errors.Count > 0) return frame.Errors[^1];
        return null;
    }
}
```

`!frame.Handled` is what makes a recovered error stop being `%!error%`, and what skips the recovery
action's own (successful) frame. **Question 3 in §5 is asking you to confirm exactly this rule.**

### 7.2 `%!error%` reads it

```csharp
// actor/context/this.cs:192
- vars.Set(new data.DynamicData("!error", () => App.Error.Error, this));
+ vars.Set(new data.DynamicData("!error", () => CallStack.Error, this));
```

### 7.3 `error.handle` stops setting anything, and stops re-implementing the chain

```csharp
// 3 call sites
- var recoveryResult = await RunRecoveryWithErrorScope(actions!, context, result.Error!);
+ var recoveryResult = await RunRecovery(actions!, context);

// the wrapper is deleted outright (also a verb+noun triple)
- private static async Task<data.@this> RunRecoveryWithErrorScope(…)
-     using (context.App.Error.Push(caughtError, context)) { return await RunRecovery(actions, context); }

// and RunRecovery stops hand-rolling the loop — the node runs itself.
// Verified equivalent: action.list.Run breaks on result.ShouldExit(), and
// ShouldExit() is `if (!d.Success && !d.Handled) return true;` — i.e. it already
// stops on failure exactly like RunRecovery's `if (!last.Success) return last;`.
// It also GAINS condition support (if/elseif/else with Child), which the hand-loop lacks.
private static async Task<data.@this> RunRecovery(list actions, context ctx)
{
    var chain = new action.list.@this(actions);      // existing adopt-ctor
    foreach (var a in chain)
        if (a.Step == null) a.Step = ctx.Step;       // ← ONLY IF the back-ref survives (§4/§5 Q1)
    return await chain.Run(ctx);
}
```

If `action.Step` dies, or the recovery chain becomes a structural slot (§5 Q2), the stamp loop goes and
this is one line: `return await new action.list.@this(actions).Run(ctx);`

### 7.4 Deletions

- `app/error/scope/` — the fused class, `Push`, `Restorer`
- `app.Error` property + construction + `this.Snapshot.cs:65` restore — **31 sites**
- `app/error/list/this.Snapshot.cs`; and `IsFrozen` / `LoadAndFreeze` / `Restore` /
  `[PlangType("trace")]` off `error/list/this.cs`, leaving a plain `IReadOnlyList<IError>` + `Add`
- audit tests: `ErrorsTrailSnapshotTests`, `ErrorsScopeTests`, the `Trail` asserts in `OtherAccessorsTests`

### 7.5 Validate, and the builder

```csharp
// action/this.Schema.cs
public IError? Validate(context ctx)
{
    var causes = new error.list.@this();
    if (_module?[Name] is not { } element)
        causes.Add(new ActionError($"action '{Name}' not found in module '{Module}'"));
    else {
        foreach (var row in element.Property.Rows)
            if (!row.Nullable && row.Default == null && !emitted.Contains(row.Name))
                causes.Add(new ActionError($"required parameter '{row.Name}' is missing"));
        if (HandlerComplaint is { } c) causes.Add(c);
    }
    return causes.Count == 0 ? null
         : new ActionError($"{Module}.{Name} is not valid") { Action = this, list = causes };
}

// action/this.cs — delete the property I had added
- public error.list.@this Error { get; init; } = new();

// build/code/Default.cs — replaces `validationErrors` (List<string>) and the harvest loop
var error = action.Validate(ctx);
if (error != null) return context.Error(error);
```

`list` becomes `{ get; init; }` so the object initializer works — Ingi: *"why `foreach … list.Add(c)`?
can't it be `{ Action = this, list = causes }`?"*

### 7.6 Ingi's step hand-off (blocked on §5 Q1)

```csharp
// goal/step/serializer/Reader.cs — step exists FIRST (needs init → internal set on read-time fields)
var step = new step.@this();
var actions = new action.list.@this();
step.Action = actions;
…
case "action": case "actions":
    reader.BeginArray();
    while (reader.NextElement())
        actions.Add((action.@this)_action.Read(ref reader, kind, ctx, step));   // explicit hand-off
    reader.EndArray();                                                          // NOT via ReadContext
    break;

// action/serializer/Reader.cs
var action = new action.@this { Step = step };   // birth fact, not a stamp
```

`_action` is a **concrete** field on the step reader, so this needs no change to `ITypeReader`.
Cost: the step's read-time fields go `init` → `internal set` (immutable outside the assembly, mutable
during the read). Reaches every action read as part of a step; does **not** reach lazily materialised
ones (finding 8).

---

## 8. Scope note

This document covers the error model and the back-ref conflict only. The rest of the session's work and
findings — builder prompt fixes (landed, `4deaa8921`), the builder never self-building, the stale `.pr`
hashes, `Property.Rows` as a middleman, suite discovery flakiness, `DiscoverActionTests` 7/10 red — are
in `open-items.md` in this folder. The full decision record with quotes is `error-model-decisions.md`.
