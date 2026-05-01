# tester — runtime2-data-share-state

Cross-version progression on this branch.

## v1 (2026-04-30) — approved

C# 2524/2533 (9 honest stubs failing for deferred Phase 5c/6). PLang 170/173 (2 fails are stale .pr files in legacy `tests/modifiers/`, NOT coder regressions). Identity-preservation contract pinned via `ReferenceEquals` everywhere it counts. 4 major + 3 minor findings — all coverage gaps, no false greens. Major gaps: `MintTyped` cold-type if-chain (decimal/float/DateTimeOffset/Guid/byte[] uncovered), `list.add` complex-snapshot path entirely uncovered (= codeanalyzer/v2's behavioral concern unprotected), `Variables.Set` dot-path JsonElement-vs-Dictionary unprotected, set.cs error paths weak. Recommendation: approve-with-deferred-coverage → auditor → merge. See [v1/summary.md](v1/summary.md).

## v2 (2026-04-30) — approved

Reviewing coder/v2 (`24cba238`) on top of codeanalyzer/v3 PASS at `ae827527`. Coder v2 was a separate scope from v1 findings — fixes for the LLM-builder NRE: nested `%var%` walk asymmetry between AsCanonical and AsT_Impl, and `JsonNode` dispatch gap in `TypeConverter`. C# 2530/2539 (same 9 deferred stubs). PLang 166/166 (lowercase `tests/modifiers/` deleted, closing v1 finding #7). New code 100% line-covered. Deletion-tested all 6 new tests: 5 of 6 production changes are properly pinned. **1 major false-green confirmed** (codeanalyzer/v3 predicted): the four state-aliasing lines on AsCanonical's container-walk transient (L491–494) are unasserted — removing them, all 14 AsTIdentityTests pass green. 2 minor: same shape on the partial-interp branch (pre-existing); Rule 4f (LiteralList) doesn't actually pin the walk. Carryover: 6 of 7 v1 findings still open (out of v2's scope). Recommendation: → auditor → merge, or bounce to coder/v3 if Ingi wants v1 coverage closed first. See [v2/summary.md](v2/summary.md).
