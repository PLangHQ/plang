# Test strategy — integration cuts and layering

Two integration cuts establish the end-to-end contract. Per-topic unit and surface tests fill in everything beneath. Concrete tests are test-designer's job; this file says what each integration cut must prove and how the layers split. The full coverage inventory — every behavior to verify, organized by topic — lives in [test-coverage.md](test-coverage.md).

## Scope

The two integration cuts below are **the contract for end-to-end behavior**. They are *not* the ceiling on what gets tested in this branch — per-type unit tests, per-surface PLang tests, and negative-path tests sit beneath them.

The split:

- **Integration cuts** (this file) — exercise the developer's full journey. Failing one of these = the design is broken at the system level.
- **Per-topic coverage matrix** ([test-coverage.md](test-coverage.md)) — every behavior the topic files commit to, mapped to a test layer. Failing one of these = a single `@this` is broken.
- **Failure matrix** ([test-coverage.md](test-coverage.md)) — negative paths only. Each row is a way the resume *should* fail; the test asserts the failure is hard, typed, and at the right layer.

Test-designer reads all three before writing tests. The matrix is the heavy lifter; this file is the spine.

## Test layer mapping

PLang has two test runners; tests pick a layer based on what they're proving:

- **C# TUnit** (`PLang.Tests/`) — pins internal behavior on `@this` types: snapshot/restore round-trips, time-travel projection, lazy signing populating, hash-matching, serializer wire shapes. The author needs to read the type, not the goal file.
- **PLang `.goal` tests** (`Tests/`) — pins developer-facing surfaces: `%!error.callback%` populating, `- run %callback%` dispatch, `- ask … vars: …` annotation honored, signed-but-stale → hard error visible to PLang `on error`. The author thinks like a PLang developer.
- **Integration cuts** (PLang goal tests with hand-rolled C# helpers where needed) — the two cuts below. Cross-layer, so each one straddles the runners.

Concrete layer choices for each behavior live in the coverage matrix.

## First cut — in-process resume

One test. No wire, no storage.

**Setup:** A goal with three actions: `set %x%=1`, then a throw, then `set %x%=2`. Run the App; it throws.

**Capture:** Read `%!error.callback%` from the failed App's context (which routes to `app.Errors.Current.Callback`) to get a `Data<ErrorCallback>`. The materialization runs `app.Snapshot()` — `app.Variables.SnapshotAt(error)` produces the throw-time view via diff reverse-apply; `App.CallStack.Snapshot()` captures the active frame chain.

**Resume:** Call `errorCallback.Run(ctx)` (the OBP root verb).

**What it must prove:**

- The resumed App lands at the bottom CallStack frame and re-executes the action. With the throw condition removed, the action that previously threw now succeeds. The next action (`set %x%=2`) runs. After resume, `%x% == 2`.
- The captured snapshot's `Variables` subtree reflects the **throw-time view**, not the post-handler state. So `callback.App.Variables["x"] == 1` even if the error handler mutated `%x%` between throw and capture.
- The CallStack snapshot's bottom frame matches the failed `(goal, step, action)`.
- `Run` returns `Task<Data>` with the resumed action's result — assertable downstream.

If this passes, the keystone is in: `callback.Run`, `app.Snapshot()` / `app.Restore()`, bind-jump-run, throw-time variable capture, CallStack-as-position, per-`@this` snapshot/restore.

## Second cut — durability round-trip

One test. Two-process simulation (one `App` instance per "process").

**Issue (process A):** Same setup as the in-process test — goal throws. Read `%!error.callback%`.

**Persist:** Write the callback through a channel that uses `application/plang+data`. The Channel picks `PlangDataSerializer`; the serializer reads `data.Value.Serialize(ctx)` (which encrypts the payload via `crypto.encrypt` — v1 identity pass-through) and reads `data.Signature` (triggering Data's lazy signing).

**Fresh process (B):** Construct a new `App`. Read the bytes back via `PlangDataSerializer` into `Data<ErrorCallback>` (signature comes back populated, unverified).

**Resume:** The `callback.run` action verifies the signature, then dispatches into `callback.Run(ctx)`. `Run` decrypts, resolves the goal stub, hash-matches, binds state, runs.

**What it must prove:**

- `Data.Signature` populates lazily on first read by a serializer, verifies cleanly on the read side.
- The encrypted-then-decrypted path round-trips byte-identical (trivially, with v1 identity crypto — but the wiring runs).
- The CallStack snapshot's bottom-frame Goal stub resolves against the live registry; `live.Hash == stub.Hash`.
- Resume runs the same way as in-process: `%x% == 2` after resume; `callback.App.Variables["x"] == 1`.

If this passes, the durability stack is green: encryption/decryption wiring, lazy signing on Data, channel routing through serializers, signature verification at the consumer, goal-hash gating, snapshot restore across the serialization boundary.

## What's not covered by these two cuts

These cuts only exercise **error-retry** issuance and the green path. Test-designer adds beneath them:

- **Ask-user issuer integration** — in-process `- ask user "...", vars: %x%` → AskCallback issued → resumed and bound. Even with HTTP wire transport deferred, the in-process shape is testable; lives in PLang goal tests.
- **Negative paths** — corrupted bytes, signature mismatch, expired signature, goal-hash mismatch, goal-not-found, unresolvable provider name, unresolvable identity name. All are documented in the failure matrix.
- **Per-`@this` round-trips** — Variables, Errors, Providers, Statics, Build, Testing, CallStack, Call. C# TUnit, one per type.
- **Per-surface PLang tests** — `%!error.callback%`, `- run %callback%`, ask-family `vars:` annotation, `- set callback timeout to 5 minutes`. PLang goal tests, one per surface.

The full list, with layer assignments, is in [test-coverage.md](test-coverage.md).
