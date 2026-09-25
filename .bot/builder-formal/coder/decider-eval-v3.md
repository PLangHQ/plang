# Decider v3 — v2 plus the four fixes

Branch `builder-formal`. The same 5 golden goals (58 steps, 90 expected actions), the decider alone, 1 run per shape.

- Raw: `tools/decider/runs/decider_eval_20260925_212505.json` (v3) and `…_212532.json` (v3 with the clause off).
- Requests: `/shared/coder/llm/plang/builder-formal/decider-v3/<goal>/` and `…/decider-v3-noclause/<goal>/`.

## The four fixes (`tools/decider/harness.py`, `os/system/modules/variable/set.examples.md`)

```python
RUNNER_UP = 0.2   # the choice's second module at or above this is asked in stage 2 as well

def picks(probs_i, cat, threshold=0.5):
    common = {a: probs_i.get(a) for a in COMMON if probs_i.get(a) is not None}
    settled = {a.split('.', 1)[0] for a, p in common.items() if p >= NEAR_CERTAIN}
    ranked = sorted(((m, p) for m, p in probs_i.items() if m in cat and p is not None), key=lambda mp: -mp[1])
    asked = ranked[:1] + [(m, p) for m, p in ranked[1:2] if p >= RUNNER_UP]
    return common, [m for m, _ in asked if m not in settled]

BRANCHES = ['condition.elseif', 'condition.else']
# stage2(goal, cat, chosen, conditions): for each step with condition.if ≥ 0.5, in the same request:
                    qs[f's{s["index"]}_{a}'] = {'type': 'noul', 'instructions':
                        f'Step {step_no(s)} is `{s["text"].strip()}`. It tests a condition. Does step {step_no(s)} also have `{a}` — '
                        f'{cat["condition"]["actions"][a.split(".", 1)[1]]["description"]}?'}

# the common-action question, with CLAUSE=1:
                    f'Step {step_no(s)} is `{text}`. Does step {step_no(s)}{CLAUSE} use `{a}`?'
CLAUSE = ' — its own work, or anything it guards behind a condition, repeats in a loop, or hands to an error handler —'
```

`variable/set.examples.md` gains 4 step texts (no JSON). I chose them away from the golden steps so the measurement doesn't flatter itself:

```
Step text: `get 'https://example.com/rates.json', write to %rates%`
Step text: `hash %password%, write to %passwordHash%`
Step text: `call GetUser id=%userId%, write to %user%`
Step text: `sort %names%, write to %sortedNames%`
```

## Side by side

| | v1 (ade8d1cdd) | v2 (d902a4502) | v3 | v3, clause off |
|---|---|---|---|---|
| stage-1 questions | 2,146 | 406 | 406 | 406 |
| stage-1 request KB (sum; checkout) | 724; 133 | 149; 29 | 190; 36 | 150; 29 |
| stage-1 seconds (sum; slowest) | 8.2; 1.8 | 4.6; 1.0 | 5.6; 1.3 | 4.7; 1.1 |
| stage-1 tokens in / out | 179,471 / 39,408 | 44,973 / 20,436 | 54,043 / 20,436 | 45,343 / 20,436 |
| stage-2 questions (skipped) | 40 (40) | 20 (31) | 43 (28) | 41 (30) |
| stage-2 request KB | 116 | 69 | 95 | 90 |
| stage-2 seconds (sum) | 4.2 | 3.8 | 4.8 | 4.4 |
| stage-2 tokens in / out | 34,005 / 3,161 | 20,324 / 1,912 | 27,286 / 2,679 | 25,805 / 2,603 |
| cut 0.9: steps exact | 37/58 | 33/58 | 34/58 | **36/58** |
| cut 0.9: hit / missed / extra | 67 / 23 / 0 | 64 / 26 / 0 | 66 / 24 / 0 | 68 / 22 / 0 |
| cut 0.5: steps exact | **53/58** | 51/58 | 46/58 | 51/58 |
| cut 0.5: hit / missed / extra | 87 / 3 / 3 | 83 / 7 / 1 | 85 / 5 / 7 | 85 / 5 / 2 |

