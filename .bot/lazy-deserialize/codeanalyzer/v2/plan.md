# codeanalyzer — lazy-deserialize — v2 plan

## Task
Re-review the coder's response to v1 findings. Coder pushed `55037aa32`
("address code-analyzer findings (F1/F3/F5/R1); F2 + ctor-unwrap routed to collections plan").

## What to verify
- **F1** (blocker): did storage become uniform OR is the contract documented + the
  signed-Data-in-collection regression test added and green?
- **F3**: is the `TryStaticResolve<T>` extraction behavior-preserving?
- **F5**: three-strict-kind-checks comment present?
- **R1**: httpbin test disabled in-goal like its siblings?
- **F2**: resolved, or deferred — and is the deferral actually tracked anywhere?
- Re-establish the deterministic baseline (rebuild — `this.cs` changed → stale binary).

## Verdict → v2/report.md + v2/verdict.json.
