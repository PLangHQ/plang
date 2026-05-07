# Coder summary — runtime2-channels

## Latest version: v9 — auditor v1 close-out + drop channel migration

### What this is

Auditor v1 returned FAIL with A1 (misleading `MigrationEnvelope.Signature`
that lied about coverage) and A3 (`Stream.AskCore` leaks reader + ignores
configured Encoding) as pre-merge blockers; A2/A4/A5 were deferred to
downstream feature work.

A1's design discussion led Ingi to remove the entire channel-migration
surface from this branch — the `cool.md` sketch is staying as a future
idea, but the half-built Stage 9 stub (`Channel.Migrate` +
`channel.migrate` action + `MigrationEnvelope` + `Signature` struct +
`FromMigration` + tests) is gone. The migration concept will be designed
properly when an actual cross-device transport need lands; Stage 9's stub
was code without a consumer.

A3 is a small one-line fix in `Stream.AskCore` plus a regression test.

### What was done in v9

- **Deleted** the entire channel-migration surface across:
  - `PLang/App/Channels/Channel/MigrationEnvelope.cs`
  - `PLang/App/Channels/Channel/this.cs` (Migrate / FromMigration / SignEmpty
    / ComputeSignature / VerifyEnvelope / SnapshotConfig)
  - `PLang/App/Channels/Channel/Stream/this.cs` (Migrate override)
  - `PLang/App/Channels/Channel/Goal/this.cs` (Migrate override + `GoalMigrationPayload`)
  - `PLang/App/modules/channel/migrate.cs` (action handler)
  - `PLang.Tests/App/ChannelsTests/Stage9_ChannelMigrateTests.cs`
  - `Tests/Channels/Migrate/{SessionOk,MessageError}/` (plang `.test.goal` files)
  - Module description on `PLang/App/modules/channel/set.cs` updated.
- **A3 fix** at `PLang/App/Channels/Channel/Stream/this.cs:115-120` —
  `using var reader = new StreamReader(Stream, ResolveEncoding(),
  detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen:
  true);` — closes the StreamReader leak and routes through the channel's
  configured Encoding.
- **A3 regression test** added at
  `PLang.Tests/App/ChannelsTests/Stage2_StreamChannelTests.cs` —
  `StreamChannel_Ask_HonoursConfiguredEncoding` writes `0xE9 0x0A` (invalid
  UTF-8 prefix; valid `é\n` in iso-8859-1) and asserts `AskCore` returns
  `"é"` when Encoding is `iso-8859-1`. Without the fix the test fails
  (UTF-8 default produces `U+FFFD`).

### Code example

A3 fix shape:

```csharp
// Channel/Stream/this.cs — AskCore
using var reader = new StreamReader(Stream, ResolveEncoding(),
    detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
var line = await reader.ReadLineAsync(timeoutCts.Token);
return Data.@this.Ok(line ?? string.Empty);
```

### Test results

- C#: 2755/2755 (was 2762; ‑7 net = 8 Stage 9 tests deleted + 1 Ask test added).
- PLang: 203 pass + 6 deliberate fixture fails (was 205; ‑2 net = 2 migrate
  test goals deleted).

### Hand-off

Auditor close-out next. A1 is satisfied by the wider deletion; A3 is fixed
with a regression test. A2/A4/A5 remain deferred per the original
auditor verdict — A2 is now moot (no `migrate` action exposes a Variables
snapshot, because no `migrate` action).

### Prior versions

v1–v8 history is in commits `30ec543a` → `38f9d153` and the per-version
`.bot/runtime2-channels/coder/v<N>/` directories. Headlines: stages 8+9
shipped (v1), `[Choices]` standardisation (v2), channel architecture
cleanup (v3, v3.1, v3.2), codeanalyzer fixes across v5–v7, tester v7
probe tests (v8). v9 (this version) closes auditor v1 by deleting Stage 9
entirely.
