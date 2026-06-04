# auditor — lazy-deserialize — summary

**Version:** v1
**Verdict:** NEEDS WORK
**HEAD:** `ca6e2fb7c`

## What this is

Cross-check of codeanalyzer v2 PASS, tester v3 PASS, and security v1 PASS on the
`lazy-deserialize` branch — and an independent trace of error propagation across the
new lazy-materialization seams.

## Findings

- **F1 (Major)** — `MaterializeFailed` error stamped on a raw-backed Data is dropped by
  every navigation entry that triggers materialization. `GetChildValue` and
  `SetValueOnObjectByPath` return a fresh `NotFound` Data with no error, so a developer
  navigating a malformed JSON sees "not found" instead of the parse error. Zero test
  coverage. Cross-instance smell: error on `this`, return value is a different Data.
- **F2 (Minor)** — Codeanalyzer's deferred `Materialize`/`Materialise` rename has no
  todo/handoff artifact. Either rename now or file the entry.
- **F3 (Info)** — Tester's missing `variable.set` List-arm regression test is real and
  benign; add the symmetric goal test.
- **F4 (Info)** — Security's catch-all silencing concern is the same site as F1; fixing
  F1 resolves it.

## What the upstream trio missed

All three bots passed on the same HEAD, but none traced the `Materialize()` failure path
across instances. Codeanalyzer audited shape; tester audited suite quality; security
audited the lazy↔signing boundary. Cross-file error propagation is auditor's lane and
was the gap.

## Next bot

**coder** — surface `MaterializeFailed` at the navigation seam, add a goal regression
test (`%cfg.host%` on malformed JSON → `error.key == "MaterializeFailed"`), and either
rename `Materialise/Materialize` or file the F2 todo before merge.

## Files

- `.bot/lazy-deserialize/auditor/v1/report.md`
- `.bot/lazy-deserialize/auditor/summary.md` (this)
