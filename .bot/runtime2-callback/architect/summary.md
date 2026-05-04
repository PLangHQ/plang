# architect — runtime2-callback

## v1 — Callback design ([details](v1/summary.md))

Three-pass whiteboard with Ingi: the design shape, then the subsystem walk, then a correction pass that simplified the model.

Locked: state-machine restoration via *bind state, jump to position, run* — never replay. Two issuers (`ask user`, error-retry) share one `App.Run(callback)` entry point but follow different capture policies. Each OBP `@this` declares its own snapshot via `ISnapshotted` (using `Snapshot.@this` and `Context.@this`, not invented helper types). Three buckets — snapshot-and-restore, reconstruct-on-build, drop. Islands rule — values only, no cross-graph identity.

Error-retry captures throw-time variables by reverse-applying the callstack's diff stream; Diff auto-flips on error. `App.Providers` is snapshot-and-restore at the registry layer (default selections + runtime registrations with DLL paths). Provider instances stay reconstruct-on-build.

Cache is **not** snapshotted. Cache is a hint, not state. The line between Variables and Cache is the line between "must survive resume" and "can be lost on resume." If a developer needs a value to be there on resume, the right tool is Variables. `MemoryStepCache` does not implement `ISnapshotted`; resumed App gets a fresh empty cache.

Resume always lands **at the action that's the focus** in both modes. No `Seek` verb — `App.Run(callback)` is the same main loop as `App.Run(goal)`, only the entry point differs. No `CallbackOrigin` — cross-process telemetry stitching is a log-layer concern.

Synthesis principle: **Variables are the values that survive resume. Everything else is a name.** Two trust layers gate resume: signature integrity (envelope) and referent integrity (named state still exists).

No code changed — design only. Open threads (wire format, storage ergonomics, ask-user builder annotation) deferred to next sessions.
