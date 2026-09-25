# Decider v2 — v0.1's question shape plus the common actions

Branch `builder-formal`. The same 5 golden goals (58 steps, 90 expected actions), 1 run, the decider alone. It is compared with v1 (`ade8d1cdd`, [decider-eval.md](decider-eval.md)). Only the question shape changed. Proposals 1 (write-to examples) and 2 (condition per-action questions) are not in this run.

- Raw: `tools/decider/runs/decider_eval_20260925_163553.json` (v1: `…_162944.json`).
- Requests and responses: `/shared/coder/llm/plang/builder-formal/decider-v2/<goal>/{1,2}.decider.*`, the same layout as `decider/`, with `state.txt`, `questions.json` and `answers.json`.

## Side by side

| | v1 (ade8d1cdd) | v2 |
|---|---|---|
| stage-1 questions | 2,146 | **406** (7 per step) |
| stage-1 request KB (sum; checkout) | 724; 133 | **149; 29** |
| stage-1 seconds (sum; slowest goal) | 8.2; 1.8 | **4.6; 1.0** |
| stage-1 tokens in / out | 179,471 / 39,408 | **44,973 / 20,436** |
| stage-2 questions (skipped) | 40 (40) | 20 (31) |
| stage-2 request KB | 116 | 69 |
| stage-2 seconds (sum) | 4.2 | 3.8 |
| stage-2 tokens in / out | 34,005 / 3,161 | 20,324 / 1,912 |
| cut 0.9: steps exact | 37/58 | 33/58 |
| cut 0.9: hit / missed / extra | 67 / 23 / 0 | 64 / 26 / 0 |
| cut 0.5: steps exact | **53/58** | 51/58 |
| cut 0.5: hit / missed / extra | 87 / 3 / 3 | 83 / 7 / 1 |

**v2 is 5× smaller and twice as fast, and 2 steps worse at 0.5.** Every pick ≥ 0.9 is still right in both.

## The code (`tools/decider/harness.py`)

```python
def stage1(goal, cat):
    state = state_for(goal, cat)          # module descriptions, notes, example steps, and the 6 common actions: once
    options = {m: None for m in cat}      # bare names
    ...
            qs[f's{s["index"]}_@module'] = {'type': 'choice', 'criteria': options, 'instructions':
                f'Step {step_no(s)} of this goal is `{text}`. Which plang module does the main work of step {step_no(s)}?'}
            for a in COMMON:
                qs[f's{s["index"]}_{a}'] = {'type': 'noul', 'instructions':
                    f'Step {step_no(s)} is `{text}`. Does step {step_no(s)} use `{a}`?'}
    ...
            if m == '@module': probs[int(i)].update(a.get('probabilities') or {a.get('choice'): a.get('confidence')})
            else: probs[int(i)][m] = a.get('noul')

def picks(probs_i, cat, threshold=0.5):
    common = {a: probs_i.get(a) for a in COMMON if probs_i.get(a) is not None}
    settled = {a.split('.', 1)[0] for a, p in common.items() if p >= NEAR_CERTAIN}
    main = main_module(probs_i, cat)
    return common, [main] if main and main not in settled else []
```

- Stage 2 is unchanged: one choice for the main module's action. It is skipped for a one-action module, or when a common action of the main module is ≥ 0.9.
- Steps are numbered from 0 (`step_no(s) = s['index']`, "Its steps are numbered from 0").
- The choice's per-module probabilities are all kept (`probs[i]`) and shown per step below.

## What v2 lost (the 7 misses at 0.5), prompt-first

1. **Two modules share one choice.** A step whose work is split between two modules gets one of them; the other never reaches stage 2:
   - decide 4, `foreach %modules%, call AddModuleToState item=%module%`: goal 0.50, loop 0.44 → loop.foreach missed.
   - checkout 3, `if %order.email% does not contain "@", throw …`: condition 0.51, error 0.48 → error.throw missed.
   
   v1's noul per module caught both. **Proposal:** stage 2 also asks the runner-up module when its probability is ≥ 0.2.
   - That is 13 of the 58 steps here, so 13 more stage-2 questions. It reaches loop and error on those two steps.
   - 4 of the 13 runner-ups are noise (build 0.33 / 0.22, module 0.23, event 0.30, http 0.29). Their action would score its module's probability, below 0.5, so it is never used.
   
   Alternatively, loop.foreach becomes a common action: it is in 5 of the 58 golden steps.
