# Resume — the core mechanic

## Why this exists

PLang executes goals as stateless flows. Two real situations need to interrupt that flow and continue it later, in a *different* process:

1. **`ask user`** — the goal needs a value the user has not provided yet. The web request that triggered the goal returns immediately with a form (or an external system holds the request). Hours or days later, the user submits, a fresh process picks up the response, and execution must continue at the `ask user` line with the user's answer bound to the declared variable.
2. **Errors that need durable retry** — a step fails, the developer wants to capture the failure and retry it later (different shift, different infrastructure state, different inputs). The original process is gone by the time retry happens.

Both cases share the same underlying mechanic: **construct a fresh App, jump to a specific position in a goal, hydrate the variable bag, and continue running from there as if the gap never happened.** No replay of earlier steps. No threads parked in memory. No coroutine state. Just *bind state, jump to position, run*.

## The shape

One **resume mechanism** at the engine level — `App.Run` accepts a Callback as its entry point, instead of a goal name. Two **issuers** that produce Callbacks consumed by it. Each issuer follows its own capture policy because the size and source of the carried state differ, but the resume contract is identical.

```
                   ┌──────────────┐
   ask user ────►  │              │
                   │  Callback    │  ──►  - run %callback%  ──►  App.Run(callback)
   error.handler   │  (record)    │
   ────►  %!error.callback%  ────►│              │
                   └──────────────┘
```

A `Callback` is a record (full schema in [callback-schema.md](callback-schema.md)). Issuers produce a `Data<Callback>`; the whole envelope is signed. The engine consumes it through `App.Run`. Storage and transport are developer-driven PLang code — neither issuer nor engine cares where the envelope lives between issue and resume.

## Bind, jump, run — never replay

Resume jumps directly to `(goal X, step Y, action Z)` and continues from there. No prior step is re-executed. No actions before Z within step Y are re-executed. The engine, when constructed with a Callback, lands at Z with the carried Variables and `ISnapshotted` state already bound, and continues from there.

This is the fundamental difference from event-sourcing-flavoured durable execution: we *don't* require producer steps to be deterministic. We snapshot enough state that the world *looks the same* at resume.

There is **no `Seek` verb**. The engine has one main loop; what differs between a normal start and a resume start is *where the loop's first tick lands*, not the existence of a different verb. `App.Run(goal)` and `App.Run(callback)` are two ways of configuring the entry point.

## Two issuers, one mechanism

| Aspect | `ask user` | error retry |
|---|---|---|
| Capture policy | Developer-declared minimal slice (`vars: %x%, %y%`) | Full app state via `ISnapshotted` |
| Storage medium | Wire (hidden form field, encrypted, signed) | Server-side, developer's choice (DB, file, queue) |
| Size constraint | Tight (HTTP form fields, tokens) | Effectively none |
| Post-resume contract | Lossy — developer reloads `%order%` from `%orderId%` | Transparent — state is as it was |
| Materialisation | Action returns `Callback` as its `Data` value | Lazily on read of `%!error.callback%` |
| Variable timing | Values at issuance | Values at *throw time* (handler scope is isolated) |
| Resume position | At the `ask` action — handler distinguishes fresh vs resumed | At the failed action — re-executed |

The handler-end vs. throw-time asymmetry is deliberate: `ask user` exists because the developer chose to issue a callback, so handler-end-state is the issuance moment. Error-retry is automatic, and the developer writing the error handler should not have to worry about whether their convenience-variable assignments collide with names from the failed code. Frozen view of the moment things broke.

## Position semantics

Resume always lands **at the action that's the focus** — same rule for both modes:

- **Error-retry**: at the failed action. Re-executed with the prepared state. If the cause was addressed (network restored, file recreated, `%name%` set to a valid value), it succeeds. If it fails again, a new `%!error.callback%` materialises in that error's handler. Self-similar.
- **Ask-user**: at the `ask` action. The handler distinguishes fresh-call from resumed-call (the bound input from the directive is the signal — coder figures out the exact mechanism), and on resume returns `Ok(boundValue)` instead of issuing a Callback. Same handler, two modes.

The simplification: there is no "before the action" or "after the action" position — the action *is* the resume point. Whatever the action's logic is, it runs with the state the directive provides.

## Names vs values — the synthesis

After walking every subsystem in `App/`, one principle organises the whole snapshot story:

> **Variables are the values that survive resume. Everything else is a name.**

- **Variables** are the only piece of state captured as full payloads. Their values can't be re-derived from anywhere else — that's why they're Variables.
- **Everything else** that needs to persist across resume — provider selections, identity, datasource, encryption choice, mode flags, runtime-loaded DLLs — is captured as a *name*. The provider/registry/store knows how to resolve the name to its current value. Names may carry small qualifications (a runtime-registered provider's name is paired with its DLL path so the loader knows how to find it) but they're not full values.
- **`Errors.Trail`** and **`App._statics`** are the small messy exceptions where there's no clean name to dereference, but their content is constrained — error records are small and known-shape; `_statics` is provisional until the TODO closes.

**Two separate trust layers gate a resume:**

1. **Signature integrity** — the signed `Data<Callback>` envelope guarantees the captured contents weren't tampered with. Without a valid signature, the resume is rejected wholesale.
2. **Referent integrity** — names in the snapshot assume the system state they reference still exists. If `myidentity2` was deleted between issue and resume, the resume fails. Same shape as `goal_hash` mismatch (redeployed goal → invalid). The signature does *not* guarantee referent integrity, by design.

Both must hold. Edge cases ("what if X was deleted between issue and retry?") get answered by these two layers: signature integrity fails loud as auth rejection; referent integrity fails loud as resolve-by-name failure. No silent degradation.

## The full issue/resume movie (durability layer)

See [resume-mechanics.md](resume-mechanics.md) for the step-by-step trace across the serialization boundary, goal lookup, and `Goal.Hash` gating.