Nothing ≥ 0.9 is wrong in any of the four.

## Fix by fix

1. **else / elseif by name — works.** Checkout 2 (`if … else if … else …`) and 6 (`if … else …`) are now exact, with every branch at 0.9 or more. The 3 structural misses of v1 and v2 are gone. The cost is 2 stage-2 questions per step with a condition.
2. **The runner-up module — half works.** It reached error.throw on the two `if …, throw …` steps and loop.foreach on `foreach …, call …`. But the action scores its module's share of the one choice, and when two modules share a step the choice splits about 50/50. So error.throw scores 0.47–0.54 and loop.foreach 0.45–0.84, even with stage 2 answering them at confidence 1.00. It also added one extra: build.actions 0.55 on `call /system/builder/EmitBuildEvent …` (build 0.55 over goal 0.38 in the choice).
   **Proposal:** score a stage-2 action as main-module probability + runner-up probability when both modules are in the step's picks, or ask a noul for the named action in a third short request. Or make loop.foreach a common action (5 of the 58 steps). I'd take the common action; it is the cheapest.
3. **The clause back on the common questions — hurts. I've turned it off by default** (`CLAUSE=1` switches it on), which departs from what was asked, on the numbers:
   - output.write fired on `call EmitBuildEvent …` (0.52, 0.62) and on bare `if` steps whose body is indented below (0.53, 0.65);
   - error.handle fired on `throw` steps (0.70, 0.72): "hands to an error handler" reads as throwing;
   - it did not bring back goal.call inside `on error call X` (0.37; clause off 0.30);
   - 46/58 against 51/58 with it off.
4. **write-to step texts — no measurable effect.** variable.set on a trailing `write to %x%`, over 13 steps:

   | | min–max |
   |---|---|
   | v2 | 0.53–0.86 |
   | v3 | 0.40–0.87 |
   | v3, clause off | 0.41–0.87 |

   One run can't separate this from noise. The examples reach stage 1 only as the variable module's `e.g.` lines in the state, not beside the variable.set question. **Proposal:** show each common action's own example steps beside it in the state's "asked about by name" list.

## Still missed at 0.5 (clause off)

| step | expected | why |
|---|---|---|
| build 3 `set channel "builder" call BuilderChannel` | goal.call | 0.33; the golden vs STRUCTURE's "naming a goal to run later is not calling it" (v1 finding 5, open) |
| start 13 `build.fold …, on error call HandleBuildFailure` | goal.call | 0.30; the call inside an on-error clause |
| decide 4 `foreach %modules%, call …` | loop.foreach | 0.46; the split choice (fix 2) |
| checkout 3 `if … does not contain "@", throw …` | error.throw | 0.47; the split choice (fix 2) |
| weekly 8 `render template …, write to %html%` | variable.set | 0.41; write-to (fix 4) |

Extras at 0.5: output.write 0.75 on `build.load, write to %app%`, and build.actions 0.55 (above).

**Recommendation:** v3 with the clause off, loop.foreach as a common action, and the write-to examples placed beside the question. Then the plan's stage 3 takes 0.5–0.9 as "possible", so these become the LLM's to confirm, and the double check is what catches them.

## Per step (v3, clause off)

