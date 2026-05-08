# OBP Audit

Detection recipes for finding OBP shape smells in existing code. Distinct from the foundational OBP principles (which describe what good shape *is*) — this folder holds the *grep screens, filter pipelines, and worked examples* used to find places where the code drifts from the principles.

## When to use

- **End of a refactor sweep.** After major restructuring (channels, callbacks, runtime2-cleanup, etc.), run the audit screens to surface anything that escaped the explicit work.
- **Onboarding to a region.** Reading code that's new to you? Run the screens scoped to that folder. Hits guide what to read carefully.
- **Investigating a specific smell.** Suspect a static-state issue or a missing collection type? Jump to that rule's section for the screen + filter recipe.

Not for active design or authoring — those live with the principles, in `Documentation/v0.2/good_to_know.md` and `/shared/bots/obp/core.md`. This folder is *find existing problems*, not *avoid creating new ones*.

## Contents

- [`obp-rules.md`](obp-rules.md) — the five architect-sharpened detection rules with quick-screen grep recipes, post-filter pipelines, and today's signal/noise counts on `PLang/App/`.

## How to extend

If a new detection pattern emerges from a stage of cleanup work, add it here:

1. Name it (Rule F, G, ... or pattern-name).
2. State the principle — what shape is wrong.
3. Provide the quick screen — exact grep command, scoped to the relevant tree.
4. Provide the filter recipe — what to discard from the raw hits and why.
5. Today's count — raw hits → real candidates after filtering.
6. Worked example from the codebase — before/after with file:line references.

The principle (what shape is right) belongs in the foundational docs; this folder owns the *finding* of the wrong shape.
