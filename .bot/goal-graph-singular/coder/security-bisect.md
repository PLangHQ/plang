# Security regressions — bisect record

## PARKED (Ingi, 2026-09-23) — state to resume from

Both searches were stopped after one probe each. Everything needed to resume is committed here:

- Oracle: `bisect-oracle.sh` (use `REAL_SIGNING=1` for the tamper pair — see Finding 1).
- Candidate lists (oldest first, last line = the branch head `3e87c6d3b`, known BAD):
  `tamper2_hashes.txt` (46 candidates in `da067599c..3e87c6d3b` + head), `mask_hashes.txt` (35 in
  `da067599c^..3e87c6d3b` + head). Built by `narrow.py tamper2|mask hashes` (the path patterns live in it).
- Driver: `bisect_cands_wt.sh <cands> <log>` with `WT=<worktree>` (binary search; parent re-probe at the end).
  Its first run wrote the logs INSIDE the worktree (relative path; now fixed to absolute).
- Worktrees: `/tmp/rt2` (tamper), `/tmp/rt3` (masking) — ephemeral; recreate with `git worktree add --detach`.

| search | range (candidate lines) | probed | verdict | remaining |
|---|---|---|---|---|
| tamper (`REAL_SIGNING=1`) | lines 1–47 | line 24 `0a003d302` | GOOD | first BAD is in lines 25–47 (23 → ~5 probes) |
| masking | lines 1–36 | line 18 `bb9e9d02f` | GOOD | first BAD is in lines 19–36 (18 → ~5 probes) |

Resume: trim each list to its remaining lines (the line before is known GOOD) and run the driver, e.g.
`tail -n +25 tamper2_hashes.txt > t.txt; REAL_SIGNING=1 WT=/tmp/rt2 ./bisect_cands_wt.sh t.txt tamper.log`
and `tail -n +19 mask_hashes.txt > m.txt; WT=/tmp/rt3 TESTS=AssertionError_Message_MasksSensitiveViaDiagnosticOutput ./bisect_cands_wt.sh m.txt mask.log`.
Each probe is a full wipe + rebuild (~4 min).

### Open items (rulings pending execution, after the culprits are recorded)

1. **Blast radius** — listed below (§Blast radius). Re-check the list once the mock is honest.
2. **Honest mock** — `TestSigning` signs with a fixed test key / deterministic content hash and verify recomputes
   and compares, so a tampered value fails exactly as with real signing; `TestApp.Create` stays the default;
   tests exercising real key handling use `TestApp.Plain`. Own commit; re-run the listed tests by name.
3. **Tamper tests** need real (or honest) signing once fixed — today they cannot fail through `TestApp.Create`.

Oracle: `.bot/goal-graph-singular/coder/bisect-oracle.sh` (full bin/obj wipe per checkout; library first;
a suite that does not compile at a commit is skipped, and a named test whose suite did not build is 125).
Worktree: `/tmp/rt2`. A culprit is confirmed only by parent-passes / commit-fails.

## Tamper pair

`Cut4_TamperingPropertyValue_FailsOuterSignatureVerify`, `OuterSignature_AfterPropertiesValueTamper_FailsVerify`
— tamper a property value on the wire, deserialize, `signing.verify` must fail. At HEAD it succeeds.

### Finding 1 — test-infrastructure flip (rule 1: a flip on a test rewrite, reported separately)

**`6071d0f13` "TestSigning mock — no-crypto signing for tests"** — `TestApp.Create` registers a signing mock
whose verify ALWAYS succeeds (its own message: "verify always succeeds"). Both tamper tests build their App
through `TestApp.Create`, so from this commit on they cannot fail whatever production does.

| commit | oracle (mock, as the tests are written) | oracle `REAL_SIGNING=1` |
|---|---|---|
| `694213d76` (parent) | PASS | — |
| `6071d0f13` | **FAIL** | PASS |

Confirmed: parent passes, commit fails; with real signing the commit passes → production was fine here.
Consequence: the previously measured range (`0ea5a4b94..da067599c^`) was an artefact of the mock. Every
measurement since must use real signing — the oracle's `REAL_SIGNING=1` removes the mock install from the
measured worktree's `PLang.Tests/Shared/TestApp.cs` (both historical shapes).

The tests themselves still need real signing when fixed: at HEAD both fail with `TestApp.Plain` too.

### Finding 2 — the production regression (real signing)

| commit | `REAL_SIGNING=1` |
|---|---|
| `da067599c^` (`4f1dfaf9c`) | PASS |
| `da067599c` "no-verify flag for nested reconstruction" | PASS — cleared |
| HEAD (`3e87c6d3b`, via `TestApp.Plain` in the main tree) | FAIL |

Range: `da067599c..HEAD` (1046 commits) → 46 candidates touching signing / verify / the wire writer /
Properties / canonical form / Transport / Output / the data reader / ReadContext / the two test files.
Binary search in progress.

### Blast radius of the mock (ruling 1)

`PLang.Tests/Shared/TestSigning.cs`: `VerifyAsync` and `Verify` return `true` unconditionally. Scan: every
test file that builds its App through `TestApp.Create` and touches signing/verify, filtered to tests
asserting a verification FAILURE (`.bot`-scratch script `verify_blast.py`, then read by hand):

| test | today | how it can still fail under the mock |
|---|---|---|
| `Wire/…/IntegrationCuts/Cut4_PropertiesWireTests::Cut4_TamperingPropertyValue_FailsOuterSignatureVerify` | RED | — (the regression) |
| `Wire/…/PropertiesWireShapeTests::OuterSignature_AfterPropertiesValueTamper_FailsVerify` | RED | — (the regression) |
| `Wire/…/IntegrationCuts/Cut2_SignThenCompressTests::Cut2_TamperingValueByte_FailsOuterSignatureVerify` | green | the verify action's own data-hash comparison, not `ISigning.Verify` |
| `Wire/…/FailureMatrixTests::SigningVerify_AfterWireByteTamper_ReturnsDataHashMismatch` | green | same — `DataHashMismatch` |

The green pair is green for a reason the mock does not reach — but only for tampers the data hash covers;
any failure that needs `ISigning.Verify` itself (wrong signer, forged signature bytes) cannot surface
through `TestApp.Create`. Not affected: the signing module's own suites (`Modules/…/signing/*Tests`) build
a plain `app.@this` (real Ed25519). Read by hand and excluded: `Stage6_EntryPointWiringTests::ChannelsVerify_*`
(channel wiring, not signatures) and the wire-shape field-count tests (no failure asserted).

Note the pattern: a tampered VALUE byte is caught; a tampered PROPERTY value is not — consistent with
properties falling outside the hashed/signed bytes. The binary search decides.

## Masking

`AssertionError_Message_MasksSensitiveViaDiagnosticOutput` — confirmed FAILING at HEAD (the message carries
`topsecret-PLAINTEXT-777`). Range `da067599c^..HEAD`; narrowing after the tamper culprit.
