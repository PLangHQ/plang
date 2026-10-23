# item-doors audit: brief

## Why

Ingi froze item-doors on 2026-10-20. His words: "seems like everything is breaking apart. Lot of bullshit seem to have happen. Lot of breaking of obp I feel like. You making decisions with out tracing the code, asking the 5 whys and just deciding to fix at the spot. I really dont know what to do. If we should just throw away this branch and start over."

He will decide from this audit, stage by stage: keep, fix, or revert. If the core shapes are wrong, he will start over from runtime2.

Two reviewers work independently:
- **codeanalyzer:** OBP shape;
- **auditor:** design, correctness, and whether what was built is what the plan asked for.

Neither reads the other's report before finishing its own. The architect made most of the rulings under review, so the architect's log is a list of claims to check, not a source of truth.

## Scope

The branch's production diff against its parent:

```bash
base=$(git merge-base origin/item-doors origin/runtime2)   # 1adfb9555
git diff $base origin/item-doors -- PLang PlangConsole PLang.Generators
git log --oneline $base..origin/item-doors -- PLang PlangConsole PLang.Generators   # 84 commits
```

About 380 production files changed (+4.4k / −3.7k lines). Tests count only where they pin a shape: the surface golden and the generator diagnostics.

## What to read

- The plan: `test/plan/item-doors/start.md` (its stages), with `start.goal`.
- The architect's log: `.bot/item-doors/architect/summary.md`, decisions 1–159, newest first. Each decision that changed code names its commit.
- The rules:
  - the OBP smells in `CLAUDE.md`;
  - `Documentation/v0.2/obp-smells.md`;
  - `Documentation/v0.2/object_pattern_formal.md`.

## How

- Group the production changes by stage: 1, 1b, 2, 3, 4, 5, the born-whole families, and the fixes outside the stages.
- For each change, say what it is, then check it against the smells by their catalog names.
- **For each finding, trace the why-chain up the tree to its cause, with file:line at each step.** Ingi: "the problem is usually not where the issue is, it is usually few steps away, think the 5whys in Lean". A finding without a chain in the code doesn't count.
- Check that each code-changing decision in the log matches what was built. Name every decision whose rule the code breaks.
- Read only. Fix nothing.

Ingi has questioned these areas. Judge them on their merits; neither his view nor the architect's is a given:
- stage 2's pairs and the door: `writer.Kind.pair.Output(...)`, the pair classes under `*/format/`, PLNG006;
- the wire, a lazy `.pr` value: `wire.Decoded(context)` beside `source.Value(data)`.

## What to write

`.bot/item-doors/<your bot>/audit.md`:
1. **At the top, in plain words:** is the branch worth keeping, and why.
2. **A table, one row per stage:** stage, verdict (keep / fix at the root / revert), and its top findings.
3. **The findings,** each with its why-chain and file:line references.

Commit and push only `.bot/`.

```plang
Audit
/ read the plan, the log and the OBP rules; the log is claims to check, not truth
- read test/plan/item-doors/start.md, the architect's log and the smell catalog
/ the base is item-doors' merge-base with runtime2, 1adfb9555
- list the branch's production diff, grouped by stage
- for each change, write what it is and check it against the named smells
/ a finding counts only with its chain: file:line at each step, up to the cause
- for each finding, trace why up the tree to its cause
- check each code-changing decision in the log against what was built
- give each stage a verdict: keep, fix at the root, or revert
- write .bot/item-doors/%bot%/audit.md, plain summary first, then the table, then the findings
- commit and push .bot only
```
