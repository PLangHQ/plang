# set-add-order — the architect's log

Plan: `.bot/set-add-order/architect/plan/start.md`.

## Replaced decisions

| replaced | by | what changed |
|---|---|---|
| — | — | none |

## 2026-11-03 — the cause, and the plan

`set %x% = %x% + 1` builds set-before-add 4 times in 5, because `Sure` marks math.add Possible: the module's one-of main-work share is 0.67, split with variable, which variable.set already answers. The fix: a module's certainty is its share of the work no certain common action answers. Ingi: "yes fix that bug … just take it all the way through, you can orchistrate all the bots … unless something surprising comes up then ask me".

| # | decision | why | who |
|---|---|---|---|
| 1 | Branch `set-add-order` from runtime2 366c06e8c (+ the backlog note 2d22f1588) | Ingi: "yes fix that bug" | Ingi |
| 2 | The cause is `Sure` (`goal/step/pick/list/this.cs:290–291`), not the line's order | the keep already follows a producer with `Value=%!data%` (`action/keep/this.cs:33`) | builder's trace, read by the architect |
| 3 | Not "sure when the main module's `@also` confirms" | that is fb14118e0's `remove %x%` bug again (file main, `@also.file` 0.99) | architect |
| 4 | A module's certainty is its stage-1 share of the open work: divided by the summed shares of the `Ranked()` modules not `Settled`; 0 when nothing is open. One private member, read by `Sure`; the prompt's shares stay as stage 1 gave them | pattern 12; refines the 09-30 guard, keeps `remove %x%` unsure (≈0.61) | architect, Ingi's go |
| 5 | The line: coder → codeanalyzer → tester (with the builder's builds) → security → auditor → docs & merge, run by the architect | Ingi: "take it all the way through" | Ingi |

| 6 | **Accepted** f3d89adf6: private `Part(module)` (open = `Ranked()` minus `Settled`; 0 when the module isn't open or the sum is 0), read by the local `Certain` (was `Sure`, which shadowed the const). Tests `ModulePartTests` (the set-of-a-sum red on revert; the remove guard green). `pick_golden.json` re-pinned, 3 prompt C lines, each Possible → Certain and right: checkout s3 and s5 (if + throw; condition settled by condition.if; the two picks fb14118e0 lost) and loop s2 (foreach + call; loop settled). Gate green, plang 322/323 with 1 skipped, PLNG 0 | the plan held | coder |
| 7 | The builder measures fresh builds while the codeanalyzer reviews (in parallel): set+add ×5, remove ×5, the edge ×3, the Mark example | independent; saves a round | architect |

Stage status:
| Stage | What | Status |
|---|---|---|
| 1 | The module's share of the open work | in review (f3d89adf6) |
