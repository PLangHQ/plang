# Decider v4 — the runner-up asked, examples beside the common actions, held actions aren't picks

Branch `builder-formal`. The same 5 golden goals, the decider alone, 1 run. The clause stays off.

- Raw: `tools/decider/runs/decider_eval_20260925_213528.json`.
- Requests: `/shared/coder/llm/plang/builder-formal/decider-v4/<goal>/`.

**Every column below is scored against the current golden** (the step's own actions; see item 3). So v1–v3 differ a little from their own reports: v1's two goal.calls at 0.60 / 0.63 are now extras, and v2's and v3's held goal.call misses are gone.

| | v1 (ade8d1cdd) | v2 (d902a4502) | v3 (fc72d3083, clause off) | **v4** |
|---|---|---|---|---|
| stage-1 questions | 2,146 | 406 | 406 | 406 |
| stage-1 request KB (sum; checkout) | 724; 133 | 149; 29 | 150; 29 | 158; 31 |
| stage-1 seconds (sum; slowest) | 8.2; 1.8 | 4.6; 1.0 | 4.7; 1.1 | 4.8; 1.1 |
| stage-1 tokens in / out | 179,471 / 39,408 | 44,973 / 20,436 | 45,343 / 20,436 | 48,523 / 20,436 |
| stage-2 questions (skipped) | 40 (40) | 20 (31) | 41 (30) | 46 (30) |
| stage-2 request KB | 116 | 69 | 90 | 91 |
| stage-2 seconds (sum) | 4.2 | 3.8 | 4.4 | 4.4 |
| stage-2 tokens in / out | 34,005 / 3,161 | 20,324 / 1,912 | 25,805 / 2,603 | 26,038 / 2,714 |
| cut 0.9: steps exact | 39/58 | 35/58 | 38/58 | **39/58** |
| cut 0.9: hit / missed / extra | 67 / 21 / 0 | 64 / 24 / 0 | 68 / 20 / 0 | 69 / 19 / 0 |
| cut 0.5: steps exact | 51/58 | 53/58 | 53/58 | **58/58** |
| cut 0.5: hit / missed / extra | 85 / 3 / 5 | 83 / 5 / 1 | 85 / 3 / 2 | **88 / 0 / 0** |

**At 0.5, every step has exactly its own actions.** Nothing ≥ 0.9 is wrong in any version. One run: the 0.5 result needs repeats before it counts as a property of the decider.

## The four items

1. **The runner-up is asked "does step N also use module X?"** in the stage-2 request. Its action scores that answer, not the module's share of the "main work" choice.
   ```python
   def picks(probs_i, cat, threshold=0.5):
       ...
       runner = [m for m, p in ranked[1:2] if p >= RUNNER_UP and m not in settled]
       main = [m for m, _ in ranked[:1] if m not in settled]
       return common, main + runner, runner
   # stage2(…, runners):
                   qs[f's{s["index"]}_@also.{m}'] = {'type': 'noul', 'instructions':
                       f'Step {step_no(s)} is `{s["text"].strip()}`. Does step {step_no(s)} also use the plang module `{m}`?'}
   ```
   It separates cleanly:

   | step | runner-up action | yes/no | expected? |
   |---|---|---|---|
   | decide 4 `foreach %modules%, call AddModuleToState …` | loop.foreach | **0.98** | yes |
   | start 0 `call /system/builder/EmitBuildEvent …` | build.actions | 0.14 | no |
   | start 3 `call Compile, on error call HandleBuildFailure` | build.validateStepActions | 0.17 | no |
   | decide 9 `call …EmitBuildEvent kind="goal-decided" …` | event.on | 0.08 | no |
   | weekly 10 `call SendReport …, on error call LogFailure` | http.request | 0.13 | no |

2. **Each common action's example steps sit beside it** in the state's "asked about by name" list (`e.g.` lines from `<action>.examples.md`, step texts only). It costs 3k tokens in, across the 5 goals. Its effect on write-to is small and inside one run's noise: variable.set on a trailing `write to %x%` scores 0.52–0.81 over 13 steps (v3 0.41–0.87).
3. **Actions held as values aren't picks.** The golden menus now list only the step's own actions. Two changes to `tools/decider/child_golden.json`:
   - build 3 `set channel "builder" call BuilderChannel`: `channel.set, goal.call` → `channel.set` (the goal.call is channel.set's Goal value);
   - start 13 `build.fold Goal=%goal%, on error call HandleBuildFailure`: `build.fold, error.handle, goal.call` → `build.fold, error.handle` (the goal.call is the recovery).
   
   The expected answers are unchanged: the goal.calls are still in the rows, as Goal's value and as the recovery. Start 3 and weekly 10 keep goal.call; there it is the step's own call. The stage-3 half (goal.call's definition added when a listed action takes actions) goes into prompt C, which doesn't exist yet.
4. **Action- and goal-typed properties reach the prompt.** `build_pr.declared` now shows them by default (`held_actions=False` gives the C# catalog as it is). So:
   - channel.set's Goal appears in the eval's signatures, and so do build.fold's and build.goalsSave's;
   - the golden now expects `Goal=%goal%` on start 13 and 14;
   - build/fold.notes.md and build/goalsSave.notes.md say what Goal is (they said "not a property" before).
   
   Until stage 4 changes `type/property/list/this.cs:82`, the plang template renders without them, so B's plang/python parity differs on build and start.

## What's left at 0.9 — all right at 0.5

| | steps | score |
|---|---|---|
| `…, write to %x%` → variable.set | 13 | 0.52–0.81 |
| loop.foreach as the **main** module, beside a `call` | 4 | 0.68–0.86 |
| error.throw as the main module, in `if …, throw …` | 2 | 0.58, 0.63 |

**Proposal (v5):**
- The main module has the same flaw item 1 fixed for the runner-up: its action scores its share of the "main work" choice. Asking it the same yes/no ("does step N use module X?") when that share is below 0.9 costs 19 questions across these goals, and would score loop.foreach and error.throw on whether they're used.
- write-to stays the decider's weak spot. It is also exactly what the plan's double check is for (a 0.5–0.9 pick is "possible", and the LLM confirms it).

Formal still holds after the golden change: `formal_check.py` gives 58/58 round trip, including `build.fold(Goal=%goal%)`.

## Per step (v4)

### build

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `set default %path% = "/"` | variable.set | variable.set 0.98 | variable 1.00 | ok | ok |
| 1 | `set default %!build.cache% = true` | variable.set | variable.set 0.98 | variable 1.00 | ok | ok |
| 2 | `set default %!build.summary% = true` | variable.set | variable.set 0.98 | variable 1.00 | ok | ok |
| 3 | `set channel "builder" call BuilderChannel` | channel.set | channel.set 1.00 (s2, conf 1.00), goal.call 0.31, output.write 0.08, variable.set 0.07 | channel 1.00 | ok | ok |
| 4 | `call EmitBuildEvent kind="build-path", path=%path%` | goal.call | goal.call 0.98, output.write 0.31, variable.set 0.09 | goal 0.89, event 0.07 | ok | ok |
| 5 | `build.load, write to %app%` | build.load, variable.set | build.load 1.00 (s2, conf 1.00), variable.set 0.72, output.write 0.44, file.read 0.12 | build 1.00 | miss variable.set | ok |
| 6 | `build.goals path=%path%, write to %goals%` | build.goals, variable.set | build.goals 1.00 (s2, conf 1.00), variable.set 0.64, output.write 0.18, file.read 0.08, goal.call 0.05 | build 1.00 | miss variable.set | ok |
| 7 | `call EmitBuildEvent kind="goals-found", goals=%goals%` | goal.call | goal.call 0.97, output.write 0.38, variable.set 0.06 | goal 0.86, event 0.09 | ok | ok |
| 8 | `foreach %goals%, call BuildGoal goal=%item%` | loop.foreach, goal.call | goal.call 0.98, loop.foreach 0.85 (s2, conf 1.00), variable.set 0.07, output.write 0.05 | loop 0.85, goal 0.15 | miss loop.foreach | ok |
| 9 | `save %traceGoals% to file '/.build/traces/%!trace.id%/manifest.json'` | file.save | file.save 0.93 (s2, conf 1.00), output.write 0.09, variable.set 0.07, goal.call 0.05 | file 0.93, build 0.07 | ok | ok |
| 10 | `build.appSave` | build.appSave | build.appSave 1.00 (s2, conf 1.00), output.write 0.10, variable.set 0.05 | build 1.00 | ok | ok |

stage 2 skipped: step 0 variable, step 1 variable, step 2 variable, step 4 goal, step 7 goal

### start

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `call /system/builder/EmitBuildEvent kind="goalHeader", goal=%goal%` | goal.call | goal.call 0.96, output.write 0.16, build.actions 0.14, variable.set 0.09 | goal 0.58, build 0.35, event 0.06 | ok | ok |
| 1 | `set %buildStart% = %Now.Ticks%` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 2 | `set %trace% = {"id": "%!trace.id%", "timestamp": "%Now%", "goal": %…` | variable.set | variable.set 0.97, goal.call 0.12 | variable 1.00 | ok | ok |
| 3 | `call Compile, on error call HandleBuildFailure` | goal.call, error.handle | error.handle 0.97, goal.call 0.95, build.validateStepActions 0.17 | goal 0.58, build 0.37 | ok | ok |
| 4 | `set %trace.menu% = %menu%` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 5 | `set %parentGoal% = %goal%` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 6 | `foreach %parentGoal.Child%, call BuildSubGoal subGoal=%item%` | loop.foreach, goal.call | goal.call 0.96, loop.foreach 0.68 (s2, conf 1.00), variable.set 0.15 | loop 0.68, goal 0.31 | miss loop.foreach | ok |
| 7 | `set %goal% = %parentGoal%` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 8 | `math.subtract A=%Now.Ticks%, B=%buildStart%, write to %elapsedTicks%` | math.subtract, variable.set | math.subtract 1.00 (s2, conf 1.00), variable.set 0.66, output.write 0.06 | math 1.00 | miss variable.set | ok |
| 9 | `math.divide A=%elapsedTicks%, B=10000, write to %elapsedMs%` | math.divide, variable.set | math.divide 1.00 (s2, conf 1.00), variable.set 0.69, output.write 0.07 | math 1.00 | miss variable.set | ok |
| 10 | `set %trace.durationMs% = %elapsedMs%` | variable.set | variable.set 0.98 | variable 1.00 | ok | ok |
| 11 | `save %trace% to file '/.build/traces/%!trace.id%/%goal.Name%.json'` | file.save | file.save 1.00 (s2, conf 1.00), variable.set 0.20, goal.call 0.05, output.write 0.05, error.handle 0.05 | file 1.00 | ok | ok |
| 12 | `add %goal.Name% to %traceGoals%` | list.add | list.add 1.00 (s2, conf 1.00), variable.set 0.18, goal.call 0.05 | list 1.00 | ok | ok |
| 13 | `build.fold Goal=%goal%, on error call HandleBuildFailure` | build.fold, error.handle | build.fold 1.00 (s2, conf 1.00), error.handle 0.96, goal.call 0.22, variable.set 0.07, output.write 0.05 | build 1.00 | ok | ok |
| 14 | `build.goalsSave Goal=%goal%` | build.goalsSave | build.goalsSave 1.00 (s2, conf 1.00), variable.set 0.08, goal.call 0.05 | build 1.00 | ok | ok |

stage 2 skipped: step 0 goal, step 1 variable, step 2 variable, step 3 goal, step 4 variable, step 5 variable, step 7 variable, step 10 variable

### decide

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `set %threshold% = 0.5` | variable.set | variable.set 0.98 | variable 1.00 | ok | ok |
| 1 | `set %modules% = %!app.module.list%` | variable.set | variable.set 0.93, goal.call 0.07, file.read 0.06, output.write 0.05 | variable 0.78, module 0.09, build 0.05 | ok | ok |
| 2 | `set %state.goal% = %goal.Name%` | variable.set | variable.set 0.96, goal.call 0.07 | variable 0.99 | ok | ok |
| 3 | `foreach %goal.Step%, call AddStepToState item=%step%` | loop.foreach, goal.call | goal.call 0.95, loop.foreach 0.69 (s2, conf 1.00), variable.set 0.11 | loop 0.69, goal 0.28 | miss loop.foreach | ok |
| 4 | `foreach %modules%, call AddModuleToState item=%module%` | loop.foreach, goal.call | loop.foreach 0.98, goal.call 0.94, variable.set 0.11 | goal 0.58, loop 0.36 | ok | ok |
| 5 | `render template "/system/builder/llm/templates/deciderModuleQuestio…` | ui.render, variable.set | ui.render 1.00 (s2, conf 1.00), variable.set 0.72, file.read 0.29, output.write 0.14 | ui 1.00 | miss variable.set | ok |
| 6 | `llm.decider State=%state%, Question=%moduleQuestions%, write to %mo…` | llm.decider, variable.set | llm.decider 1.00 (s2, conf 1.00), variable.set 0.56, output.write 0.08 | llm 1.00 | miss variable.set | ok |
| 7 | `render template "/system/builder/llm/templates/deciderActionQuestio…` | ui.render, variable.set | ui.render 1.00 (s2, conf 1.00), variable.set 0.69, file.read 0.22, output.write 0.13, goal.call 0.05 | ui 1.00 | miss variable.set | ok |
| 8 | `llm.decider State=%state%, Question=%actionQuestions%, write to %ac…` | llm.decider, variable.set | llm.decider 1.00 (s2, conf 1.00), variable.set 0.53, output.write 0.08 | llm 1.00 | miss variable.set | ok |
| 9 | `call /system/builder/EmitBuildEvent kind="goal-decided", goal=%goal%` | goal.call | goal.call 0.92, output.write 0.09, event.on 0.08, variable.set 0.05 | goal 0.43, event 0.33, build 0.24 | ok | ok |

stage 2 skipped: step 0 variable, step 1 variable, step 2 variable, step 4 goal, step 9 goal

### checkout

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `read 'orders/%orderId%.json', write to %order%` | file.read, variable.set | file.read 0.98, variable.set 0.81, output.write 0.06, error.handle 0.05 | file 1.00 | miss variable.set | ok |
| 1 | `count %order.items%, write to %itemCount%` | list.count, variable.set | list.count 0.97 (s2, conf 1.00), variable.set 0.75, output.write 0.06 | list 0.97 | miss variable.set | ok |
| 2 | `if %itemCount% is 0, write out "Your cart is empty", else if %itemC…` | condition.if, condition.elseif, condition.else, output.write | condition.if 0.98, condition.elseif 0.97, output.write 0.96, condition.else 0.95, variable.set 0.06 | condition 0.56, output 0.44 | ok | ok |
| 3 | `if %order.email% does not contain "@", throw "Invalid email address"` | condition.if, error.throw | condition.if 0.98, error.throw 0.58 (s2, conf 0.99), error.handle 0.38, condition.else 0.05 | error 0.58, condition 0.41 | miss error.throw | ok |
| 4 | `if %order.coupon% is not empty, call ApplyCoupon code=%order.coupon…` | condition.if, goal.call | condition.if 0.98, goal.call 0.93, variable.set 0.08, condition.else 0.06 | goal 0.97 | ok | ok |
| 5 | `if %order.total% is not a number, throw "The total must be a number"` | condition.if, error.throw | condition.if 0.97, error.throw 0.63 (s2, conf 0.99), error.handle 0.42, condition.else 0.10, variable.set 0.06, output.write 0.05 | error 0.63, condition 0.33 | miss error.throw | ok |
| 6 | `if %order.country% is "IS", set %vat% = 0.24, else set %vat% = 0.25` | condition.if, condition.else, variable.set | condition.if 0.99, variable.set 0.96, condition.else 0.95, condition.elseif 0.30 | variable 0.72, condition 0.28 | ok | ok |
| 7 | `math.multiply A=%order.total%, B=%vat%, write to %vatAmount%` | math.multiply, variable.set | math.multiply 1.00 (s2, conf 1.00), variable.set 0.66, output.write 0.05 | math 1.00 | miss variable.set | ok |
| 8 | `if %order.note% does not start with "#", add %order.note% to %notes%` | condition.if, list.add | condition.if 0.98, list.add 0.95 (s2, conf 1.00), variable.set 0.12, condition.else 0.06 | list 0.95, condition 0.05 | ok | ok |
| 9 | `write out "Total: %order.total%, VAT: %vatAmount%"` | output.write | output.write 0.98, variable.set 0.11 | output 1.00 | ok | ok |

stage 2 skipped: step 0 file, step 2 condition, step 4 goal, step 6 variable, step 9 output

### weekly_report

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `list files in '/logs', pattern "*.log", write to %files%` | file.list, variable.set | file.list 1.00 (s2, conf 1.00), variable.set 0.81, output.write 0.06, file.read 0.05 | file 1.00 | miss variable.set | ok |
| 1 | `set %errors% = []` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 2 | `foreach %files%, call ScanFile file=%item%` | loop.foreach, goal.call | goal.call 0.96, loop.foreach 0.86 (s2, conf 1.00), file.read 0.20, variable.set 0.15 | loop 0.86, goal 0.14 | miss loop.foreach | ok |
| 3 | `if %errors% is empty` | condition.if | condition.if 0.97, condition.else 0.07, goal.call 0.05, output.write 0.05 | condition 1.00 | ok | ok |
| 4 | `write out "No errors this week"` | output.write | output.write 0.98, condition.if 0.09 | output 1.00 | ok | ok |
| 5 | `return` | goal.return | goal.return 1.00 (s2, conf 1.00), goal.call 0.35, condition.if 0.09 | goal 1.00 | ok | ok |
| 6 | `if %files% is a list` | condition.if | condition.if 0.97, condition.else 0.12 | condition 1.00 | ok | ok |
| 7 | `write out "Scanned the log files"` | output.write | output.write 0.97, condition.if 0.14 | output 1.00 | ok | ok |
| 8 | `render template "report.html", errors=%errors%, write to %html%` | ui.render, variable.set | ui.render 1.00 (s2, conf 1.00), variable.set 0.52, output.write 0.16, condition.if 0.08, file.read 0.08, goal.call 0.05 | ui 1.00 | miss variable.set | ok |
| 9 | `save %html% to file '/reports/week.html'` | file.save | file.save 1.00 (s2, conf 1.00), variable.set 0.21, output.write 0.05, error.handle 0.05 | file 1.00 | ok | ok |
| 10 | `call SendReport to="ops@example.com", subject="Weekly report", on e…` | goal.call, error.handle | error.handle 0.97, goal.call 0.96, output.write 0.17, http.request 0.13, variable.set 0.05 | goal 0.62, http 0.35 | ok | ok |
| 11 | `write out "Report sent" to "log" channel` | output.write | output.write 0.97 | output 1.00 | ok | ok |

stage 2 skipped: step 1 variable, step 3 condition, step 4 output, step 6 condition, step 7 output, step 10 goal, step 11 output