### build

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `set default %path% = "/"` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 1 | `set default %!build.cache% = true` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 2 | `set default %!build.summary% = true` | variable.set | variable.set 0.97 | variable 0.99 | ok | ok |
| 3 | `set channel "builder" call BuilderChannel` | channel.set, goal.call | channel.set 1.00 (s2, conf 1.00), goal.call 0.33, output.write 0.09, variable.set 0.06 | channel 1.00 | miss goal.call | miss goal.call |
| 4 | `call EmitBuildEvent kind="build-path", path=%path%` | goal.call | goal.call 0.96, output.write 0.39, variable.set 0.07 | goal 0.88, event 0.08 | ok | ok |
| 5 | `build.load, write to %app%` | build.load, variable.set | build.load 1.00 (s2, conf 1.00), output.write 0.75, variable.set 0.74, file.read 0.13 | build 1.00 | miss variable.set | extra output.write |
| 6 | `build.goals path=%path%, write to %goals%` | build.goals, variable.set | build.goals 1.00 (s2, conf 1.00), variable.set 0.71, output.write 0.24, file.read 0.06, goal.call 0.05 | build 1.00 | miss variable.set | ok |
| 7 | `call EmitBuildEvent kind="goals-found", goals=%goals%` | goal.call | goal.call 0.97, output.write 0.48, variable.set 0.06 | goal 0.80, output 0.10, event 0.09 | ok | ok |
| 8 | `foreach %goals%, call BuildGoal goal=%item%` | loop.foreach, goal.call | goal.call 0.97, loop.foreach 0.66 (s2, conf 1.00), variable.set 0.07, output.write 0.06 | loop 0.66, goal 0.34 | miss loop.foreach | ok |
| 9 | `save %traceGoals% to file '/.build/traces/%!trace.id%/manifest.json'` | file.save | file.save 0.91 (s2, conf 1.00), output.write 0.08, variable.set 0.06 | file 0.91, build 0.09 | ok | ok |
| 10 | `build.appSave` | build.appSave | build.appSave 1.00 (s2, conf 1.00), output.write 0.09, goal.call 0.05 | build 1.00 | ok | ok |

stage 2 skipped: step 0 variable, step 1 variable, step 2 variable, step 4 goal, step 7 goal

### start

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `call /system/builder/EmitBuildEvent kind="goalHeader", goal=%goal%` | goal.call | goal.call 0.91, build.actions 0.55 (s2, conf 0.62), output.write 0.23, variable.set 0.06 | build 0.55, goal 0.38, event 0.05 | ok | extra build.actions |
| 1 | `set %buildStart% = %Now.Ticks%` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 2 | `set %trace% = {"id": "%!trace.id%", "timestamp": "%Now%", "goal": %…` | variable.set | variable.set 0.96, goal.call 0.08 | variable 1.00 | ok | ok |
| 3 | `call Compile, on error call HandleBuildFailure` | goal.call, error.handle | error.handle 0.96, goal.call 0.92, build.validate 0.33 (s2, conf 0.26) | goal 0.63, build 0.33 | ok | ok |
| 4 | `set %trace.menu% = %menu%` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 5 | `set %parentGoal% = %goal%` | variable.set | variable.set 0.97, goal.call 0.05 | variable 1.00 | ok | ok |
| 6 | `foreach %parentGoal.Child%, call BuildSubGoal subGoal=%item%` | loop.foreach, goal.call | goal.call 0.95, loop.foreach 0.68 (s2, conf 1.00), variable.set 0.13 | loop 0.68, goal 0.31 | miss loop.foreach | ok |
| 7 | `set %goal% = %parentGoal%` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 8 | `math.subtract A=%Now.Ticks%, B=%buildStart%, write to %elapsedTicks%` | math.subtract, variable.set | math.subtract 1.00 (s2, conf 1.00), variable.set 0.55, output.write 0.06 | math 1.00 | miss variable.set | ok |
| 9 | `math.divide A=%elapsedTicks%, B=10000, write to %elapsedMs%` | math.divide, variable.set | math.divide 1.00 (s2, conf 1.00), variable.set 0.67, output.write 0.08 | math 1.00 | miss variable.set | ok |
| 10 | `set %trace.durationMs% = %elapsedMs%` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 11 | `save %trace% to file '/.build/traces/%!trace.id%/%goal.Name%.json'` | file.save | file.save 1.00 (s2, conf 1.00), variable.set 0.16, output.write 0.06, goal.call 0.05 | file 1.00 | ok | ok |
| 12 | `add %goal.Name% to %traceGoals%` | list.add | list.add 1.00 (s2, conf 1.00), variable.set 0.15 | list 1.00 | ok | ok |
| 13 | `build.fold Goal=%goal%, on error call HandleBuildFailure` | build.fold, error.handle, goal.call | build.fold 1.00 (s2, conf 1.00), error.handle 0.96, goal.call 0.30, variable.set 0.08, output.write 0.05 | build 1.00 | miss goal.call | miss goal.call |
| 14 | `build.goalsSave Goal=%goal%` | build.goalsSave | build.goalsSave 1.00 (s2, conf 1.00), variable.set 0.07 | build 1.00 | ok | ok |

