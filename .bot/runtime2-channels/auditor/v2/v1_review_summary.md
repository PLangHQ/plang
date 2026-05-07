# Auditor v1 → v2 transition

## v1 verdict (re-stated)

FAIL with pre-merge scope: **A1** (MigrationEnvelope.Signature did not cover Payload/Config — doc-vs-code lie + foot-gun PKI surface) and **A3** (Stream.AskCore leaked StreamReader and ignored Encoding). A2/A4/A5 deferred to later branches.

## What coder v9 did

Coder did **not** take the documented A1 path (rename `Signature` → `IntegrityHash`, `[Obsolete]` `VerifyEnvelope`, fix doc). Mid-implementation Ingi reframed:

1. The inner hash duplicates `crypto.hash`.
2. The outer `Data.Signature` (Ed25519) is the real trust gate.
3. "Migration" is the wrong word in software (collides with schema/db migration).
4. "I actually feel like all this Migration on channel is not needed now, lets remove it."

Coder pivoted scope to **delete the entire channel-migration surface** instead.

Removed:
- `PLang/App/Channels/Channel/MigrationEnvelope.cs`
- `PLang/App/modules/channel/migrate.cs`
- `Channel.@this`: `Migrate`, `MigrateSnapshot`, `FromMigration`, `SignEmpty`, `ComputeSignature`, `VerifyEnvelope`, `SnapshotConfig`
- `Stream.@this.Migrate`, `Goal.@this.Migrate` + `GoalMigrationPayload`
- `migrate` from the `channel.set` module description
- 8 Stage9 C# tests + 2 PLang fixture goals (`Tests/Channels/Migrate/*`)

Fixed (A3):
- `Stream.AskCore` now uses `using var reader = new StreamReader(Stream, ResolveEncoding(), ..., leaveOpen: true)` — disposes reader, honours configured Encoding, leaves underlying Stream open.
- Added regression test `StreamChannel_Ask_HonoursConfiguredEncoding` (iso-8859-1 0xE9 → "é").

## v2 scope

Confirm:
1. The deletion is **complete** (no dangling references to migrate/MigrationEnvelope/etc. in src or tests).
2. The bigger move is consistent with v1's pre-merge intent (A1 is closed by deletion; A2 is moot — there is no `migrate` action exposing Variables snapshot anymore).
3. A3 fix is correct and the regression test actually exercises the bug.
4. Test counts match coder's claim.
5. No new architectural debt introduced by the surgery (e.g., `Channel.App` backreference dangling unused).
