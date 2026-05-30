# Auditor v2 — plan

**Trigger:** security v1 pushed `179121964` after I ran. v1 missed it because I
didn't fetch. Capturing security's findings + the new pipeline-state rule.

## Tasks

1. Read `security/v1/summary.md`. Verify their PASS verdict + 3 low findings.
2. Cross-reference security's findings to my v1 findings — overlap means
   independent confirmation of the same load-bearing invariants.
3. Re-issue verdict: still PASS (security agreed), but retract v1's F1
   ("no security review"). Net findings tighten.
4. Update report.json + summary.md; next bot is docs (security said so too).

## Process change (already done)

- Saved feedback memory `feedback-upstream-bots-required`.
- Proposed character change in `.bot/singular-namespaces/claude-md-proposals.md`.