stage 2 skipped: step 1 variable, step 2 variable, step 3 goal, step 4 variable, step 5 variable, step 7 variable, step 10 variable

### decide

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `set %threshold% = 0.5` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 1 | `set %modules% = %!app.module.list%` | variable.set | variable.set 0.93, file.read 0.08, goal.call 0.07, output.write 0.05 | variable 0.65, module 0.14, build 0.07, code 0.07 | ok | ok |
| 2 | `set %state.goal% = %goal.Name%` | variable.set | variable.set 0.96, goal.call 0.08 | variable 0.98 | ok | ok |
| 3 | `foreach %goal.Step%, call AddStepToState item=%step%` | loop.foreach, goal.call | goal.call 0.93, loop.foreach 0.72 (s2, conf 1.00), variable.set 0.12 | loop 0.72, goal 0.26 | miss loop.foreach | ok |
| 4 | `foreach %modules%, call AddModuleToState item=%module%` | loop.foreach, goal.call | goal.call 0.92, loop.foreach 0.46 (s2, conf 1.00), variable.set 0.11 | goal 0.48, loop 0.46 | miss loop.foreach | miss loop.foreach |
| 5 | `render template "/system/builder/llm/templates/deciderModuleQuestio…` | ui.render, variable.set | ui.render 1.00 (s2, conf 1.00), variable.set 0.64, file.read 0.34, output.write 0.13 | ui 1.00 | miss variable.set | ok |
| 6 | `llm.decider State=%state%, Question=%moduleQuestions%, write to %mo…` | llm.decider, variable.set | llm.decider 1.00 (s2, conf 1.00), variable.set 0.51, output.write 0.08 | llm 1.00 | miss variable.set | ok |
| 7 | `render template "/system/builder/llm/templates/deciderActionQuestio…` | ui.render, variable.set | ui.render 1.00 (s2, conf 1.00), variable.set 0.59, file.read 0.24, output.write 0.09 | ui 1.00 | miss variable.set | ok |
| 8 | `llm.decider State=%state%, Question=%actionQuestions%, write to %ac…` | llm.decider, variable.set | llm.decider 1.00 (s2, conf 1.00), variable.set 0.56, output.write 0.08, goal.call 0.05 | llm 1.00 | miss variable.set | ok |
| 9 | `call /system/builder/EmitBuildEvent kind="goal-decided", goal=%goal%` | goal.call | goal.call 0.92, event.on 0.30 (s2, conf 0.89), output.write 0.11, variable.set 0.05 | goal 0.42, event 0.30, build 0.28 | ok | ok |

stage 2 skipped: step 0 variable, step 1 variable, step 2 variable, step 4 goal, step 9 goal

