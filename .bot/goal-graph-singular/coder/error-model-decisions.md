# Error model — decisions from the 2026-09 design session (Ingi + coder)

**Status: DESIGN SETTLED EXCEPT ONE BLOCKER. No green light to implement.**
Written so the context isn't lost. Nothing here is committed as code except where noted.

---

## 1. What we settled

### Naming / shape

| Decision | Detail |
|---|---|
| **`error.trail` dies** | "I don't want any error.trail, it's a bad design." A domain word for a container — the named smell. |
| **`error.scope` rejected** | My rename. Ingi: *"scope is behaviour, and shouldn't be visible to the app in obp design."* |
| **`ErrorChain` → `list`** | `ErrorChain` was obpv twice: a public naked `List<IError>` **and** a container word. Ingi asked *"why don't we call it `action.Error.list`? why are we calling it something different?"* — `list` wins over `.Child`; it's the existing convention (`module.list.@this.list`). |
| **`list` means CAUSED-BY** | Not merely "contains". Confirmed against the real case: *"'action file.read is not valid', that is caused by those required missing errors, so it is caused-by them."* |
| **`Requires` → `Requirement`** | Plural bothered him; singular noun matches `Warning` / `Parameter` / `Modifier`. Already applied (uncommitted). |

### Ownership / flow

| Decision | Detail |
|---|---|
| **`Validate` returns `IError?`** | Not `void`, not a collection. Null when the action is fine. |
| **No error state stored on the action** | `action.Error` (which I had added) is **deleted**. Ownership inverts: the **error** holds a reference to the action. |
| **`Error.Action` added** | `Error` already holds `Step` and `Goal`; `Action` completes the chain. This is also the *correct* version of the `ActionError.ActionModule` / `ActionName` strings deleted earlier this session — a reference, not a flat copy. |
| **One error, causes underneath** | `Validate` gathers N problems, returns ONE error whose `list` holds them:<br>`new ActionError($"{Module}.{Name} is not valid") { Action = this, list = causes }` |
| **`list` is `{ get; init; }`** | So the object initializer above works — Ingi: *"why `foreach … error.list.Add(c)`? can't it be `{ Action = this, list = causes }`?"* Yes. |
| **`validationErrors` (List&lt;string&gt;) dies** | *"those are just errors, it doesn't matter what type they are (a validation error)… I think he just wants errors."* |
| **The harvest loop dies** | `foreach (var verdict in a.Error) validationErrors.Add(...)` is obpv — the object owns its responsibility; outside code doesn't collect on its behalf. |
| **No middle steps** | *"I don't like doing middle steps, it is better to clean things up, when we do middle step it confuses us later."* |

### Rejected along the way

- **`error.list : IError`** (a list pretending to be one error). This was me bending a type so `context.Error(actions.Error)` would compile — `Data.Error` is a single `IError`. Ingi caught it: *"why does the list need to be IError?"* Correct call; it's gone.
- **`action.Error` as a list.** Superseded by "Validate returns one error with causes".

### `app.Error` and the audit — both go

- **No `app.Error`.** *"I don't think there should be any app.error actually, because context always exists and only time error happens is on some specific context."*
- **The run-wide audit (`Trail`) is deleted.** Evidence gathered:
  - **Zero** production readers of `Trail` / `Error.list` / `Error.Count` — only tests and snapshot capture/restore.
  - Exactly **one** production writer: `error/handle.cs:153`, `context.App.Error.Push(caught, context)` — which already takes a context.
  - Its own doc: *"unbounded for the App's lifetime — long-running processes accumulate linearly."* The leak Ingi predicted.
  - **It doesn't capture the case he cared about**: `Push` runs only when there is a *recovery goal*. `on error ignore` never pushes — so the audit records handled errors and misses swallowed ones.

### Ignored errors — deliberately NOT changed now

