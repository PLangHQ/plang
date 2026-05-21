# codeanalyzer v4 — plan

## Scope

Review the coder work landed since codeanalyzer v3 (PASS, `863c7c972`):

- **coder v4** (`528490b8b`) — closed 9 tester-v3 test-quality findings; deleted 6
  intent-only `Tests/Permission/*` placeholder goal dirs.
- **coder v5** (`ccaf95bb0`) — dropped `PermissionRecord.AppId`; persistent grants
  now keyed `(Actor + Path + Verb)`.
- **runtime2 merge** (`0b4ff9cc1`) — app-lowercase rename, 63 conflicts.
- **coder v6** (`894d6a0ca`) — closed auditor F-A (persistent-grant durability via
  `SkipFreshnessCheck`) and F-B (the merge).

The auditor FAILed at v5 (F-A, F-B). v6 closes both. F-C/D/E and security
F1/F2/F4 are explicitly deferred follow-ups.

## Passes

1. **OBP** — v5 removes a record field; v6 adds a flag + conditional branches.
   No new collections, no cross-file locking. Check shape stays clean.
2. **Simplification** — `SkipFreshnessCheck` plumbing: flag, two `if (!skip)`
   guards in Ed25519, one call site. Check nothing is over-built.
3. **Readability** — comment volume on the new security-relevant branches.
4. **Behavioral** — does `SkipFreshnessCheck` skip exactly the right checks?
   Any *other* `signing.verify` caller that should pass it? Does step 3
   (Expires) still run? Does the default-false keep wire verification intact?
5. **Deletion test** — every line of the fix earns its place.

## Method

- Diff v4/v5/v6 against v3 baseline; read current state of the four touched
  files (`actor/permission/this.cs`, `filesystem/permission/this.cs`,
  `modules/signing/verify.cs`, `modules/signing/code/Ed25519.cs`).
- Clean rebuild to confirm the merge + v6 compile (stale-binary trap).
- Confirm the Scenario4 regression test is real (advances NowUtc), not
  false-green — the specific defect the auditor flagged.

## Not in scope

- Re-verifying the 63-conflict merge mechanically (auditor/tester domain).
- Re-litigating deferred F-C/D/E and security F1/F2/F4 — tracked follow-ups.
