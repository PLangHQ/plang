# Auditor v2 — close-out pass on coder v9

**Reviewed state:** dfcc6b96 (`runtime2-channels coder v9: drop channel migration; A3 Stream.AskCore fix`)
**Baseline:** auditor v1 (FAIL, pre-merge: A1 + A3).

## Independent verification performed

1. **Build clean** — `dotnet build PlangConsole` → 0 errors, warnings only.
2. **Migration dead-code search** —
   `grep -rn "Migrat|MigrationEnvelope|VerifyEnvelope|FromMigration|SignEmpty|ComputeSignature|MigrateSnapshot|SnapshotConfig"` across src/tests returned only `.gitignore` (`MigrationBackup/`) and `Plng001PostMigrationTests` (unrelated source-generator diagnostic). **Clean.**
3. **C# tests** — `PLang.Tests/bin/Debug/net10.0/PLang.Tests` → 2755/2755 pass. Matches coder's claim (2762 → 2755 = -8 Stage9 + +1 encoding regression).
4. **PLang tests** — `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` → 203 pass + 6 deliberate fixture fails. Matches.
5. **A3 regression test runs and asserts the right thing** — `StreamChannel_Ask_HonoursConfiguredEncoding` reads `0xE9 0x0A` with `Encoding = "iso-8859-1"` and expects "é". Without the fix, default UTF-8 reader yields U+FFFD. Test passes (15/15 in Stage2 suite).
6. **Channel.App backreference still load-bearing** — `grep` confirms `App` is read inside `Channel.@this` for app-level event-binding match (lines 186-190) and diagnostic `App?.Debug?.Write` (line 241). Not orphaned by the migration removal. Coder's claim is correct.
7. **`Documentation/Runtime2/cool.md`** — still mentions "Channels that migrate across devices" as a forward sketch. Acceptable — it's a future idea, not a contract.

## Findings

**None** at critical/major. Three notes for the docs bot to be aware of:

- **N1 (note, doc):** `cool.md` "Channels that migrate across devices" line is now a forward-looking sketch with **no implementation in tree**. Not wrong, but if the docs bot does a sweep, it should either keep the sketch labelled as future or move it to a roadmap doc — it should not read as documenting current behaviour.
- **N2 (note, architectural fit):** The pivot from "fix A1" → "delete migration surface" is the right call. The original A1 finding was that `Signature` covered only `(Name, Direction, ChannelType)` — none of `Config`, `Payload`. Renaming to `IntegrityHash` would have papered over the foot-gun without removing it. Deletion is structurally correct: zero callers existed, and the outer `Data.Signature` (Ed25519) is the real trust gate. Less code, less attack surface, less doc-vs-code drift risk. When real cross-device transport lands, it can design the envelope from scratch under Stage 9 with security review.
- **N3 (note, scope consistency):** A2 (the `migrate` action exposing `Variables` snapshot by-ref) is mooted by the deletion — there is no `migrate` action anymore. v1's "bundle with Stage 9 transport" carries forward unchanged: when migration is rebuilt, a permission gate on `Variables` exposure must be designed in from the start, not bolted on.

## Cross-bot review of the previous reviewers

- **Codeanalyzer (v4):** PASS on B1 + L1 — those fixes are still in tree (Events._active static, Enter HashSet ownership). Coder v9 didn't touch Events. Agree.
- **Tester (v7):** PASS with 2 minor missing-coverage notes. Coder v9 added one regression test (encoding) and removed Stage9 tests. The deleted Stage9 tests were testing code that no longer exists, so their removal is correct (not a coverage regression). Agree.
- **Security (v1):** PASS with 1 medium + 1 low + 3 notes. Their medium was on the `migrate` action's Variables-by-ref exposure — **closed by deletion**. Their low was a note about the inner Signature struct's surface — **closed by deletion**. The deletion *over*-resolves security's pre-merge concerns. Agree.

## Verdict

**PASS.** Auditor v1's pre-merge scope (A1 + A3) is closed:
- A1 by deleting the foot-gun outright (stronger than the proposed rename + obsolete-attribute path).
- A3 by `using` + `ResolveEncoding()` + regression test.

Deferred items (A4 = Variables.Set dot-path overlay routing, A5 = PlangDataSerializer caps) carry forward to the parallel-foreach and Stage 9 transport branches respectively. A2 is moot.

## Next bot

**docs.** No code changes needed. The docs bot should:
1. Update `cool.md` to label the cross-device migration line clearly as future/roadmap (or move it).
2. If any architecture doc (Stage 9 plan, channel surface doc) describes `migrate`/`MigrationEnvelope` as a current API, update it.
3. Pick up any pending CLAUDE.md proposals on this branch.
