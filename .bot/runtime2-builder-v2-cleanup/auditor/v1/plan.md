# Auditor v1 Plan — runtime2-builder-v2-cleanup

## Context
Large cleanup branch (~30K lines, 244+ files) consolidating pieces 1-4 (identity, crypto, signing, HTTP) plus engine-wide cleanup. Three bots already reviewed: codeanalyzer (PASS), tester (PASS after fix), security (PASS with 5 findings, 2 medium open).

## Audit Approach

1. **Read all previous bot reports** — understand what was checked and what was concluded
2. **Cross-file contract audit** — the gaps between reviewers:
   - Settings→Config rename completeness
   - Data type hierarchy + clone family
   - Library→Module rename completeness
   - Provider pattern consistency
3. **Verify open security findings** — confirm they're real and assess if they block merge
4. **Challenge previous verdicts** — are the tester's tests adequate? Did codeanalyzer miss cross-file issues?
5. **Write verdict** — pass/fail based on findings

## Focused Areas
- Variables.Clone() + Data.Clone() — clone family completeness with new Data subclasses
- DefaultEvaluator catch filter — security finding #2
- Data.Envelope.Decompress catch blocks — security finding #1
- Rename completeness (Settings→Config, Library→Module)
- DataList<T> as new stateful Data subclass
