# tester v3 — set-add-order at d98920480 (production final at d30db6c03; coder v3 tests at 37b2995fa) — PASS

Patterns: 12 (one answer, on the element: `Part` read by `Certain`), 11 (worked out at the listing's ask), 7 (`Settled` reused).

## Counts (no red names; branch touches 3 files: pick/list/this.cs, ModulePartTests.cs, pick_golden.json)
- C# `./dev.sh test`: Modules 1500, Types 1049, Wire 647 (641 on runtime2 + the 6 ModulePartTests; 3 were there at the first look), Data 882, Generator 191, Runtime 1513 — all green. Only Wire changes with this branch's files, so the others are the runtime2 numbers.
- Decider fixtures by class: ModulePartTests 6/6, PickListTests 8/8, ConfirmTemplateTests 2/2, LineTwinTests 1/1, PickOptionTests 18/18, KeptStepTests 8/8.
- plang `--test` (from `test/`, after `./dev.sh build`): 323 total, 322 pass, 0 fail, 0 stale, 1 skipped (runtime2's skip). Tree clean after.
- No baseline file on this branch (a green runtime2 needs none); no new red names.

## The moved picks (pick_golden.json, 4 lines = 3 picks + one prompt line)
checkout [3] and [5] (`if …, throw …`): error.throw 0.95 (module error 0.48 / 0.52) Possible → Certain, and the starting line now holds `condition.if(Left) { error.throw() }`. Right: the step throws; condition's share was the settled one. Loop step (`foreach …, call Ship`): goal.call, loop settled by loop.foreach. Right.

## Mutations on `Part` / `Certain` (announced; each reverted; `git status` clean)
| # | mutation | caught by |
|---|---|---|
| A | settled shares not subtracted (`left` stays 1) | `ASetOfASum_IsCertainOfTheAdd_AndAddsFirst`; `PickListTests.ThePromptCUserMessage_IsThePinnedOne` |
| B | divide by the open modules' sum (the v1 bug) | `ARemoveAnsweredWithoutProbabilities_StaysPossible` |
| C | `Certain` always true | both remove tests; `ThePromptCUserMessage_IsThePinnedOne` |
| D | threshold 0.5 instead of Near | both remove tests; `ThePromptCUserMessage_IsThePinnedOne` |
| E | "nothing left" returns 1 instead of 0 | **not caught** |

E is an equivalent mutant on any stage 1 whose shares sum to at most 1: whatever the settled modules leave is the open modules' share, positive for any module that gets asked, so `left <= 0` fires only on a malformed stage 1 (shares over 1). It is a defensive guard; the plan's "never a division by zero" holds by reading, unpinned by test.

## Plan cases vs tests
- the bug ✓ (`ASetOfASum…`), the guard ✓ (`ARemoveUnsureOfItsModule…`), the guard without probabilities ✓, the fixtures ✓.
- `set %x% = 5` (plan's third row, "as today"): no `Part` test; `AVariablesPlace_EndsWhereItsTextEnds` reads the known set, not the Mark. Unchanged path, no risk found.
- `Part`'s `Ranked().Single(...)`: a module asked is always in `Ranked()` (Asked takes from it), so no throw; no test needed.

## Builder's measurement
`.bot/set-add-order/builder/v1/result.md` (set+add 5/5, Mark prints 3 5/5, `remove %x%, write to %y%` 3/3) was taken on **f3d89adf6**, the open-sum `Part`, not on the final d30db6c03. The two formulas agree when stage 1 gives probabilities (the set+add case: 0.67 / (1 − 0.33) = 0.67 / 0.67); they differ only without probabilities, where the final one is the stricter. So the 5/5 stands by arithmetic, but the final binary was not measured. The `remove %x%` alone miss (file.delete 5/5, module file 0.94) is a decider answer, not this change; the builder reported it.
