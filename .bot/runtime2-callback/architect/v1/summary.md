# v1 — Callback design

## What this is

A design pass for **Callback** — PLang's mechanism for state-machine restoration: pausing a goal mid-flow (the original process dies) and resuming it later in a fresh process at the exact point it stopped, with the variables and provider state it had. Two real situations need this: `ask user` (developer asks for input, web returns immediately, user submits hours later) and **error-retry** (a step fails, the developer captures the failure for durable retry).

The architectural challenge: both situations look superficially different but share a core mechanic — *seek + bind*. Construct a fresh App, jump to `(goal, step, action)`, hydrate variables and ISnapshotted state, continue. No replay, no threads, no coroutine state. The session settled the design of that mechanic.

## What was done

Whiteboard-style architect conversation with Ingi. No code changed. The deliverable is the design doc at `v1/plan.md`. Key decisions, in conversational order:

1. **Two issuers, one primitive.** `ask user` and error-retry share `Seek(goal, step, action, vars, snapshot)`; they differ only in *what state they carry* and *who's responsible for the rest*. Ask-user: developer-declared minimal slice, wire-bounded, encrypted+signed. Error-retry: full app state, server-side, no size constraint.
2. **Per-type `ISnapshotted` interface.** Each OBP `@this` declares its own snapshot discipline. Three buckets emerge: snapshot-and-restore (Variables, Cache, Channels, recoverable Providers), reconstruct-on-build (Settings, FileSystem, Events), drop (Timing, history, in-flight network). The classifier *is* the type system.
3. **Islands rule.** Each `ISnapshotted` captures values, not graph identity. No cross-pointer fix-up phase. Ref-capture (`{ref: "Variables:name"}`) was brainstormed and rejected — no use case survived examination; size belongs to the serializer.
4. **Throw-time vars for error-retry, not handler-end.** Corrected mid-conversation by Ingi. The error handler is its own scope; its variable mutations are real but should not appear in the callback. The runtime computes throw-time-Variables by walking the callstack diff stream backward and reverse-applying every `Set` since the throw timestamp.
5. **Diff auto-flips on error.** No conditional path. If `Flags.Diff` is off when an error occurs, the runtime turns it on for the duration of error processing.
6. **`%!error.callback%`** is a synthetic, lazy PLang property. Materialised on read. Free if unused.
7. **Cross-process causality** goes through `Call.SetItem<CallbackOrigin>`, not by extending `Cause` (which stays same-process only).
8. **Smallest first cut**: in-process test that throws, reads `%!error.callback%`, hands it to a fresh App, asserts the failed action re-runs with throw-time vars. Validates the keystone before any wire/storage/UI work.

Files modified: only doc files under `.bot/runtime2-callback/architect/v1/`. No source touched.

## Code example

The seek primitive's whole engine-side surface — five fields, three behaviours during construction, one tweak to the main loop:

```csharp
record ResumeDirective(
    string GoalPath,
    int StepIndex,
    int ActionIndex,
    Variables.@this Variables,
    SnapshotBag Snapshot,
    CallbackOrigin? Origin
);

// In App constructor: if directive present, use it instead of default-construction
// for snapshot-and-restore types; reconstruct-on-build types unchanged.
// In Engine main loop: on first tick, navigate to (GoalPath, StepIndex, ActionIndex)
// instead of (entry goal, step 1, action 1).
```

The pattern PLang devs see:

```plang
- insert into users, name=%name%
   on error
      - write %!error.callback% to file callbacks/%!error.id%.bin

Recover
- read file callbacks/%id%.bin, write to %callback%
- run %callback%
```

## Next steps

Per Ingi's request at session end: documentation first to preserve context, then we continue. The doc is written. Two reasonable next sessions:

1. **Subsystem walk** (recommended) — go type-by-type through App OBP graph and pin down `Capture`/`Restore` per type. Variables, Cache, Channels, Providers (case-by-case), Errors trail, Actor, Catalog. The first-pass inventory in the plan is a starting point, not a finished classification.
2. **Wire format design** — for ask-user. Signing, encryption, key rotation, `goal_hash` computation, expiry. Independent of the seek primitive.

Seven open threads are enumerated at the bottom of `plan.md`. None blocks the smallest first cut.
