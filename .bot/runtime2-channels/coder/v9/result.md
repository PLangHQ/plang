# Coder v9 — Result

## What was decided mid-session

Plan started as "drop misleading PKI surface from `MigrationEnvelope`, rename
inner `Signature` → `IntegrityHash`, fix `Stream.AskCore`." During
implementation:

1. Ingi flagged that the inner hash duplicated `crypto.hash`. I argued that
   if the outer `Data.Signature` (Ed25519 via `EnsureSigned`) is the real
   trust gate, the inner hash adds zero value — drop it entirely.
2. Ingi then questioned `MigrationEnvelope` itself: "sounds like something
   wrong." Two issues — "Migration" is the wrong word in software (means
   schema/db migration), and `Data` already is the envelope (so wrapping
   one envelope in another is a layer that doesn't pay rent).
3. Ingi: "I actually feel like all this Migration on channel is not needed
   now, it's cool but I don't need it, lets remove it."

Final scope = (a) **delete all channel migration**, (b) A3 fix + regression test.

## Files removed

- `PLang/App/Channels/Channel/MigrationEnvelope.cs`
- `PLang/App/Channels/Channel/ChannelConfigSnapshot.cs` (transient — created and deleted within this session)
- `PLang/App/modules/channel/migrate.cs`
- `PLang.Tests/App/ChannelsTests/Stage9_ChannelMigrateTests.cs`
- `Tests/Channels/Migrate/SessionOk/` (`Start.test.goal` + `ChatGoal.goal` + `.build/`)
- `Tests/Channels/Migrate/MessageError/` (`Start.test.goal` + `.build/`)

## Files modified

- `PLang/App/Channels/Channel/this.cs` — removed `Migrate`, `MigrateSnapshot`,
  `FromMigration`, `SignEmpty`, `ComputeSignature`, `VerifyEnvelope`,
  `SnapshotConfig`. Removed the "Stage 9: migration API stub" section. Cleaned
  the `App` xmldoc that mentioned `SignEmpty`.
- `PLang/App/Channels/Channel/Stream/this.cs` — removed `Migrate` override.
  A3 fix: `AskCore` now uses `using var reader = new StreamReader(Stream,
  ResolveEncoding(), detectEncodingFromByteOrderMarks: false, bufferSize: 1024,
  leaveOpen: true);` — disposes the reader and honours the configured Encoding.
- `PLang/App/Channels/Channel/Goal/this.cs` — removed `Migrate` override and
  `GoalMigrationPayload` class.
- `PLang/App/modules/channel/set.cs` — module description string updated to
  drop "migrate" from the (set, remove, migrate) list.

## Files added

- `PLang.Tests/App/ChannelsTests/Stage2_StreamChannelTests.cs` —
  `StreamChannel_Ask_HonoursConfiguredEncoding` regression test for A3.

## Auditor findings status

- **A1** — addressed by the bigger move. The misleading `Signature` struct
  (and the `VerifyEnvelope` API trap) is gone because the entire migration
  surface is gone. The doc-vs-code mismatch can no longer mislead anyone.
- **A3** — fixed (`using` + `ResolveEncoding`) + regression test.
- **A2 / A4 / A5** — out of scope per auditor verdict; A2 is moot now (no
  `migrate` action exposes Variables snapshot, because no `migrate` action).

## Test results

- C#: 2762 → 2755 (deleted 8 Stage 9 tests, added 1 Ask encoding test → net ‑7). 2755/2755 pass.
- PLang: 205 → 203 (deleted 2 migrate test goals → net ‑2). 203 pass + 6 deliberate fixture fails.

## Notes for next bot (auditor close-out)

The auditor's pre-merge scope (A1 + A3) is closed. A2 is no longer applicable
on this branch. `Channel.@this.App` backreference is now used only for general
App access; if dead under static analysis, the auditor can flag for a
follow-up cleanup, but it's still touched at boot for `App.User.Channels`
plumbing — not unused.

`cool.md` (Documentation/Runtime2/) still describes "Channels that migrate
across devices" as a future idea — left intact as a forward-looking sketch.
