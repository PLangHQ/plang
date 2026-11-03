# set-add-order

Traces to: the backlog's "before plang 2.0b" item (event-action 79), the builder's trace (2026-11-03), Ingi: "yes fix that bug … just take it all the way through". Decisions: `.bot/set-add-order/architect/summary.md`.

## Why

`set %x% = %x% + 1` is the most common step in plang, and a fresh build gets it wrong 4 times in 5: `variable.set(Name=%x%, Value=%!data%); math.add(A=%x%, B=1)`. The set runs first and keeps the previous step's value; the sum is thrown away. The same bug makes `doc/events.md`'s Mark example print `marks: 2` instead of 3 (`doc/verify.md` row 68): Mark's one step is `set %marks% = %marks% + 1`.

## The cause (5 whys, read in the code)

1. The writer keeps the starting line's order and appends what the line lacks. The line held only `variable.set(Value, Name=%x%)`.
2. The starting line holds only the Certain actions (`goal/step/pick/list/this.cs:314`, Prefill). math.add was marked Possible.
3. An action picked through a module is Certain only if its score is at least Near (0.9) **and** `Sure(name)` holds (`:290–291, :298`). `Sure` asks for the module's stage-1 share to be at least 0.9.
4. Stage 1's module question is the step's *main work*, a one-of choice whose shares sum to 1. A step with two modules splits it: math 0.67, variable 0.33. Stage 2 asked "does the step use math?" (`@also.math` 0.98) and math.add's pick carries that score (`:544`), but `Sure` still reads the split share.
5. `Sure` came from fb14118e0 (2026-09-30): `remove %x%` pre-filled file.delete, because the yes/no said "uses file 0.99" while the shares were file 0.56, variable 0.36. The share guards against a yes/no that says yes too easily. But it can't tell "unsure which module" (remove) from "two modules" (set + add).

The ordering is not the bug: once math.add is on the line, the keep follows it with `Value=%!data%` (`goal/step/action/keep/this.cs:33`, whose own doc names this step).

## The fix

**Pattern 12 (one question, one answer, on the element):** the question `Sure` asks is "is the decider sure this module is in the step?" Stage 1's split mass on a module that a certain common action already answers is not doubt about the other module. That module is in the step already. So a module's certainty is **its share of the work no certain common action answers**: its stage-1 share divided by 1 less the shares of the `Settled` modules in `Ranked()` (`:498`, the set `Asked` already uses), and 0 when nothing is left. Mass no module claims stays as doubt, so a stage 1 that answers without probabilities (one module, its confidence) keeps its plain share (summary 10).

| step | stage 1 | settled | the module's share of the open work | today | after |
|---|---|---|---|---|---|
| `set %x% = %x% + 1` | math 0.67, variable 0.33; variable.set 0.98 | variable | math 0.67 / (1 − 0.33) = 1.0 | math.add Possible | **Certain**: `math.add(…); variable.set(Name=%x%, Value=%!data%)` |
| `remove %x%` | file 0.56, variable 0.36 (or file 0.56 alone, no probabilities) | none | file 0.56 / 1 = 0.56 | file.delete Possible | Possible (unchanged) |
| `set %x% = 5` | variable 0.95; variable.set 0.98 | variable | none asked | as today | as today |

- One private member on the pick list answers the module's share of the open work. `Sure` reads it; nothing else changes. No new threshold, no new question to the decider, no prompt text change.
- The prompt's "(module math 0.67, …)" beside a picked action stays stage 1's answer as given (it reports what the decider said; it isn't the judgement).
- Nothing left (the settled shares sum to 1 or more): the share is 0 (unsure), never a division by zero.
- Boy scout (codeanalyzer 3): `AssignsOne`'s `%\S+%` arm and `Placed()`'s IndexOf scan read through the variable parser.
- The builder's other option (sure when the main module's `@also` confirms) is not taken: it is the 09-30 bug again (file was main, `@also.file` 0.99).
- Known edge, for the builder's measurement: a settled module's stage-1 mass might be for another of its own actions (`remove %x%, write to %y%`: variable settled by the capture, while variable's share was for variable.remove). It's rare; the builder probes it once.

## Stages

One stage.

1. **The module's share of the open work.** The member above, read by `Sure`. Boy scout on `pick/list/this.cs` (the codeanalyzer reads it whole). The local function `Sure` shares its name with the const `Sure` (0.8, `:30`) and is renamed for what it answers.

## Tests (each red without the change)

- **C#, the bug:** stage-1 and stage-2 answers as recorded for `set %x% = %x% + 1` (variable.set 0.98; @module math 0.67, variable 0.33; @also.math 0.98; math → add). math.add is listed Certain, and the starting line is `math.add(…); variable.set(…Value=%!data%)` in that order.
- **C#, the guard stays:** `remove %x%` (file 0.56, variable 0.36; @also.file 0.99; file → delete). file.delete is still Possible, and the starting line doesn't hold it. The same with stage 1 answering without probabilities (file 0.56 alone): red on f3d89adf6.
- **The decider fixtures:** `PickListTests`, `ConfirmTemplateTests`, `LineTwinTests`, `PickOptionTests`, `KeptStepTests`. A pin that moves is re-pinned through the Explicit `AcceptTheFixture`, and each moved pick is read and listed in the report (which step, Possible → Certain, why it's right).
- **The builder, not a test (reported):** fresh builds, cache skipped, against the new binary. `set %x% = %x% + 1` builds add-then-set 5/5. `remove %x%` builds variable.remove (09-30: 5/6). The edge `remove %x%, write to %y%` is probed once. `doc/events.md`'s Mark example is built and run, and its output is reported to docs.

## Demolition

Nothing is removed. **Stays:** the 09-30 guard (refined, not dropped), `@also`, the prompt's shares, Prefill and the keep's placement.

## Not in this branch

- Asking stage 1 a many-of module question instead of the one-of main work (a decider redesign).
- The writer ignoring the math.add note ("a variable.set after it keeps the sum") when the line lacks math.add: with the fix, the line has it.

## OBP

| piece | pattern | check |
|---|---|---|
| a module's share of the open work | 12 one answer | one member, read by `Sure`; no second door beside it |
| when it's worked out | 11 lazy | at the listing's ask, from what stages 1 and 2 answered; nothing kept |
| `Settled` reused | 7 one door | the set `Asked` already uses; not a second notion of settled |

## Collision check

Decisions 1–10 (summary.md) read against each other and against fb14118e0's rule: 4 refines 09-30's guard, and 10 replaces 4's arithmetic (written in the Replaced table). This file says only what holds.
