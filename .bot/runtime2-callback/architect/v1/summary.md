# v1 — Callback design

## What this is

A design pass for **Callback** — PLang's mechanism for state-machine restoration: pausing a goal mid-flow (the original process dies) and resuming it later in a fresh process at the exact point it stopped, with the variables and provider state it had. Two real situations need this: `ask user` (developer asks for input, web returns immediately, user submits hours later) and **error-retry** (a step fails, the developer captures the failure for durable retry).

The architectural challenge: both situations look superficially different but share a core mechanic — *seek + bind*. Construct a fresh App, jump to `(goal, step, action)`, hydrate variables and ISnapshotted state, continue. No replay, no threads, no coroutine state. The session settled the design of that mechanic, then walked every subsystem in `App/` to lock the snapshot inventory.

## What was done

Whiteboard-style architect conversation with Ingi across two passes — first the design shape, then the subsystem walk. No code changed. The deliverable is the design doc at `v1/plan.md`. Key decisions, in conversational order:

1. **Two issuers, one primitive.** `ask user` and error-retry share `Engine.Seek(callback)`; they differ in *what state they carry* and *who's responsible for the rest*. Ask-user: developer-declared minimal slice, wire-bounded, encrypted+signed. Error-retry: full app state, server-side, no size constraint.
2. **Per-type `ISnapshotted`.** Each OBP `@this` declares its own snapshot discipline. Three buckets emerge.
3. **Islands rule.** Each `ISnapshotted` captures values, not graph identity. Ref-capture rejected — no use case survived examination.
4. **Throw-time vars for error-retry.** Corrected mid-conversation by Ingi. Error handler is its own scope; its variable mutations are real but should not appear in the callback. Reverse-apply diffs from the callstack stream from now back to throw timestamp.
5. **Diff auto-flips on error.** No conditional path.
6. **`%!error.callback%`** is a synthetic, lazy PLang property.
7. **Cross-process causality** via `Call.SetItem<CallbackOrigin>`, not by extending `Cause`.

Then the subsystem walk refined the inventory:

8. **`MemoryStepCache` is audit-derived, not `ISnapshotted`.** Walk the callstack for `cache.set`/`cache.tryAdd` Calls (handlers `Tag` the resolved key), collect distinct keys, ask the cache for current state of each, capture `(key, value, absoluteExpiry)`. Per-run scoped, pluggable-cache friendly, generalisable as a pattern.
9. **`App.Providers` is snapshot-and-restore at the registry layer.** Captures default selections per type + runtime-registered (non-default) providers with their DLL paths. Provider *instances* themselves remain reconstruct-on-build; built-ins don't implement `ISnapshotted`.
10. **Names vs values is the unifying principle.** Snapshots store names; they store values only when the name doesn't determine the value. Variables and Cache fall on the values side; provider selections, identity, datasource, encryption, mode flags fall on the names side. Two trust layers gate resume: signature integrity (envelope) and referent integrity (named state still exists in the system).
11. **Identity is name-only.** Just `Identity.Name`; the provider resolves on resume. The whole `Data<Callback>` envelope is signed including `ActorName`, so tampering with who-runs-as breaks the signature.
12. **MemoryStepCache and memory Channels drop for now.** Cache loss documented as a known fragility — revisit when someone hits a wall.

Final snapshot surface is small: three `Variables` (per-actor, with existing exclusions), `Errors.Trail`, `App.Providers` registry selections, `App._statics`, plus `(key, value, expiry)` cache tuples derived from the callstack.

Files modified: only doc files under `.bot/runtime2-callback/architect/v1/`. No source touched.

## Code example

The `Callback` schema — single record, signed as `Data<Callback>`:

```csharp
record Callback(
    string GoalHash,
    int StepIndex,
    int ActionIndex,
    string ActorName,                 // System / Service / User
    Dictionary<string, object?> VariablesByActor,
    Dictionary<string, object?> Selections,        // names: provider defaults, identity, etc.
    List<CacheEntry> CacheEntries,                 // (key, value, absoluteExpiry)
    Dictionary<string, object?> Statics,
    List<IError> ErrorTrail,
    bool BuildEnabled,
    bool TestingEnabled,
    DateTimeOffset Expiry,
    CallbackOrigin? Origin
);
```

The PLang surface a developer touches:

```plang
- insert into users, name=%name%
   on error
      - write %!error.callback% to file callbacks/%!error.id%.bin

Recover
- read file callbacks/%id%.bin, write to %callback%
- run %callback%
```

## Next steps

The plan is complete enough that test-designer or coder can pick up the smallest first cut (in-process error → callback → resume). Open threads enumerated at the bottom of `plan.md` are independent layers — wire format, storage ergonomics, ask-user builder annotation. None blocks the seek primitive.

Recommended next session: **wire format and key management for ask-user**, since that's the load-bearing piece that turns the design into something deployable. Or hand off to test-designer with the design as locked.
