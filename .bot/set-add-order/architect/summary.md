# set-add-order — the architect's log

Plan: `.bot/set-add-order/architect/plan/start.md`.

## Replaced decisions

| replaced | by | what changed |
|---|---|---|
| 4's arithmetic (share / the open shares' sum) | 10 | share / (1 − the settled shares): a stage 1 without probabilities made the open sum the module's own share |

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

| 8 | **The builder's measurement** (b2d52c720, binary f3d89adf6): set+add add-then-set 5/5 (was 1/5). A bare `remove %x%` builds file.delete 5/5, but stage 1 gives file 0.94 and nothing is asked through, so the fix isn't the cause (both rules say Certain); it goes on the backlog. The edge `remove %x%, write to %y%` is the same (file.delete; variable.set) 3/3. The Mark example prints `marks: 3` 5/5, but `before action output.write` builds `%!app.type.step.on.start%`: right for the wrong reason. On the backlog; the builder traces it read only, and the fix waits for Ingi. doc/events.md stays without output | measured | builder |
| 9 | **Codeanalyzer v1 NEEDS WORK** (b172d5123). Ruled: **1 (blocking) fix**: `Part` divides by 1 less the settled modules' shares, not by the open sum (stage 1 without probabilities gave file share/share = 1.0, Certain; probed), with a no-probabilities test red on f3d89adf6. **2 with 1** (the `open` list's unreachable guard goes). **3 fix in branch** (boy scout: `AssignsOne`'s `%\S+%` and `Placed()`'s IndexOf scan go to the parser; anything added to the parser comes to me first). **4 and 5 backlog** (on the one list, not obp-cleanup.md). **6 name stays `Part`**: `Left` reads like condition's `Left` operand, which this file's lines show | rule every finding | architect |
| 10 | Replaces 4's arithmetic: the open work is 1 − the settled shares, not the sum of the open shares; `remove %x%` is 0.56, not ≈0.61 (same verdict) | 9 | architect |

| 11 | **Accepted v2** d30db6c03 (merged 983bc7b38): `Part` = share / (1 − the settled shares), 0 when nothing is left; `Placed()` asks `parser.Read(at)` at each place; `Assigned()` reads a `Literal` or one bare variable spanning the rest; `AssignsOne` is gone; nothing added to the parser. The no-probabilities test is red on f3d89adf6. No pin moved. Gate green, plang 322/323 | the fix holds for every answer stage 1 can give | coder |
| 12 | **The event path's cause** (builder's trace, 483dbb7f4): the writer copies an `action module.name` example only when the step's whole text matches it, called goal's name included. Otherwise it takes the nearest example's path (`step.on.start`, or `app.on.start` with a comma): 0/37 right on other shapes, 3/3 on the example texts. Not the decider (0.98), not on-event-examples' words. Fix: teaching only (varied examples, the rule first); not C# (no matching on step text). To Ingi: this branch or its own | right for the wrong reason must not stand in doc/events.md | builder, architect |

| 13 | **Codeanalyzer v2 NEEDS WORK, narrow** (afce1c940): v1's 1 and 3 fixed; the broken-reference difference doesn't matter (Placed only hints the starting line, which the formal reader re-reads). Ruled: **fix** the three tests pinning v2's rewritten lines (M2 at `:138`, the whole-rest check; M1 at `:114`, the skip past a variable), each red on its mutation. **Backlog**: the parser answering each variable's place (a locked addition) | rule every finding | architect |

| 14 | **Accepted v3** 37b2995fa (tests only). M2 → `ASetOfASum_KnowsTheCapture` (what M2 loses is the capture's `Value=%!data%`; the formal reader already refuses `Value=%x% + 1`). M1 → `ASetOfOneVariable_KnowsItAsTheValue`, `AVariablesPlace_EndsWhereItsTextEnds` (the End shift). The skip side (`%a[%b%]%`) can't be shown through a production door, so it goes with the backlog's parser-place item. The tester started on d30db6c03 in parallel (production code final) | each rewritten line pinned | coder |

| 15 | **Codeanalyzer PASS** on v3 (1de7b8380): M2 and the M1 End shift are caught; the M1 skip alone has no production door (backlog). Security starts beside the tester (the code is final). Its question: can the looser certainty pre-fill a destructive action the words didn't name, when a settled module raises another's part? | the line | architect |

| 16 | **Tester PASS** (e25a46435, on d98920480): the sweep green against runtime2 by name (Wire 641 → 647), plang 322/323 with 1 skipped (runtime2's), mutations A–D caught. E ("nothing left" answers 1) is left: it's equivalent for shares summing to ≤ 1. The builder's set+add on the final binary comes with its F1 run (control) | the line | tester |
| 17 | **Security PASS with F1, Medium** (46caa3172): with variable.set settled, `Part` removes variable's whole share, so file.delete is Certain at file 0.58 / variable 0.36 (probe), and `Agree` (`:217`) then refuses a build without it. Shares alone can't tell this from set+add (math 0.67 / variable 0.33): only the decider knows what variable's share was for. Not taken: a floor on the plain share (it would sit between 0.58 and 0.67), dropping Agree's refusal for this Certain (a fork). Weighing: (A) when removing a settled module's share changes the verdict, stage 2 asks that module's action choice, and the share is removed only if it answers the settled action; (B) ship with the probes pinned if real wordings never reach the window. The builder measures seven wordings first | security's bar | architect |

| 18 | **Security on A:** the argmax alone doesn't close F1 (`remove %x%, write to %y%` really uses remove and set, and a one-of choice may answer set). A reads the choice's **probabilities**: the settled module's share is removed only when its settled action is ≥ Near on its own probability, and a mixed choice falls back to the plain share (fails safe to Possible). A confidently wrong decider is B's residual. The rule: the window is reached in a real wording → A (with probabilities) + the probes; never reached → B, Low accepted risk, the probes as the pin, A on the backlog. Put to Ingi | security's bar | architect, security |

| 19 | **F1 ruled B** (the builder's 74bf815d1): 7 real wordings ×3 on the final binary, none in the window. The closest is `remove %x%, write to %y%` (file 0.90, variable.set 0.84–0.86, nothing settled); the delete/path/file wordings give file 0.98–1.00, and the list remove gives list 0.99. The control `set %x% = %x% + 1`: math 0.52–0.59, lifted by `Part`, add-then-set 3/3, which also closes the tester's note 2. F1 is Low accepted risk, security's `ModulePartSecurityProbeTests` are the pin (merged with the branch), and A (with the choice's probabilities) goes on the backlog. Side finding: `clear %x%, write to %y%` builds an extra output.write (backlog). The auditor starts | the rule in 18 | architect |

Stage status:
| Stage | What | Status |
|---|---|---|
| 1 | The module's share of the open work | auditor |
