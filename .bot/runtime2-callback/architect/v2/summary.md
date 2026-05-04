# v2 — Wire format and Phase 2 staging

## What this is

The wire-format and persistence design for Callback, scoped to **Phase 2** (error-retry only). v1 settled the snapshot/restore mechanics; v2 is the layer above — how callbacks travel through serialization, get signed, persist to developer-chosen storage, and verify on resume. Phase 3 (ask-user + encryption) is explicitly deferred to a future v3 doc.

## What was decided

Three insights from the v2 conversation reshape the design:

1. **Signing is transparent at the Data IO boundary.** Any `Data.@this` written through the Channels serializer gets its `Signature` auto-populated by the existing `signing` module. Default signature has *no expiry* — integrity, not validity, is the durable guarantee. Developers add expiry explicitly via `- sign %callback% expires in N`. No explicit `- sign` is needed at the issue site for the common case.
2. **`Goal.Hash` already exists** at `PLang/App/Goals/Goal/this.cs:121` (SHA-256 of name + step text). `Callback.GoalHash = goal.Hash` directly — no new hashing needed. Limitation: tracks developer prose, not module-behavior drift. Documented.
3. **Encryption is a Callback-internal concern, not a Data layer concern.** Most Data writes (logs, files, debug) don't need encryption. The Callback class owns its own encryption discipline (Phase 3 only — `Callback.EncryptInPlace(ctx)` calls `ctx.App.Modules.Get('crypto')`). The Data layer signs the encrypted bytes.

The combined consequence: **Phase 2 ships entirely on existing infrastructure.** The `signing` module already does what we need; `Goal.Hash` already exists; storage is the developer's PLang code. No new modules, no new crypto, no new core abstractions beyond what v1 already designed.

Schema corrections to `Callback`:

- **Added** `GoalPrPath` (string) — `App.Goals` loads goals lazily by path; the resumer needs the path to find one.
- **Removed** `Expiry` — it was duplicating what `Data.Signature` already carries via `SignedData.Expires`. One source of truth.

OBP cleanup flagged: rename `App.modules.signing.SignedData` → `Signature`. The current name is a leftover from pre-OBP-shape rewriting.

## Phase split locked

| Phase | Scope | New infra |
|---|---|---|
| Phase 1 | In-process error → callback → resume test | None |
| Phase 2 | Error-retry: signed envelope, dev-chosen storage, full resume | None |
| Phase 3 | Ask-user: HTTP wire transport, encryption | Extends `ICryptoProvider` with `EncryptAsync`/`DecryptAsync` |

Phase 2 captures most of the durable-execution value. Phase 3 is the wire-bound special case.

## Code example

The transparent IO hook — the only new code path Phase 2 needs:

```csharp
// In App.Channels.Serializers, on the Data write path:
if (data is Data.@this d && d.Signature == null)
    d.Signature = await signing.SignAsync(d, expiresInMs: null);
```

The PLang surface a Phase 2 developer touches:

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

Phase 2 design is complete and ready for handoff. Two reasonable next moves:

1. **Hand to test-designer** for the smallest-first-cut test plan around the round-trip flow.
2. **Coder** picks up Phase 2 implementation: transparent-signing IO hook, `SignedData` → `Signature` rename, `Callback` record + materialization, `App.Run(callback)` entry point, `callback.run` action, goal-hash mismatch error path.

Phase 3 (ask-user + encryption) is its own future architect session. Encryption design (extending `ICryptoProvider`) is small in shape — symmetric AES-256-GCM via the existing `IKeyProvider`, encrypt/decrypt as Callback-class methods.

## Files

- `v2/plan.md` — full design.
- `v1/plan.md` — referenced for state model (snapshot/restore, ISnapshotted, names-vs-values, etc.). v2 is a delta on it.