2. **goal.call inside a clause.** In `build.fold Goal=%goal%, on error call HandleBuildFailure`, goal.call scored 0.39 (v1 0.63). In `set channel "builder" call BuilderChannel` it scored 0.32 (v1 0.60). The short common-action question dropped v1's "its own work, or anything it guards behind a condition, repeats in a loop, or hands to an error handler". The same teaching is still in STRUCTURE in the state, but not in the question. **Proposal:** put that clause back on the common-action questions only (6 per step, not 37).
3. **else / elseif** (checkout 2, 6): the same as v1, structural (proposal 2 from v1, held).
4. **The one extra**: output.write 0.68 on `build.load, write to %app%`.

At 0.9 the misses are write-to → variable.set (0.53–0.86, the same as v1's finding 1), and loop.foreach at 0.62–0.88 as the main module when a `call` shares the step (goal takes 0.12–0.37 of the probability).

## Per step (v2)

`module choice` = the stage-1 choice's probabilities ≥ 0.05. The first one is the main module.

### build

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `set default %path% = "/"` | variable.set | variable.set 0.98 | variable 1.00 | ok | ok |
| 1 | `set default %!build.cache% = true` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 2 | `set default %!build.summary% = true` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 3 | `set channel "builder" call BuilderChannel` | channel.set, goal.call | channel.set 1.00 (s2, conf 1.00), goal.call 0.32, output.write 0.08, variable.set 0.07 | channel 1.00 | miss goal.call | miss goal.call |
| 4 | `call EmitBuildEvent kind="build-path", path=%path%` | goal.call | goal.call 0.96, output.write 0.44, variable.set 0.06 | goal 0.87, event 0.08 | ok | ok |
| 5 | `build.load, write to %app%` | build.load, variable.set | build.load 1.00 (s2, conf 1.00), output.write 0.68, variable.set 0.57, file.read 0.15 | build 1.00 | miss variable.set | extra output.write |
| 6 | `build.goals path=%path%, write to %goals%` | build.goals, variable.set | build.goals 1.00 (s2, conf 1.00), variable.set 0.71, output.write 0.31, file.read 0.08, goal.call 0.05 | build 1.00 | miss variable.set | ok |
| 7 | `call EmitBuildEvent kind="goals-found", goals=%goals%` | goal.call | goal.call 0.97, output.write 0.44, variable.set 0.07 | goal 0.91, event 0.05 | ok | ok |
| 8 | `foreach %goals%, call BuildGoal goal=%item%` | loop.foreach, goal.call | goal.call 0.97, loop.foreach 0.80 (s2, conf 1.00), variable.set 0.07, output.write 0.05 | loop 0.80, goal 0.20 | miss loop.foreach | ok |
| 9 | `save %traceGoals% to file '/.build/traces/%!trace.id%/manifest.json'` | file.save | file.save 0.85 (s2, conf 1.00), output.write 0.08, variable.set 0.06 | file 0.85, build 0.15 | miss file.save | ok |
| 10 | `build.appSave` | build.appSave | build.appSave 1.00 (s2, conf 1.00), output.write 0.08 | build 1.00 | ok | ok |

stage 2 skipped: step 0 variable, step 1 variable, step 2 variable, step 4 goal, step 7 goal

### start

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `call /system/builder/EmitBuildEvent kind="goalHeader", goal=%goal%` | goal.call | goal.call 0.95, output.write 0.20, variable.set 0.05 | goal 0.59, build 0.33, event 0.07 | ok | ok |
| 1 | `set %buildStart% = %Now.Ticks%` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 2 | `set %trace% = {"id": "%!trace.id%", "timestamp": "%Now%", "goal": %…` | variable.set | variable.set 0.96, goal.call 0.08 | variable 1.00 | ok | ok |
| 3 | `call Compile, on error call HandleBuildFailure` | goal.call, error.handle | error.handle 0.96, goal.call 0.93, file.read 0.05 | goal 0.73, build 0.22, error 0.05 | ok | ok |
| 4 | `set %trace.menu% = %menu%` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 5 | `set %parentGoal% = %goal%` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 6 | `foreach %parentGoal.Child%, call BuildSubGoal subGoal=%item%` | loop.foreach, goal.call | goal.call 0.96, loop.foreach 0.62 (s2, conf 1.00), variable.set 0.15 | loop 0.62, goal 0.37 | miss loop.foreach | ok |
| 7 | `set %goal% = %parentGoal%` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 8 | `math.subtract A=%Now.Ticks%, B=%buildStart%, write to %elapsedTicks%` | math.subtract, variable.set | math.subtract 1.00 (s2, conf 1.00), variable.set 0.70, output.write 0.07 | math 1.00 | miss variable.set | ok |
| 9 | `math.divide A=%elapsedTicks%, B=10000, write to %elapsedMs%` | math.divide, variable.set | math.divide 1.00 (s2, conf 1.00), variable.set 0.73, output.write 0.08 | math 1.00 | miss variable.set | ok |
| 10 | `set %trace.durationMs% = %elapsedMs%` | variable.set | variable.set 0.98 | variable 1.00 | ok | ok |
| 11 | `save %trace% to file '/.build/traces/%!trace.id%/%goal.Name%.json'` | file.save | file.save 1.00 (s2, conf 1.00), variable.set 0.11, output.write 0.07, error.handle 0.05 | file 1.00 | ok | ok |
| 12 | `add %goal.Name% to %traceGoals%` | list.add | list.add 1.00 (s2, conf 1.00), variable.set 0.10 | list 1.00 | ok | ok |
| 13 | `build.fold Goal=%goal%, on error call HandleBuildFailure` | build.fold, error.handle, goal.call | build.fold 1.00 (s2, conf 1.00), error.handle 0.96, goal.call 0.39, variable.set 0.06, output.write 0.05 | build 1.00 | miss goal.call | miss goal.call |
| 14 | `build.goalsSave Goal=%goal%` | build.goalsSave | build.goalsSave 1.00 (s2, conf 1.00), variable.set 0.06, output.write 0.05, error.handle 0.05 | build 1.00 | ok | ok |

stage 2 skipped: step 0 goal, step 1 variable, step 2 variable, step 3 goal, step 4 variable, step 5 variable, step 7 variable, step 10 variable

### decide

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `set %threshold% = 0.5` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 1 | `set %modules% = %!app.module.list%` | variable.set | variable.set 0.93, goal.call 0.06, file.read 0.06, output.write 0.05 | variable 0.58, module 0.23, build 0.09, code 0.05 | ok | ok |
| 2 | `set %state.goal% = %goal.Name%` | variable.set | variable.set 0.96, goal.call 0.09 | variable 0.99 | ok | ok |
| 3 | `foreach %goal.Step%, call AddStepToState item=%step%` | loop.foreach, goal.call | goal.call 0.93, loop.foreach 0.68 (s2, conf 1.00), variable.set 0.12 | loop 0.68, goal 0.26 | miss loop.foreach | ok |
| 4 | `foreach %modules%, call AddModuleToState item=%module%` | loop.foreach, goal.call | goal.call 0.91, variable.set 0.12, output.write 0.05 | goal 0.50, loop 0.44 | miss loop.foreach | miss loop.foreach |
| 5 | `render template "/system/builder/llm/templates/deciderModuleQuestio…` | ui.render, variable.set | ui.render 1.00 (s2, conf 1.00), variable.set 0.73, file.read 0.34, output.write 0.10 | ui 1.00 | miss variable.set | ok |
| 6 | `llm.decider State=%state%, Question=%moduleQuestions%, write to %mo…` | llm.decider, variable.set | llm.decider 1.00 (s2, conf 1.00), variable.set 0.53, output.write 0.08, goal.call 0.05 | llm 1.00 | miss variable.set | ok |
| 7 | `render template "/system/builder/llm/templates/deciderActionQuestio…` | ui.render, variable.set | ui.render 1.00 (s2, conf 1.00), variable.set 0.70, file.read 0.22, output.write 0.08 | ui 1.00 | miss variable.set | ok |
| 8 | `llm.decider State=%state%, Question=%actionQuestions%, write to %ac…` | llm.decider, variable.set | llm.decider 1.00 (s2, conf 1.00), variable.set 0.61, output.write 0.06, goal.call 0.05 | llm 1.00 | miss variable.set | ok |
| 9 | `call /system/builder/EmitBuildEvent kind="goal-decided", goal=%goal%` | goal.call | goal.call 0.90, output.write 0.11, variable.set 0.05 | goal 0.44, event 0.30, build 0.26 | ok | ok |

stage 2 skipped: step 0 variable, step 1 variable, step 2 variable, step 4 goal, step 9 goal

### checkout

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `read 'orders/%orderId%.json', write to %order%` | file.read, variable.set | file.read 0.97, variable.set 0.86, output.write 0.07 | file 1.00 | miss variable.set | ok |
| 1 | `count %order.items%, write to %itemCount%` | list.count, variable.set | list.count 0.99 (s2, conf 1.00), variable.set 0.83, output.write 0.09 | list 0.99 | miss variable.set | ok |
| 2 | `if %itemCount% is 0, write out "Your cart is empty", else if %itemC…` | condition.if, condition.elseif, condition.else, output.write | condition.if 0.99, output.write 0.96, variable.set 0.05 | condition 0.54, output 0.46 | miss condition.else, condition.elseif | miss condition.else, condition.elseif |
| 3 | `if %order.email% does not contain "@", throw "Invalid email address"` | condition.if, error.throw | condition.if 0.98, error.handle 0.41 | condition 0.51, error 0.48 | miss error.throw | miss error.throw |
| 4 | `if %order.coupon% is not empty, call ApplyCoupon code=%order.coupon…` | condition.if, goal.call | condition.if 0.98, goal.call 0.91, variable.set 0.06, output.write 0.05 | goal 0.97 | ok | ok |
| 5 | `if %order.total% is not a number, throw "The total must be a number"` | condition.if, error.throw | condition.if 0.97, error.throw 0.55 (s2, conf 1.00), error.handle 0.41 | error 0.55, condition 0.42 | miss error.throw | ok |
| 6 | `if %order.country% is "IS", set %vat% = 0.24, else set %vat% = 0.25` | condition.if, condition.else, variable.set | condition.if 0.98, variable.set 0.96 | variable 0.72, condition 0.28 | miss condition.else | miss condition.else |
| 7 | `math.multiply A=%order.total%, B=%vat%, write to %vatAmount%` | math.multiply, variable.set | math.multiply 1.00 (s2, conf 1.00), variable.set 0.70, output.write 0.07 | math 1.00 | miss variable.set | ok |
| 8 | `if %order.note% does not start with "#", add %order.note% to %notes%` | condition.if, list.add | condition.if 0.98, list.add 0.97 (s2, conf 1.00), variable.set 0.08 | list 0.97 | ok | ok |
| 9 | `write out "Total: %order.total%, VAT: %vatAmount%"` | output.write | output.write 0.98, variable.set 0.06 | output 1.00 | ok | ok |

stage 2 skipped: step 0 file, step 2 condition, step 3 condition, step 4 goal, step 6 variable, step 9 output

### weekly_report

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `list files in '/logs', pattern "*.log", write to %files%` | file.list, variable.set | file.list 1.00 (s2, conf 1.00), variable.set 0.82, output.write 0.07, file.read 0.05 | file 1.00 | miss variable.set | ok |
| 1 | `set %errors% = []` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 2 | `foreach %files%, call ScanFile file=%item%` | loop.foreach, goal.call | goal.call 0.94, loop.foreach 0.88 (s2, conf 1.00), file.read 0.24, variable.set 0.14, output.write 0.05 | loop 0.88, goal 0.12 | miss loop.foreach | ok |
| 3 | `if %errors% is empty` | condition.if | condition.if 0.97 | condition 1.00 | ok | ok |
| 4 | `write out "No errors this week"` | output.write | output.write 0.98, condition.if 0.09 | output 1.00 | ok | ok |
| 5 | `return` | goal.return | goal.return 1.00 (s2, conf 1.00), goal.call 0.30, condition.if 0.07 | goal 1.00 | ok | ok |
| 6 | `if %files% is a list` | condition.if | condition.if 0.97 | condition 1.00 | ok | ok |
| 7 | `write out "Scanned the log files"` | output.write | output.write 0.97, condition.if 0.11 | output 1.00 | ok | ok |
| 8 | `render template "report.html", errors=%errors%, write to %html%` | ui.render, variable.set | ui.render 1.00 (s2, conf 1.00), variable.set 0.61, output.write 0.18, file.read 0.17, condition.if 0.07 | ui 1.00 | miss variable.set | ok |
| 9 | `save %html% to file '/reports/week.html'` | file.save | file.save 1.00 (s2, conf 1.00), variable.set 0.19, output.write 0.05, error.handle 0.05 | file 1.00 | ok | ok |
| 10 | `call SendReport to="ops@example.com", subject="Weekly report", on e…` | goal.call, error.handle | goal.call 0.96, error.handle 0.95, output.write 0.14, variable.set 0.05 | goal 0.70, http 0.29 | ok | ok |
| 11 | `write out "Report sent" to "log" channel` | output.write | output.write 0.97 | output 1.00 | ok | ok |

stage 2 skipped: step 1 variable, step 3 condition, step 4 output, step 6 condition, step 7 output, step 10 goal, step 11 output
