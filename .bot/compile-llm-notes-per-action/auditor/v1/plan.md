# Auditor — compile-llm-notes-per-action — v1 plan

## Why this pass exists

Per-axis bots have all closed:

- test-designer v1 — failing-test contract
- coder v1 → v3 — markdown migration, drift-case .pr files via real builder
- tester v1 (NEEDS-FIXES F1) → v2 (PASS, 3 fresh-cache rounds)
- security v1 — PASS (0 new critical/high/medium/low; trust-boundary unchanged)

The auditor's job is the seam between axes — the things each per-axis bot
naturally scopes out of view. Specifically:

1. Did every stage in the architect plan's "Order of work" actually land?
2. Do the two architect verification checks reproduce on a fresh
   independent build?
3. Are there cross-axis defects — surfaces one axis assumed another
   would cover, that nobody actually covered?

## Method

- Read architect plan §"Order of work" (0..8) and trace each stage to
  concrete files at HEAD.
- Rebuild cleanly from zero (rm -rf bin/obj for all four projects +
  `dotnet build PlangConsole` + `dotnet run --project PLang.Tests`).
  Re-run plang drift cases across 3 fresh-cache rounds independently.
- Spot-check the loader call sites, renderer template, and the orphan
  scan — each per-axis bot looked at one side.
- Diff the carry-forwards from path-polymorphism's auditor v1 to make
  sure none crept back in.

## Out of scope

- Anything per-axis bots already cleared on this branch (drift-case
  pinning, mutation tests, security trust-boundary on the loader).
- Anything explicitly listed under architect's "Out of scope".

Carry-forwards are noted, not re-litigated.
