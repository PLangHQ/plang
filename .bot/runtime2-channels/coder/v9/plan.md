# Coder v9 — auditor v1 fixes (A1 + A3)

**Input:** `.bot/runtime2-channels/auditor/v1/report.md` (FAIL → coder is next).
Scope = A1 + A3. A2 / A4 / A5 deferred per auditor verdict.

A1 picks the security-recommended path (delete the misleading struct), not the
"fix-doc + `[Obsolete]`" alternative. Three reasons (see chat with Ingi):
no callers today, the outer `Data.Signature` is the real Ed25519 trust gate,
and removing the foot-gun is safer than papering over it.

## A1 — drop misleading PKI surface from `MigrationEnvelope`

### Source changes

1. `PLang/App/Channels/Channel/MigrationEnvelope.cs`
   - Replace `class Signature` with `class IntegrityHash`.
   - `IntegrityHash` carries only `byte[] Bytes` — drop `IdentityName` and
     `PublicKey`. xmldoc explicitly states it is **not** a cryptographic
     signature; the outer `Data.Signature` (Ed25519) is the trust gate.
   - Rename `MigrationEnvelope.Signature` field → `IntegrityHash`.

2. `PLang/App/Channels/Channel/this.cs`
   - Delete `VerifyEnvelope` (zero callers in src; only callers were the two
     tests we're rewriting).
   - Replace `SignEmpty()` with `ComputeIntegrityHash(object? payload)` —
     SHA256 over a canonical JSON of `(name, direction, config, payload)`.
     `Config` and `Payload` are now covered (closes auditor's "doc lies"
     concern by making the hash actually cover what the old doc claimed).
   - Remove `ComputeSignature` (folded into `ComputeIntegrityHash`).

3. `PLang/App/Channels/Channel/Stream/this.cs:204-211` — pass `payload` to
   `ComputeIntegrityHash` and assign to `IntegrityHash` field.

4. `PLang/App/Channels/Channel/Goal/this.cs:96-112` — same.

### Test changes (`PLang.Tests/App/ChannelsTests/Stage9_ChannelMigrateTests.cs`)

- Delete `MigrationEnvelope_IsSignedBySourceSystemIdentity` — there is no
  identity-bearing inner shape any more. The outer `Data.Signature` already
  has separate end-to-end coverage (Stage-9 plan; not on this branch's
  blast radius).
- Replace `MigrationEnvelope_Signature_IsVerifiable` with
  `MigrationEnvelope_IntegrityHash_DiffersOnTamper` — construct two
  envelopes that differ only in `Name` and assert their `IntegrityHash`
  bytes differ. (We're no longer claiming verifiability; we're asserting
  the hash actually covers the fields it claims to.)
- `ChannelThis_FromMigration_PresentButThrowsNotImplemented` — update
  fixture to construct `IntegrityHash` instead of `Signature`.

## A3 — `Stream.AskCore` `using` + `ResolveEncoding`

`PLang/App/Channels/Channel/Stream/this.cs:115-119`

```csharp
using var reader = new StreamReader(Stream, ResolveEncoding(),
    detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
var line = await reader.ReadLineAsync(timeoutCts.Token);
return Data.@this.Ok(line ?? string.Empty);
```

Two effects:
1. Reader is disposed on exit (no leak; the underlying Stream survives via
   `leaveOpen: true`).
2. Decoding follows `ResolveEncoding()` so a channel configured
   `Encoding = "iso-8859-1"` actually reads iso-8859-1.

Auditor explicitly noted: this does **not** fix the cross-call buffer-pre-read
issue (a permanent cached reader would). That's correctly out of scope —
when a real non-console Ask scenario lands, the cached-reader rework is a
separate piece of work and `using` does not regress what's there today.

### Regression test (Stage2_StreamChannelTests.cs)

`StreamChannel_Ask_HonoursConfiguredEncoding`: feed iso-8859-1 bytes for
"naïve\n" (where 'ï' is byte `0xEF`), set `Encoding = "iso-8859-1"`, assert
`AskCore` returns the decoded string `"naïve"`. Without the fix, the reader
defaults to UTF-8 and `0xEF` decodes to U+FFFD or fails the bytes-form test.

## Out of scope (auditor explicit)

- A2 — `migrate` permission gate / by-value Snapshot — bundle with Stage 9 transport.
- A4 — `Variables.Set` dot-path overlay routing — bundle with parallel-foreach.
- A5 — `PlangDataSerializer` size/depth caps — bundle with Stage 9 transport.

## Verification plan

1. C# tests: `dotnet run --project PLang.Tests` — must remain 2762/2762
   (the two Stage9 deletions + replacement + the one new Stage2 test net
   to ±0 on count).
2. PLang tests: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`
   — must remain 205 pass + 6 deliberate fixture fails.
3. Commit + push.
4. Hand back to auditor for the close-out pass.
