# Callback — State-Machine Restoration via Seek + Bind

## Why this exists

PLang executes goals as stateless flows. Two real situations need to interrupt that flow and continue it later, in a *different* process:

1. **`ask user`** — the goal needs a value the user has not provided yet. The web request that triggered the goal returns immediately with a form (or an external system holds the request). Hours or days later, the user submits, a fresh process picks up the response, and execution must continue from the line after `ask user` with the user's answer bound to the declared variable.
2. **Errors that need durable retry** — a step fails, the developer wants to capture the failure and retry it later (different shift, different infrastructure state, different inputs). The original process is gone by the time retry happens.

Both cases share the same underlying mechanic: **construct a fresh App, jump to a specific position in a goal, hydrate the variable bag, and continue running from there as if the gap never happened.** No replay of earlier steps. No threads parked in memory. No coroutine state. Just *seek + bind*.

This branch designs that mechanic.

## The shape

One **resume primitive** at the engine level. Two **issuers** that produce values consumed by it. Each issuer follows its own capture policy because the size and source of the carried state differ, but the resume contract is identical.

```
                   ┌──────────────┐
   ask user ────►  │              │
                   │  Callback    │  ──►  - run %callback%  ──►  Engine.Seek(g,s,a, vars, snapshot)
   error.handler   │  (record)    │
   ────►  %!error.callback%  ────►│              │
                   └──────────────┘
```

A `Callback` is a record carrying `(goal_hash, step_index, action_index, vars, snapshot, expiry, signature)`. Issuers produce them. The engine consumes them through `Seek`. Storage and transport (wire, DB, file) are developer-driven PLang code — neither issuer nor engine cares where the callback lives between issue and resume.

## Settled design

### Seek + bind, never replay

Resume jumps directly to `(goal X, step Y, action Z)` and continues from there. No prior step is re-executed. No actions before Z within step Y are re-executed. The engine, on first tick after a `Seek`, lands at Z with the carried Variables and ISnapshotted state already bound, and continues to Z+1 (or step Y+1 if Z was the last action in Y).

This is the fundamental difference from event-sourcing-flavoured durable execution: we *don't* require producer steps to be deterministic. We just snapshot enough state that the world *looks the same* at resume.

### Two issuers, one primitive

| Aspect | `ask user` | error retry |
|---|---|---|
| Capture policy | Developer-declared minimal slice (`vars: %x%, %y%`) | Full app state via `ISnapshotted` |
| Storage medium | Wire (hidden form field, encrypted, signed) | Server-side, developer's choice (DB, file, queue) |
| Size constraint | Tight (HTTP form fields, tokens) | Effectively none |
| Post-resume contract | Lossy — developer reloads `%order%` from `%orderId%` | Transparent — state is as it was |
| Materialisation | At handler return time (action returns `Callback` as its `Data` value) | Lazily on read of `%!error.callback%` |
| Variable timing | Values at the moment of issuance | Values at *throw time* (handler scope is isolated) |

The handler-end vs. throw-time asymmetry is deliberate: `ask user` exists because the developer chose to issue a callback, so handler-end-state is the issuance moment. Error-retry is automatic, and the developer writing the error handler should not have to worry about whether their convenience-variable assignments collide with names from the failed code. Frozen view of the moment things broke.

### Per-type `ISnapshotted` — three buckets

Each OBP `@this` type declares its own snapshot discipline. No central registry, no classifier table.

```csharp
public interface ISnapshotted
{
    void Capture(SnapshotWriter w);
    static abstract void Restore(SnapshotReader r, RestoreContext ctx);
}
```

Three buckets emerge naturally:

1. **Snapshot-and-restore** — type implements `ISnapshotted`. Variables, Cache, Channels (in-memory buffers), recoverable Providers, Errors trail. Captured on issuance, restored on resume.
2. **Reconstruct-on-build** — type does *not* implement `ISnapshotted`. Settings, FileSystem handles, Events subscriptions, stateless Providers. Normal App construction rebuilds them; resume just runs construction.
3. **Drop** — runtime-only state with no resume relevance. Timing tier, Children-as-history, in-flight network state. Just gone.

The classifier *is* the type system. A new subsystem's author makes the bucket choice by deciding whether to implement the interface. No coordination across the codebase.

### Islands rule — values only, no graph identity

Each `ISnapshotted` captures **values**, not graph identity. If `Cache` holds an entry that came from `Variables`, `Cache.Capture` deep-clones the value into its own payload. Variables snapshots itself separately. Two independent islands.

After `Restore`, no pointer fix-up phase, no inter-type ordering dependency. Each subsystem rehydrates from its own bytes. References across types are by *name* (the same way PLang already works at runtime), resolved at lookup time, never restored as object pointers.

