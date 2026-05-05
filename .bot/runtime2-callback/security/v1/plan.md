# security v1 — runtime2-callback — plan

## Subject

All C# added/changed on `runtime2-callback` vs. its base `runtime2-callstack`
(merge base `f8a0d641`). The branch implements:

- Stage 1 — `App.Snapshot.@this` + `ISnapshotted` (typed sections).
- Stage 2 — `CallStack` capture/restore + `Variables.SnapshotAt(error)` time-travel.
- Stage 3 — Lazy `Data.Signature` + `application/plang+data` serializer.
- Stage 4 — `AskCallback`, `ErrorCallback`, `callback.run`, `output.ask`,
  `crypto.encrypt`/`crypto.decrypt` (v1 identity), `signing.Signature` rename.

Auditor (`v1` PASS — F1 + F2 fixed in `3fbc7cfa`) and tester (`v1` PASS, 16
non-blocking quality findings) have already cleared the branch. Security is
the third gate: independent threat-model walk of the wire surface, not a
re-run of auditor's static checks.

## Threat model

The wire shape is the security boundary. Two callback envelopes cross it:

1. **AskCallback** — `{ ActorName, Position(GoalPrPath, GoalHash, StepIndex,
   ActionIndex), Variables(Name, Value)[] }`. The wire is `crypto.encrypt`-ed
   (v1 identity) JSON. The Data envelope around it carries a
   `signing.Signature` populated lazily on read for `ICallback` values.

2. **ErrorCallback** — `{ Frames(GoalPrPath, GoalHash, StepIndex, ActionIndex,
   ActionModule, ActionName, Id)[], Variables(Name, Value)[] }`. Same outer
   crypto/signing shape as AskCallback.

`callback.run` is the consumer-side gate: it either invokes `signing.verify`
(Ed25519 — full pipeline: type, age, expiry, nonce-replay, contracts,
headers, hash match, signature match) or skips when there's no signature.
Then it dispatches into `ICallback.Run` which does:

- `AskCallback.Run` → bind variables onto live ctx → dispatch the resumed action.
- `ErrorCallback.Run` → `App.Restore(AppSnapshot, ctx)` → dispatch `BottomFrame.Action`.

### Entry points to walk

- `PLang/App/modules/callback/run.cs` — verify gate, then `cb.Run`.
- `PLang/App/Callback/AskCallback.cs` — Serialize/Deserialize/Run.
- `PLang/App/Callback/ErrorCallback.cs` — Serialize/Deserialize/Run.
- `PLang/App/Variables/this.Snapshot.cs` — Capture (filter) and Restore (??).
- `PLang/App/CallStack/this.Snapshot.cs` — Restore guards (auditor F1 mirror).
- `PLang/App/Providers/this.Snapshot.cs` — `Assembly.LoadFrom(Source)` —
  is `Source` reachable from a wire?
- `PLang/App/Data/this.Envelope.cs` — lazy `EnsureSigned` for ICallback,
  `RawSignature` peek path.
- `PLang/App/Channels/Serializers/Serializer/PlangDataSerializer.cs` —
  `application/plang+data` round-trip; `[Sensitive]` filter applied?
- `PLang/App/modules/signing/providers/Ed25519Provider.cs` — full
  `VerifyAsync` pipeline; nonce cache TTL.
- `PLang/App/modules/crypto/encrypt.cs`, `decrypt.cs` — v1 identity confirmed.

### Threat categories

| # | Category | Asset | Question |
|---|---|---|---|
| T1 | Tampering | Position + Variables on wire | Can a wire reach `cb.Run` without an integrity check? |
| T2 | Authentication bypass | Whole callback wire | Does `callback.run` reject wires that arrive with no signature? |
| T3 | Replay | Nonce / expiry | Can a captured callback be replayed across the validity window? |
| T4 | Injection — variable namespace | `!`-prefixed infrastructure variables | Can a wire inject `!app`, `!ask.answer`, `!error.*` etc. onto resume? |
| T5 | Injection — provider DLL | Wire → `Assembly.LoadFrom(Source)` | Is `Providers.Source` ever populated from the wire? |
| T6 | Resource exhaustion | Wire body size, JSON depth | Are `MaxDepth`, body-length bounds applied to callback deserialization? |
| T7 | Confidentiality | `[Sensitive]` properties on captured Variables | Do callback wire serializers honor `SensitivePropertyFilter`? |
| T8 | Authorization | Goal selection at resume | Can a callback for goal A be repurposed to resume goal B? |
| T9 | Goal hash bypass | `goalHash` comparison | Is the comparison constant-time / canonical? |

### What's out of scope

- v1 crypto being identity is an explicit, documented v1 decision (architect
  Stage 3); v2 lands real symmetric crypto. Not flagged unless it interacts
  with another finding.
- The unbounded nonce cache growth — same nonce store as already audited in
  the runtime2 branch's signing review; covered there.
- Auditor's F1 (PositionWire bounds) and F2 (`!`-prefix filter on
  AskCallback.Run) are already closed in `3fbc7cfa` — they will be re-verified
  but not re-flagged unless incomplete.

## Plan

1. Walk each threat T1–T9 with the actual code paths. For each: trace from
   the wire byte to the side-effect.
2. Note residuals; classify by severity using the standing rubric (memory:
   `discipline.md`).
3. Re-verify auditor's F1/F2 fixes against the trace (PositionWire guards,
   AskCallback `!`-prefix filter).
4. Confirm test green on a clean rebuild (2720 C# + ~192 PLang).
5. Write summary, verdict, report. Update standing memory if a new pattern
   emerges.

## Severity rubric (from memory)

- **High**: exploitable today against shipping defaults; Ingi would not ship.
- **Medium**: design pattern locks in a bypass that activates as soon as a
  near-term lifting (here: HTTP channel wiring callbacks) lands. Or:
  exploitable today but narrowly bounded.
- **Low**: defense-in-depth; bounded by another invariant; or future-only
  with a fix that's a one-liner.
- **Note**: clarification, not a finding.