`error/handle.cs`: `if (await IgnoreError.ToBooleanAsync()) return context.Ok();` — the error is dropped
on the floor. No push, no log. This is the empty try/catch, and it exists today.

Emitting swallowed errors on a redirectable channel was discussed (no retention, no leak, never
silent). Ingi: *"lets not do the channel write now, this was me thinking outloud… ignored errors are
swallowed for now."* → recorded as an open item, not part of this work.

---

## 2. THE BLOCKER — must be answered before implementing

**`context.Error` cannot be a property.** The context already has the failed-`Data` factory that
every handler uses:

```csharp
public data.@this    Error(IError error)  => new("", context: this) { Error = error };
public data.@this<T> Error<T>(IError error) …
```

C# forbids a property `Error` and methods `Error(...)` on the same type. So the settled shape
"`context.Error` = the current error (`%!error%`)" **cannot be spelled that way.**

Options, none chosen:
- **(a)** the current error takes a different name on context (then `%!error%` and the C# name diverge)
- **(b)** rename the factory (used by essentially every handler — wide but mechanical)
- **(c)** `%!error%` resolves without a C# property at all (it is registered as a `DynamicData`
  variable in `actor/context/this.cs:192`, so this may be viable)

Related, still unanswered: **where does the recovery scope live?** Today `App.Error.Push(caught, ctx)`
returns an `IDisposable`; its only caller is `error.handle`. If `app.Error` goes, the AsyncLocal
current-error + its scoping needs a home and a name. My suggestion `context.Recovering(caughtError)`
was never approved.

---

## 3. Other issues found while writing the plan out (all real, none fixed)

1. **`Action` is not a global alias** — it's file-local (`using Action = app.goal.step.action.@this;`).
   `Error.cs` aliases only `Goal` and `Call`. The `Error.Action` / `IError.Action` additions need a
   using or full qualification, or they will not compile.
2. **`error.list` is still the audit type** — it carries `IsFrozen`, `LoadAndFreeze`, `Restore`,
   `[PlangType("trace")]` and a snapshot partial. `IError.list` now points at it, so every error
   would inherit trail semantics. It must be reduced to a plain collection **first**.
3. **The wire key did not move.** `Error.Write` still emits `writer.Name("errorChain")`. Renaming the
   C# member changed nothing on the wire — keep for compatibility, or rename to `list`? Undecided.
4. **Stale test names** — `ErrorChain_IsEmptyByDefault`, `Format_IncludesErrorChain`,
   `ServiceErrorChainTests` now assert `.list`.

---

## 4. Implementation order (when green-lit)

1. Strip `error.list` to a plain collection (drop freeze / restore / snapshot / PlangType). **First** — issue 2 above.
2. Fix the `Action` alias in `IError.cs` and `Error.cs`.
3. Decide the wire key (issue 3).
4. `action.Validate(ctx) → IError?`; delete the `action.Error` I added.
5. `action.list.Validate(ctx) → IError?` (the rung beside `Run`/`Output`); `Default.cs` collapses to:
   ```csharp
   var error = action.Validate(ctx);
   if (error != null) return context.Error(error);
   ```
6. Delete `app.Error`, `error/scope/`, the audit and its tests — **only after the blocker is resolved.**

---

## 5. Current uncommitted state (builds green as of the folder swap; NOT since the IError edits)

- `error/trail` → `error/list`, old `error/list` → `error/scope` ← **contains the rejected `scope` name**
- `action.Error` + `Validate()` recording on it ← **to be deleted per the decisions above**
- `Default.cs` calling `Validate()` + the harvest loop ← **to be replaced**
- `Requires` → `Requirement` ← keep
- `IError`/`Error`: `ErrorChain` → `list`, `Action` added ← keep, but see issues 1 and 2
- Last pushed commit: `340eb58cf`. Nothing above is committed.

---

# ADDENDUM — the current error is stored FOUR times (2026-09, later in the same session)

