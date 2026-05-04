# architect — runtime2-callback

## v1 — Callback design ([details](v1/summary.md))

Whiteboard pass with Ingi to design the Callback feature: state-machine restoration via *seek + bind*, never replay. Two issuers (`ask user`, error-retry) share one engine-level resume primitive but follow different capture policies (developer-declared minimal slice for ask-user; full app state for error-retry). Each OBP `@this` declares its own snapshot via `ISnapshotted`; three buckets emerge (snapshot-and-restore, reconstruct-on-build, drop). Islands rule — values only, no cross-graph identity, no ref-capture. Error-retry captures throw-time variables by reverse-applying the callstack's diff stream from now back to the error point; Diff auto-flips when errors happen. `%!error.callback%` is the developer surface; `- run %callback%` invokes the seek primitive. No code changed — design only. Subsystem walk and wire-format design deferred to next sessions.
