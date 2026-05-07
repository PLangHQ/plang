# auditor — runtime2-channels

## v1 (2026-05-07) — FAIL → coder

Independent audit on coder v8 + codeanalyzer v4 PASS + tester v7 PASS state (`38f9d153`). No security pass had run on this branch — the auditor here also lifts the wire-surface concerns that would normally be a security input.

**Baseline (clean rebuild):** C# 2762/2762, PLang 201/201.

**Verdict: FAIL.** Two findings need pre-merge work (A1 doc/code mismatch + A3 real `AskCore` bug). All prior codeanalyzer findings (F1/F4/F5/F6 + B1/L1) confirmed closed.

**Next bot: coder.** After A1 + A3 land, re-run auditor for the close-out pass.

| ID | Severity | Summary | Disposition |
|---|---|---|---|
| A1 | High (latent) | `MigrationEnvelope.Signature` doesn't cover Payload/Config; doc claims it does. | Pre-merge — coder. |
| A3 | Low (real bug) | `Stream.AskCore` leaks StreamReader across calls + ignores configured Encoding (F5 missed this site). | Pre-merge — coder. |
| A2 | Medium | `migrate` action exposes `Variables.Snapshot()` to user code, no permission gate, by-ref. | Deferred — bundle with Stage 9 transport. |
| A4 | Note | `Variables.Set` dot-path branch bypasses `Calls.Current` — overlay isolation incomplete. | Deferred — bundle with parallel-foreach. |
| A5 | Note | `PlangDataSerializer` lacks MaxBytes/MaxDepth (S-F3 carry-over from runtime2-callback). | Deferred — bundle with Stage 9 transport. |

Report: `auditor/v1/report.md`. Verdict JSON: `auditor/v1/verdict.json`.
