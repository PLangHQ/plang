# v1 — Callback design

## What this is

A design pass for **Callback** — PLang's mechanism for state-machine restoration: pausing a goal mid-flow (the original process dies) and resuming it later in a fresh process at the exact point it stopped, with the variables it had. Two real situations need this: `ask user` (developer asks for input, web returns immediately, user submits hours later) and **error-retry** (a step fails, the developer captures the failure for durable retry).

The architectural challenge: both situations look superficially different but share a core mechanic — *bind state, jump to position, run*. Construct a fresh App, jump to `(goal, step, action)`, hydrate variables, continue. No replay, no threads, no coroutine state. The session settled the design of that mechanic, walked every subsystem in `App/` to lock the snapshot inventory, and ran one more correction pass that materially shrank the design.

## What was done

Whiteboard-style architect conversation with Ingi across three passes — first the design shape, then the subsystem walk, then a correction pass that simplified the model. No code changed. The deliverable is the design doc at `v1/plan.md`. Key decisions, in conversational order:

1. **Two issuers, one mechanism.** `ask user` and error-retry both use `App.Run(callback)`. Differ in what state they carry, not how it's consumed.
2. **Per-type `ISnapshotted`.** Each OBP `@this` declares its own snapshot discipline. Three buckets: snapshot-and-restore, reconstruct-on-build, drop. Interface uses `Snapshot.@this` and `Context.@this` — proper OBP types, not invented `Reader`/`Writer`/`RestoreContext`.
3. **Islands rule.** Each `ISnapshotted` captures values, not graph identity. Ref-capture rejected.
4. **Throw-time vars for error-retry.** Reverse-apply diffs from the callstack stream; Diff auto-flips on error.
5. **`%!error.callback%`** is a synthetic, lazy PLang property.
6. **`App.Providers` snapshots at the registry layer** — default selections + runtime registrations with DLL paths. Provider instances stay reconstruct.
7. **Identity is name-only.** Provider resolves on resume. Whole `Data<Callback>` signed including `ActorName`.
8. **Resume position lands at the action in both modes** — error-retry re-executes the failed action; `ask user`'s handler distinguishes fresh vs resumed and returns `Ok(boundValue)` instead of issuing a Callback.
9. **No `Seek` verb.** `App.Run(callback)` is the same main loop as `App.Run(goal)`; only the entry point differs.
10. **No `CallbackOrigin`.** Cross-process telemetry stitching is a log-layer concern, correlated by callback identity. No structured runtime field.
11. **Cache is not snapshotted.** Reframed as a hint, not state. The line between Variables and Cache *is* the line between "must survive resume" and "can be lost on resume." `MemoryStepCache` does not implement `ISnapshotted`; resumed App gets a fresh empty cache. Sharpens the rule for developers: **if it must survive resume, it's a Variable.**

## The synthesis principle

> **Variables are the values that survive resume. Everything else is a name.**

Variables are the only piece of state captured as full payloads. Everything else that needs to persist (provider selections, identity, datasource, encryption, mode flags, runtime-loaded DLLs) is captured as a name; the provider/registry/store resolves the name on resume. Two trust layers gate the resume: signature integrity (envelope) and referent integrity (named state still exists).

`Errors.Trail` and `App._statics` are the small messy exceptions where there's no clean name to dereference, but their content is constrained.

## Code example

The `Callback` schema — single record, signed as `Data<Callback>`:

```csharp
record Callback(
    string GoalHash,
    int StepIndex,
    int ActionIndex,
    string ActorName,
    Dictionary<string, object?> VariablesByActor,
    Dictionary<string, object?> Selections,
    Dictionary<string, object?> Statics,
    List<IError> ErrorTrail,
    bool BuildEnabled,
    bool TestingEnabled,
    DateTimeOffset Expiry
);
```

The PLang surface a developer touches:

```plang
- insert into users, name=%name%
   on error call goal HandleError

HandleError
- write %!error.callback% to file callbacks/%!error.id%.bin

Recover
- read file callbacks/%id%.bin, write to %callback%
- run %callback%
```

## Next steps

The design is complete. Two reasonable next sessions:

1. **Wire format and key management for ask-user** — signing scheme over `Data<Callback>`, encryption, key rotation, `goal_hash` computation. Turns the design into something deployable.
2. **Hand to test-designer** to draft the smallest-first-cut test (in-process error → callback → resume) and the surrounding test plan. Coder picks up the resume mechanism once tests are designed.
