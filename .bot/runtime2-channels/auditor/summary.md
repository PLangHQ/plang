# auditor — runtime2-channels

## v1 (2026-05-07) — PASS with conditions

Independent audit on coder v8 + codeanalyzer v4 PASS + tester v7 PASS state (`38f9d153`). No security pass had run on this branch — the auditor here also lifts the wire-surface concerns that would normally be a security input.

**Baseline (clean rebuild):** C# 2762/2762, PLang 201/201.

**Verdict:** PASS — branch is mergeable for the stated stub scope (Stage 9 produce-side, no receive transport). All prior codeanalyzer findings (F1/F4/F5/F6 + B1/L1) confirmed closed. Five new findings:

| ID | Severity | Summary |
|---|---|---|
| A1 | High (latent) | `MigrationEnvelope.Signature` doesn't cover Payload/Config; doc claims it does. |
| A2 | Medium | `migrate` action exposes `Variables.Snapshot()` to user code, no permission gate, by-ref. |
| A3 | Low (real bug) | `Stream.AskCore` leaks StreamReader across calls + ignores configured Encoding (F5 missed this site). |
| A4 | Note | `Variables.Set` dot-path branch bypasses `Calls.Current` — overlay isolation incomplete. |
| A5 | Note | `PlangDataSerializer` lacks MaxBytes/MaxDepth (S-F3 carry-over from runtime2-callback). |

**Recommended pre-merge:** A1 doc fix (or `[Obsolete]` on `VerifyEnvelope`) and A3 one-liner. A2 belongs with the transport work; A4 with parallel-foreach; A5 with Stage 9 transport.

Report: `auditor/v1/report.md`. Verdict JSON: `auditor/v1/verdict.json`.
