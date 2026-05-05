# Resume mechanics — the issue/resume movie

The conceptual mechanic (bind, jump, run) lives in [resume.md](resume.md). This file walks the same flow concretely across the serialization boundary — issue, persist, deserialize, verify, decrypt, run.

## Issue (developer's `on error` runs)

```plang
- insert into users, name=%name%
   on error call goal HandleError

HandleError
- write %!error.callback% to file callbacks/%!error.id%.bin
```

Step-by-step:

1. `insert into users` blows up. Error is captured in `App.Errors`; handler dispatch begins.
2. `HandleError` runs as the recovery body.
3. `%!error.callback%` (which lives at `app.Errors.Current.Callback`) is read → lazy materialization triggers: `app.Snapshot()` walks the App tree, `app.Variables.SnapshotAt(error)` produces the throw-time view via diff reverse-apply, `App.CallStack.Snapshot()` captures the active frame chain. The result is wrapped in `ErrorCallback(snapshot)` and then `Data<ErrorCallback>`. See [callback-schema.md](callback-schema.md#lazy-materialization-of-errorcallback).
4. `- write %!error.callback% to file ...` invokes the file channel.
5. Channel picks the right `Serializer.@this` for the file's mimetype (see [transparent-signing.md](transparent-signing.md)). For a callback, that's `PlangDataSerializer` (`application/plang+data`).
6. `PlangDataSerializer` reads `data.Value.Serialize(ctx)` for the payload (encrypted via `crypto.encrypt` — see [encryption-layering.md](encryption-layering.md)) and reads `data.Signature` to emit on the wire. Reading `data.Signature` triggers Data's lazy signing — `signing.SignAsync` runs with `expiresInMs` seeded from `ctx.App.Callback.Signature.ExpiresInMs`.
7. Final signed envelope hits disk.

## Resume (developer triggers it)

```plang
Recover
- read file callbacks/%id%.bin, write to %callback%
- run %callback%
```

Step-by-step:

1. `read file ...` reads bytes. The right `Serializer.@this` for the file's mimetype reconstructs `Data<ICallback>` (typed dispatch picks `Data<ErrorCallback>` or `Data<AskCallback>` based on the envelope) with `Signature` populated (unverified — verification is the consumer's explicit step, not automatic on read).
2. `- run %callback%` calls `callback.Run(ctx)` — the interface verb. Each impl owns its body and returns `Task<Data>`, propagating whatever the resumed action returned.

## `ErrorCallback.Run(ctx)`

```
errorCallback.Run(ctx)
├── 1. signing.verify(data.Signature) over the encrypted payload bytes
│      └── Hard error on integrity / expiry / identity mismatch
├── 2. crypto.decrypt(payload bytes) → plaintext snapshot bytes
│      └── ErrorCallback.Deserialize(plaintext, ctx) reconstructs the snapshot tree
├── 3. Construct fresh App (RegisterDefaults runs, modules + channels boot normally)
├── 4. app.Restore(snapshot, ctx) — walk subtrees and dispatch to each @this:
│      • App.Providers.Restore  → replay runtime registrations, apply default selections
│      │                          (hard error if any name doesn't resolve)
│      • App.Variables.Restore  → bind throw-time variable values
│      • App.Errors.Restore     → populate Trail
│      • App._statics.Restore   → bind statics dict
│      • App.Build.Restore      → set IsEnabled
│      • App.Testing.Restore    → set IsEnabled
│      • App.CallStack.Restore  → reconstruct frame chain
│      • Each frame's Goal-stub resolves against app.Goals; hash-match (hard error on mismatch)
├── 5. Main loop's first tick lands at the bottom CallStack frame (Goal, StepIndex, ActionIndex)
│      → re-executes the failed action with the bound state
```

`callback.Run` is the only verb the resumer sees. Position lives inside `app.CallStack` after restore — the engine pulls it from there, just like in any normal run.

## `AskCallback.Run(ctx)`

Different shape inside, same interface verb outside.

```
askCallback.Run(ctx)
├── 1. signing.verify(data.Signature)
├── 2. crypto.decrypt(payload bytes)
├── 3. AskCallback.Deserialize(plaintext, ctx) → record fields populated
├── 4. Resolve Position.Goal stub against app.Goals; hash-match (hard error on mismatch)
├── 5. Construct fresh App
├── 6. Bind callback.Variables into App.Variables (the names the developer pinned via `vars:`)
├── 7. Dispatch the ask action at (Position.StepIndex, Position.ActionIndex) with callback.Actor
│      as actor context; the action returns the bound value instead of issuing a fresh ask
```

No App snapshot to walk — ask-user is a clean pause, the developer named the surviving variables, the rest is fresh App boot.

## Goal hash subtlety — known limitation

`Goal.Hash` (at `PLang/App/Goals/Goal/this.cs:121`) covers the developer's prose: goal name + concatenated step text. It does **not** cover:

- The compiled `.pr.json` (so a recompile that doesn't change source produces the same hash — good, this is what we want).
- The behavior of modules referenced by steps. If `file.write`'s implementation changes between issue and resume in a way that changes step semantics without the prose changing, the hash stays the same and the resume runs against the new behavior.

**Accept this.** The dominant invalidation case is "developer changed the goal file," and `Goal.Hash` catches that. Module-behavior drift is a rare cross-cut concern. A future branch could extend the hash to include module signatures (or the resolved `.pr.json` schema), but it's not warranted now.

Documented as a known limitation in [open-threads.md](open-threads.md), not blocking.

## No cross-process causal trace

There is **no cross-process causal trace** in the runtime data model. Telemetry stitching between the original run and the resumed run happens at the log layer by correlating callback identity (signature digest, expiry timestamp). `Call.Cause` stays same-process only — its invariant ("live ref, same process only") is preserved.