Ingi, from memory: *"why isn't the error on the current call on the stack? then we reach for the
error there?"* — chasing that changed the shape of this work. **It already is on the call.**

## What is actually there

Three separate collection types, all `IReadOnlyList<IError>`:

| Type | Scope | Status |
|---|---|---|
| `app.callstack.call.error.@this` | **per Call frame** (`Call.Errors`) | live — populated by `App.Run` on failure |
| `app.callstack.audit.@this` | run-wide | live — written in 3 production sites, **read from plang as `%!callStack.Audit%`** |
| `app.error.trail.@this` | run-wide | **dead** — zero readers; the one we already decided to delete |

On a single failure the same error is written to all of them, plus the AsyncLocal slot:

```csharp
// callstack/call/this.cs:221 — "this.Errors.Add and CallStack.Audit.Add on failure"
this.Errors.Add(err);          // 1. the frame it happened in
_stack.Audit.Add(err);         // 2. the run-wide accumulator
…
Push(caughtError, context);    // 3. the AsyncLocal slot, from error.handle
```

**So the slot we spent this session trying to name is the fourth home of the same fact.** That is
why every candidate name felt wrong — the thing shouldn't exist.

It also gives a better reason for a decision we had already made on weaker grounds: of the two
run-wide accumulators, `CallStack.Audit` is the real one and `app.Error.Trail` is the duplicate.
The trail's own doc admits it: *"Distinct from app.callstack.audit.@this which records errors
observed at Call frames."* Deleting it is removing a **stored-twice**, not just dead code.

## The rule Ingi gave

> "which frame the `%!error%` should read is where we had the last error"

So: from `CallStack.Current`, walk the caller chain outward to the nearest frame whose `Errors` is
non-empty; `%!error%` is that frame's latest error.

**Verified this is safe:** `app.callstack.@this` holds `private readonly AsyncLocal<call.@this?> _current`
and documents itself as *"fork-safe by construction"*, instance-level. Flow-locality — the only thing
the error slot's own AsyncLocal was buying — the callstack already has. Parallel `Task.WhenAll`
branches each walk their own chain. `Current.Caller` navigation already exists and is used elsewhere
(`goal/this.cs` relies on it).

## What this collapses

- **The slot disappears.** No AsyncLocal on context, no `Push`, no `Restorer`, no save/restore, and
  **no name to choose** — which was the last open blocker of the session.
- `%!error%` becomes a read over state that is already correct and already maintained.
- `error.handle` stops needing any way to "set the current error" — it just runs the recovery; the
  failed frame is already recorded.
- The three collection types should collapse toward one (`error.list`), used by `Call.Errors` and
  `Error.list`. `CallStack.Audit` stays as the run-wide accumulator (it is read from plang).

## Still open / to verify before implementing

1. **Nearest-errored-frame vs the recovery frame.** Inside a recovery body, `CallStack.Current` is the
   *recovery action's own* frame. The walk must skip it and find the frame that failed. `handle.cs`
   already identifies it (`erroredCall`, from `error.CallFrames[0]`), so the information exists — but
   the rule needs stating in code rather than being implicit in an AsyncLocal.
2. **Nested handlers.** Today LIFO restore gives an inner handler its own error and returns the outer
   one afterwards. The frame walk should reproduce that naturally (inner frame is nearer), but it
   must be tested, not assumed.
3. **`Handled`.** `Call.Handled` is flipped on recovery success. Does the walk skip handled frames?
   Probably yes — otherwise a recovered error stays visible as `%!error%` after recovery.
4. Whether `Call.Errors` (`app.callstack.call.error.@this`) and `Error.list` should be the same type.

## Consequence for the earlier plan

The implementation order in the main document is unchanged for everything except the slot: items
about `context.error`, the private AsyncLocal, and naming the scope call are **superseded** — there is
nothing to name. The `%!error%` registration in `actor/context/this.cs:192` changes from reading
`App.Error.Error` to walking the callstack.
