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

Stage status:
| Stage | What | Status |
|---|---|---|
| 1 | The module's share of the open work | pending |