**Rejected:** ref-capture syntax like `{name:"x", value:{ref:"Variables:name"}}`. We brainstormed it. No use case survived examination — every candidate (cache as live view, indexes, late-bound config, current-selection pointers) collapsed into either "use the existing language mechanism" or "use the bucket system, this isn't what it's for." Size concerns belong to the serializer (blob-dedup with internal IDs), not the API surface. Closed.

**Limitation accepted:** intra-island graph identity (two list entries pointing to the same object) is not preserved across resume — JSON loses it. PLang is value-based; this rarely bites. Future serializer work can add `PreserveReferencesHandling`-style ID/IDREF if a real case appears.

### Variable capture semantics for error-retry

`%!error.callback%` is a **synthetic, lazily materialised** PLang property. It has no cost until read.

When read, the runtime computes Variables-at-throw-time by:
1. Take current `App.Variables.@this`.
2. Walk the callstack diff stream, filtered to events with `timestamp > error.throwTime`.
3. Reverse-apply each `Set` (restore each variable's `Before` value).

This means:
- Error handler mutations (`- set %name% = "ble"` inside the handler) appear in the live App's Variables but are *reversed* in the callback's view.
- The developer writing the error handler never has to reason about variable name collisions with the failed code.

**Diff is required.** When `Flags.Diff` is off and an error occurs, the runtime auto-flips it for the duration of error processing. Cost is paid only on the error path, which is rare. No conditional code path on the consumer side.

Providers do **not** have diff tracking. The callback captures their current state at materialisation time. Convention: error handlers should not mutate provider state. If they do, the callback reflects the post-handler value — the runtime will not catch this. Honest asymmetry: vars get rich treatment because we have rich tooling for them; providers get the pragmatic one.

### Position semantics

`Seek` lands at `(goal X, step Y, action Z)` and **re-executes action Z**. This is intentional — the developer's handler has presumably addressed the cause of failure (network restored, file recreated, %name% set to a valid value), and re-running the action with prepared state is the natural retry shape. If it fails again, a new `%!error.callback%` materialises in that error's handler. Self-similar.

For `ask user`, position is the action *after* the ask — the ask itself is satisfied by the bound `%name%` from the wire payload, and execution resumes at the next action.

### Cross-process causal trace

When a callback resumes, the new run gets a fresh callstack with its own root Call. To preserve causality across processes for telemetry, the engine writes a `CallbackOrigin` typed item onto the resumed root via the existing `Call.SetItem<T>` mechanism:

```csharp
record CallbackOrigin(string PriorRunId, string PriorCallId, DateTimeOffset IssuedAt);
```

The same-process `Call.Cause` field is *not* extended — its invariant ("live ref, same process only") stays clean. Cross-process identity goes through the typed metadata bag, exactly the use case the bag was designed for.

## PLang surfaces

### `%!error.callback%`

Synthetic, read-only, lazy. Available inside any error handler scope. Materialises a `Callback` record on read. Multiple reads in one handler with var mutations between them produce different callbacks — convention: read once at the end of the handler.

```plang
- insert into users, name=%name%
   on error
      - write %!error.callback% to file callbacks/%!error.id%.bin
```

### `- run %callback%`

Consumes a callback. Verifies signature, decrypts vars (sensitive ones only, in v1), confirms `goal_hash` against current build (mismatch = signed-but-stale → hard error), constructs an App with the seek directive, hands off to the engine.

```plang
Recover
- read file callbacks/%id%.bin, write to %callback%
- run %callback%
```

### Ask-user `vars:` annotation

Step-level on issuing actions only:

```plang
- ask user 'What is your name?', vars: %orderId%, write to %name%
```

`vars:` is **not** a Step-level concept (rejected in design — would imply error-retry needs declared vars too, which it doesn't). It belongs to ask-family actions specifically.

## The seek primitive — engine-side

`App` constructor accepts an optional `ResumeDirective`:

```csharp
record ResumeDirective(
    string GoalPath,
    int StepIndex,
    int ActionIndex,
    Variables.@this Variables,        // pre-bound, not built from scratch
    SnapshotBag Snapshot,             // ISnapshotted payloads keyed by class FQN
    CallbackOrigin? Origin            // for cross-process trace
);
```

When a `ResumeDirective` is present:
1. App construction runs normally for reconstruct-on-build types (Settings, FileSystem, Events).
2. Variables and ISnapshotted types are restored from the directive *instead of* default-constructed.
3. Engine's main loop, on first tick, navigates to `(GoalPath, StepIndex, ActionIndex)` and runs from there.
4. Root Call gets `SetItem(Origin)` if Origin is non-null.

That's the entire engine-side surface. Five fields, three behaviours during construction, one tweak to the main loop. Everything else (issuance, signing, storage, wire format, UI) is layers on top.

## Subsystem inventory — first-pass buckets

| Subsystem | Bucket | Notes |
|---|---|---|
| `App.Variables` | snapshot-and-restore | Sensitive-marked entries AES-wrapped inside the snapshot. |
| `App.Cache` | snapshot-and-restore | Mid-run cache misses on resume defeat the no-replay rule. |
| `App.Channels` (buffered) | snapshot-and-restore | In-flight queued items. |
| `App.Providers` | mostly reconstruct-on-build; case-by-case | Pure handles (DB, HTTP) reconstruct. Stateful ones (in-memory cache, conversation history) implement `ISnapshotted`. |
| `App.Settings` / `App.Config` | reconstruct-on-build | Disk-backed, idempotent. |
| `App.FileSystem` | reconstruct-on-build | Handles don't survive crashes. |
| `App.Events` (lifecycle) | reconstruct-on-build | Re-attach during App boot. |
| `App.Errors.Trail` | snapshot-and-restore (read-only context) | Resume needs to know what it's recovering from. |
| `App.CallStack` | special — see below | |
| `App.Actor` | unwalked | Need to inspect. |
| `App.Catalog` | unwalked | Need to inspect. |
| `App.Test` | reconstruct-on-build | Test machinery, no live state. |

**CallStack as positional context.** Not history (drop), not timing (drop). The *Caller chain* of the throwing Call is captured as a sequence of `(goal, step, action)` resume points so when the resumed action finishes and unwinds, control returns to the caller correctly. Concretely: if goal A's step 3 called goal B and B's step 2 errored, resume needs to know "after this resumed B step 2 finishes, return to A step 3 action N+1." That's a small list of positions, not a tree.

This inventory needs a deeper walk type-by-type before implementation. Flagged for the next architect session.

## Smallest meaningful first cut

One in-process test, no wire, no storage, no UI:

```csharp
[Fact]
public async Task Error_callback_resumes_at_failed_action_with_throwtime_vars()
{
    var app = new App(/* goal with: set %x%=1; throw; set %x%=2 */);
    await app.Run();  // throws
    var callback = app.GetItem<ErrorCallback>();  // synthetic %!error.callback%

    var resumed = new App(callback.ToResumeDirective());
    await resumed.Run();

    Assert.Equal(2, resumed.Variables["x"]);  // step 3 ran, %x% is 2
    Assert.Equal(1, callback.Variables["x"]); // throw-time view, never 2
}
```

If that passes, every remaining piece (signing, encryption, wire format, storage backends, ask-user web flow) is mechanical. The seek primitive is the keystone — get it green first, layer the rest.

## Open threads — for future sessions

These were intentionally deferred. None blocks the seek primitive.

1. **Wire format and key management for ask-user.** Signing scheme, encryption (AES-GCM probably), key rotation, expiry semantics, how `goal_hash` is computed and stored.
2. **Storage shape for error-retry.** Recommended patterns — file, DB provider, queue. Probably no runtime opinion; developer chooses via PLang. But might want a built-in `callback.store` / `callback.load` action pair for ergonomics.
3. **`goal_hash` mismatch handling.** Signed but stale → hard error vs. recoverable. Probably hard error (security correct), but worth deciding.
4. **PLang surface for `- run %callback%`.** Is it a normal action in a `callback` module, or a special infrastructure verb? Lean: normal action. The seek primitive is the runtime mechanism; PLang sees just another module call.
5. **Subsystem walk** for Variables / Cache / Channels / Providers / Actor / Catalog — `Capture`/`Restore` per type, including how Variables wraps sensitive entries.
6. **Ask-user builder annotation** — exact `.pr.json` shape for `vars: %x%, %y%` on a step.
7. **Cross-process trace** (`CallbackOrigin` payload) — what fields are useful for telemetry stitching.

## Settled rejections (context for future sessions)

- **`Data.Pause` lane.** Callback is a successful `Data` value whose payload happens to be a Callback record. Engine has one extra branch ("is the value a Callback? unwind without continuing"). No new Ok/Fail/Pause trichotomy.
- **Step-level `vars:` capture annotation.** Only on ask-family actions. Error-retry doesn't need declared vars — it captures everything.
- **`Cause` extension to cross-process.** Stays same-process only. Cross-process causality goes through `Call.SetItem<CallbackOrigin>`.
- **Replay-style resume.** Never. Seek + bind only.
- **Re-running producer steps for stateful provider re-acquisition.** Not in v1. Providers are either pure (reconstruct), stateful-snapshottable (`ISnapshotted`), or out of luck (silently degrade). Event-sourcing is a separate, larger conversation.
- **Ref-capture across islands.** Brainstormed, no real use case, closed. Values-only. Serializer-level blob dedup is the answer if size becomes a problem.