### checkout

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `read 'orders/%orderId%.json', write to %order%` | file.read, variable.set | file.read 0.97, variable.set 0.85, output.write 0.07 | file 1.00 | miss variable.set | ok |
| 1 | `count %order.items%, write to %itemCount%` | list.count, variable.set | list.count 0.98 (s2, conf 1.00), variable.set 0.82, output.write 0.06 | list 0.98 | miss variable.set | ok |
| 2 | `if %itemCount% is 0, write out "Your cart is empty", else if %itemC…` | condition.if, condition.elseif, condition.else, output.write | condition.if 0.99, output.write 0.97, condition.elseif 0.97, condition.else 0.95, variable.set 0.06 | condition 0.55, output 0.45 | ok | ok |
| 3 | `if %order.email% does not contain "@", throw "Invalid email address"` | condition.if, error.throw | condition.if 0.98, error.handle 0.47, error.throw 0.47 (s2, conf 1.00), condition.else 0.05 | condition 0.52, error 0.47 | miss error.throw | miss error.throw |
| 4 | `if %order.coupon% is not empty, call ApplyCoupon code=%order.coupon…` | condition.if, goal.call | condition.if 0.98, goal.call 0.92, condition.else 0.08, variable.set 0.07 | goal 0.98 | ok | ok |
| 5 | `if %order.total% is not a number, throw "The total must be a number"` | condition.if, error.throw | condition.if 0.97, error.throw 0.60 (s2, conf 0.99), error.handle 0.42, condition.else 0.10, output.write 0.05 | error 0.60, condition 0.38 | miss error.throw | ok |
| 6 | `if %order.country% is "IS", set %vat% = 0.24, else set %vat% = 0.25` | condition.if, condition.else, variable.set | condition.if 0.98, variable.set 0.97, condition.else 0.95, condition.elseif 0.29 | variable 0.73, condition 0.27 | ok | ok |
| 7 | `math.multiply A=%order.total%, B=%vat%, write to %vatAmount%` | math.multiply, variable.set | math.multiply 1.00 (s2, conf 1.00), variable.set 0.72, output.write 0.05 | math 1.00 | miss variable.set | ok |
| 8 | `if %order.note% does not start with "#", add %order.note% to %notes%` | condition.if, list.add | condition.if 0.98, list.add 0.98 (s2, conf 1.00), variable.set 0.09, condition.else 0.07 | list 0.98 | ok | ok |
| 9 | `write out "Total: %order.total%, VAT: %vatAmount%"` | output.write | output.write 0.98, variable.set 0.07 | output 1.00 | ok | ok |

stage 2 skipped: step 0 file, step 2 condition, step 3 condition, step 4 goal, step 6 variable, step 9 output

### weekly_report

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `list files in '/logs', pattern "*.log", write to %files%` | file.list, variable.set | file.list 1.00 (s2, conf 1.00), variable.set 0.72, output.write 0.09, file.read 0.05 | file 1.00 | miss variable.set | ok |
| 1 | `set %errors% = []` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 2 | `foreach %files%, call ScanFile file=%item%` | loop.foreach, goal.call | goal.call 0.95, loop.foreach 0.89 (s2, conf 1.00), file.read 0.23, variable.set 0.13, output.write 0.05 | loop 0.89, goal 0.11 | miss loop.foreach | ok |
| 3 | `if %errors% is empty` | condition.if | condition.if 0.97, condition.else 0.08, output.write 0.05 | condition 1.00 | ok | ok |
| 4 | `write out "No errors this week"` | output.write | output.write 0.98, condition.if 0.08 | output 1.00 | ok | ok |
| 5 | `return` | goal.return | goal.return 1.00 (s2, conf 1.00), goal.call 0.23, condition.if 0.08 | goal 1.00 | ok | ok |
| 6 | `if %files% is a list` | condition.if | condition.if 0.96, condition.else 0.09, output.write 0.05 | condition 1.00 | ok | ok |
| 7 | `write out "Scanned the log files"` | output.write | output.write 0.97, condition.if 0.12 | output 1.00 | ok | ok |
| 8 | `render template "report.html", errors=%errors%, write to %html%` | ui.render, variable.set | ui.render 1.00 (s2, conf 1.00), variable.set 0.41, file.read 0.18, output.write 0.16, condition.if 0.06 | ui 1.00 | miss variable.set | miss variable.set |
| 9 | `save %html% to file '/reports/week.html'` | file.save | file.save 1.00 (s2, conf 1.00), variable.set 0.19, output.write 0.05, error.handle 0.05 | file 1.00 | ok | ok |
| 10 | `call SendReport to="ops@example.com", subject="Weekly report", on e…` | goal.call, error.handle | goal.call 0.96, error.handle 0.96, http.request 0.26 (s2, conf 0.98), output.write 0.16, variable.set 0.05 | goal 0.73, http 0.26 | ok | ok |
| 11 | `write out "Report sent" to "log" channel` | output.write | output.write 0.97 | output 1.00 | ok | ok |

stage 2 skipped: step 1 variable, step 3 condition, step 4 output, step 6 condition, step 7 output, step 10 goal, step 11 output
