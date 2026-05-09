# auditor v1 — runtime2-cleanup

## Scope

107-commit, 27-stage OBP cleanup branch. Three reviewers already passed:
- **codeanalyzer v3** — PASS, 3 non-blockers, ran both test suites.
- **security v1** — PASS, 2 low (carry-over from codeanalyzer), no crypto/trust regressions.
- **tester** — no report on file. Gap.

## Approach

Don't redo the file-level OBP sweep codeanalyzer did. Don't redo crypto/trust pass security did. Look in the seams:

1. **Cross-file contract gaps** — same logical thing stored multiple places (Q3 OBP smell). Three `JsonSerializerOptions` bags scattered (`CaseInsensitiveRead`, `CamelCaseIndented`) with deliberate "per-consumer ownership" — verify the trade-off holds and the test facade doesn't paper over drift.
2. **Architectural fit** — Tier 5 made several static-vs-instance judgment calls (Diagnostics, Conversion, http defaults). Verify these are consistent with the `@this` convention and don't dilute its signal.
3. **Process gap** — no tester review. ~111 test sites still go through `TypeMappingTestFacade` (a compatibility shim). Determine whether the facade routes to production homes (legitimate) or locally re-creates them (drift risk).
4. **Anti-thematic carry-overs** — codeanalyzer v3-2 (Console.Out.Write in test/report.cs) accepted as "non-blocker", security accepted as "accepted-risk". A cleanup branch whose own thesis is channel discipline should fix this pre-merge, not defer.
5. **Verification** — clean rebuild + both test suites, confirm 2752 + 199 green.

## Out of scope

- Re-doing per-file OBP shape checks (codeanalyzer v3 covered the 6 backbone files + Tier 5 stages 23-27).
- Crypto/trust/serialization-boundary review (security v1 covered).
- Architect's planned-vs-delivered audit (results.md is internally consistent and matches the tree).
