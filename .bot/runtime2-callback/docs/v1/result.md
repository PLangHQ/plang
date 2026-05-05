# Documentation result — runtime2-callback v1

## CHANGELOG entry (proposed)

```
### Added — Callbacks and snapshots

- **Callbacks subsystem** — typed suspend/resume records (`ICallback`).
  Two impls: `AskCallback` (slim — Position + ActorName + named Variables;
  used by `output.ask`) and `ErrorCallback` (full Snapshot — for the
  error-retry issuer). Both ride a signed `Data` envelope and dispatch
  through `callback.run`'s seal-then-verify gate. In-process and wire
  paths are indistinguishable to the security boundary — absence of
  signature is rejection, never trust.
- **PLang verb `- run %callback%`** (`callback.run`) — verifies signature,
  then dispatches into the callback's typed `Run`. Surfaces typed errors
  (`MissingCallbackSignature`, `CallbackSignatureMismatch`,
  `CallbackGoalNotFound`, `CallbackGoalHashMismatch`,
  `CallbackDispatchError`) — never raw exceptions.
- **`- ask user '...', vars: %x%, write to %y%`** — `output.ask` now
  carries developer-named variables across the suspend via `vars:` and
  resumes by binding the answer under the sentinel `%!ask.answer%`.
- **`application/plang+data` mimetype** — wire serializer for the full
  Data envelope (`Type` + `Value` + `Signature`). Lazy-signs Data on Write
  when the wrapped value is an `ICallback`.
- **App snapshot/restore** — `App.Snapshot.ISnapshotted` is the type-system
  classifier for subsystems that round-trip. Captured into a
  `Snapshot.@this` tree of named sections; `App.Restore` is gated on
  section presence so narrow wire shapes (CallStack + Variables only)
  round-trip cleanly. Implementers in v1: App, CallStack, Call, Variables,
  Errors, Errors.Trail, Providers, Statics, Build, Test.
- **Wire-size caps** — `AskCallback` 1 MB, `ErrorCallback` 4 MB. Defense
  in depth above the channel layer's primary control.
- **Sensitive-property strip** — `[Sensitive]`-marked properties are
  removed from the wire shape on both callback envelopes and the
  `application/plang+data` serializer.

### Renamed

- `App/modules/signing/SignedData.cs` → `App/modules/signing/Signature.cs`
  (and the type within). Reflects the wire shape: a `Signature` is an
  envelope concern, not a separate "signed data" record.

### Internal

- `Data.@this.Signature` populates lazily **only when the wrapped value
  is an `ICallback`**. Plain `Data<T>` keeps `Signature == null` until an
  explicit `EnsureSigned()` call. `RawSignature` (internal) is the
  verify-path's hatch for peeking without triggering populate.
- `RestoredFrame` — surrogate position record. NOT a `Call.@this`; not
  pushable into the AsyncLocal `Current`. Callbacks read it to identify
  the resume Position; dispatch happens through `App.Run` which Pushes
  a fresh live `Call`.
- `Errors.Push` sets `error.App = this.App` so `Error.Callback` can
  materialise on demand via `app.Snapshot()`.
```

## Documentation written

| File | Type | Note |
|---|---|---|
| `Documentation/v0.2/callbacks.md` | new | Architecture + developer reference for the callback subsystem |
| `Documentation/v0.2/snapshots.md` | new | `ISnapshotted` pattern, per-subsystem subtrees, referent integrity |
| `Documentation/v0.2/good_to_know.md` | append | 3 entries: lazy-Signature carve-out; RestoredFrame surrogate; Errors.Push App back-ref |
| `Documentation/v0.2/architecture.md` | update | Snapshot/Restore + Callbacks sections after Error Handling |
| `docs/modules/callback.md` | new | User-facing reference for `- run %callback%` |
| `docs/modules/output.md` | rewrite | `ask` is now its own subsection; covers `vars:` + resume mechanism |
| `docs/modules/index.md` | update | `callback` added to module reference; `output.ask` listed |

## Character proposals applied

| Proposal | Target | Decision |
|---|---|---|
| architect v2 — review server workflow | `characters/architect/character.md` | applied (Reviewing User Comments section) |
| architect v3 — test-designer prep two-files | `characters/architect/character.md` | applied (Preparing test-designer section) |
| coder v4 — `.test.goal` stub contract | `characters/coder/character.md` | applied (under Testing Requirements) |
| codeanalyzer v1 — read-only scope | `characters/codeanalyzer/character.md` | applied (new Scope section) |

## What was NOT documented (and why)

- **HTTP wire transport for ask resume** — out of branch scope per architect handoff. When it lands, `callbacks.md` "Out of scope" section should move to the main flow.
- **Real symmetric crypto** — out of branch scope; `callbacks.md` already notes that `crypto.encrypt`/`decrypt` are v1 identity pass-through.
- **Stale `.test.goal` scenarios** — not documentation gaps; tester / coder follow-ups (`AskVarsOnNonAsk`, `CallbackTimeoutSetting`, `DurabilityRoundTrip`, `TamperedSignature`).
- **PLang `.goal` examples for callback module** — kept minimal in `docs/modules/callback.md` (linked to `Tests/Callback/RunCallbackVerb/Start.test.goal` etc. via prose). Adding fresh examples is the tester's turf.

## Verdict

**pass** — the branch is ready to merge. Auditor v2 + security v1 already PASS; documentation is now complete; CHANGELOG entry prepared above for the merge commit.
