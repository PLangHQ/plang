# Decider v5 — the main module asked by yes/no too; v4 and v5 each run twice

Branch `builder-formal`. The same 5 golden goals, the decider alone, all scored against the current golden.
- Raw (`tools/decider/runs/`):
  - v4 run 2: `decider_eval_20260925_213742.json`
  - v5 run 1: `…_213738.json`
  - v5 run 2: `…_213740.json`
- Requests: `/shared/coder/llm/plang/builder-formal/decider-{v4-run2,v5-run1,v5-run2}/<goal>/`.

## The change

When the main module's share of the "main work" choice is below 0.9, stage 2 asks it the runner-up's yes/no too ("Does step N use the plang module `X`?"). Its action then scores that answer instead of the share. `MAIN_YESNO=0` gives v4 (runner-up only); the v4 repeat was run that way.

```python
    unsure_main = [m for m, p in ranked[:1] if m in main and p < NEAR_CERTAIN and MAIN_YESNO]
    return common, main + runner, unsure_main + runner
# stage 2 — the main module is asked "use", the runner-up "also use":
                also = '' if chosen.get(s['index'], [None])[0] == m else ' also'
```

## Side by side

| | v1 | v2 | v3 | v4 run 1 | v4 run 2 | v5 run 1 | v5 run 2 |
|---|---|---|---|---|---|---|---|
| stage-1 questions | 2,146 | 406 | 406 | 406 | 406 | 406 | 406 |
| stage-1 request KB (sum) | 724 | 149 | 150 | 158 | 158 | 158 | 158 |
| stage-1 seconds (sum; slowest) | 8.2; 1.8 | 4.6; 1.0 | 4.7; 1.1 | 4.8; 1.1 | 5.7; 1.7 | 5.0; 1.2 | 5.0; 1.0 |
| stage-1 tokens in / out | 179k / 39k | 45k / 20k | 45k / 20k | 49k / 20k | 49k / 20k | 49k / 20k | 49k / 20k |
| stage-2 questions (skipped) | 40 (40) | 20 (31) | 41 (30) | 46 (30) | 47 (31) | 53 (29) | 52 (31) |
| stage-2 tokens in / out | 34k / 3.2k | 20k / 1.9k | 26k / 2.6k | 26k / 2.7k | 26k / 2.8k | 27k / 2.9k | 26k / 2.9k |
| **cut 0.9: steps exact** | 39 | 35 | 38 | 39 | 40 | **44** | **43** |
| cut 0.9: hit / missed / extra | 67/21/0 | 64/24/0 | 68/20/0 | 69/19/0 | 70/18/0 | 74/14/0 | 73/15/0 |
| **cut 0.5: steps exact** | 51 | 53 | 53 | **58** | 55 | 57 | 56 |
| cut 0.5: hit / missed / extra | 85/3/5 | 83/5/1 | 85/3/2 | 88/0/0 | 87/1/2 | 88/0/1 | 88/0/2 |

(of 58 steps and 88 expected actions; nothing ≥ 0.9 is wrong in any run)

**v4's 58/58 does not hold:** its second run gives 55/58. v5 gives 57 and 56. At 0.5, v4 and v5 are the same within run noise. At 0.9, v5 is better: 43–44 against 39–40, 4–5 more actions pre-filled, and still none wrong.

## The yes/no scores

| run | yes/no answers | expected modules | not expected |
|---|---|---|---|
| v4 run 1 | 5 | 0.98 | 0.08, 0.13, 0.14, 0.17 |
| v4 run 2 | 6 | 0.90, 0.98 | 0.17, 0.18, 0.18, **0.82** |
| v5 run 1 | 11 | 0.91, 0.92, 0.98 ×5 | 0.10, 0.17, 0.18, 0.20 |
| v5 run 2 | 11 | 0.87, 0.88, 0.97, 0.98 ×4 | 0.17, 0.18, 0.18, **0.85** |

It separates, except one step: decide 9, `call /system/builder/EmitBuildEvent kind="goal-decided", goal=%goal%`. There build.actions scores 0.82 / 0.85 in two runs and 0.14 in another. The word "builder" in the goal's path pulls the build module in.

## What moves between runs (the extras and misses at 0.5)

| step | runs | what |
|---|---|---|
| build 5 `build.load, write to %app%` | v4 r2, v5 r1, v5 r2 (v4 r1: 0.44) | **output.write 0.51–0.69 extra**: "write to" read as output |
| decide 9 `call /system/builder/EmitBuildEvent …` | v4 r2, v5 r2 | build.actions 0.82–0.85 extra (above) |
| decide 6 `llm.decider …, write to %moduleAnswer%` | v4 r2 | variable.set 0.49, missed |

At 0.9, what's left in v5 is **12 write-to → variable.set** in both runs, plus 1–2 others (a goal.call at 0.88, error.throw ×2 in run 2).

## Proposals (prompt-first, not done)

1. **output.write has no `<action>.examples.md`**, so beside its common-action question there's nothing but "Write Data to a named channel". Add step texts, `write out "…"` / `write %x% to "log" channel` / `show %message%`, so "write to %x%" has something to be told apart from. It's the same route as the variable.set step texts.
2. **write-to is the one systematic 0.9 miss** (12 of 14). It is the case the plan's double check exists for: a 0.5–0.9 pick is "possible", and the LLM confirms it from the step's words. I'd stop tuning the decider here and let stage 3 measure it.
3. The "builder" path noise (decide 9) is one step. I'd leave it to stage 3's check too.

## Per step (v5 run 1)

### build

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `set default %path% = "/"` | variable.set | variable.set 0.98 | variable 1.00 | ok | ok |
| 1 | `set default %!build.cache% = true` | variable.set | variable.set 0.98 | variable 1.00 | ok | ok |
| 2 | `set default %!build.summary% = true` | variable.set | variable.set 0.98 | variable 1.00 | ok | ok |
| 3 | `set channel "builder" call BuilderChannel` | channel.set | channel.set 1.00 (s2, conf 1.00), goal.call 0.29, output.write 0.10, variable.set 0.07 | channel 1.00 | ok | ok |
| 4 | `call EmitBuildEvent kind="build-path", path=%path%` | goal.call | goal.call 0.97, output.write 0.35, variable.set 0.11 | goal 0.85, event 0.10 | ok | ok |
| 5 | `build.load, write to %app%` | build.load, variable.set | build.load 1.00 (s2, conf 1.00), variable.set 0.74, output.write 0.59, file.read 0.12 | build 1.00 | miss variable.set | extra output.write |
| 6 | `build.goals path=%path%, write to %goals%` | build.goals, variable.set | build.goals 1.00 (s2, conf 1.00), variable.set 0.63, output.write 0.17, file.read 0.07, goal.call 0.05 | build 1.00 | miss variable.set | ok |
| 7 | `call EmitBuildEvent kind="goals-found", goals=%goals%` | goal.call | goal.call 0.97, output.write 0.46, variable.set 0.06 | goal 0.83, event 0.10, output 0.05 | ok | ok |
| 8 | `foreach %goals%, call BuildGoal goal=%item%` | loop.foreach, goal.call | goal.call 0.98, loop.foreach 0.98, variable.set 0.08, output.write 0.05 | loop 0.75, goal 0.25 | ok | ok |
| 9 | `save %traceGoals% to file '/.build/traces/%!trace.id%/manifest.json'` | file.save | file.save 0.93 (s2, conf 1.00), output.write 0.09, variable.set 0.06 | file 0.93, build 0.07 | ok | ok |
| 10 | `build.appSave` | build.appSave | build.appSave 1.00 (s2, conf 1.00), output.write 0.09, variable.set 0.05, goal.call 0.05 | build 1.00 | ok | ok |

stage 2 skipped: step 0 variable, step 1 variable, step 2 variable, step 4 goal, step 7 goal

### start

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `call /system/builder/EmitBuildEvent kind="goalHeader", goal=%goal%` | goal.call | goal.call 0.95, output.write 0.24, build.actions 0.20, variable.set 0.07 | goal 0.70, build 0.20, event 0.08 | ok | ok |
| 1 | `set %buildStart% = %Now.Ticks%` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 2 | `set %trace% = {"id": "%!trace.id%", "timestamp": "%Now%", "goal": %…` | variable.set | variable.set 0.96, goal.call 0.11 | variable 1.00 | ok | ok |
| 3 | `call Compile, on error call HandleBuildFailure` | goal.call, error.handle | error.handle 0.97, goal.call 0.93, build.validate 0.17 | goal 0.58, build 0.40 | ok | ok |
| 4 | `set %trace.menu% = %menu%` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 5 | `set %parentGoal% = %goal%` | variable.set | variable.set 0.97, goal.call 0.05 | variable 1.00 | ok | ok |
| 6 | `foreach %parentGoal.Child%, call BuildSubGoal subGoal=%item%` | loop.foreach, goal.call | loop.foreach 0.98, goal.call 0.94, variable.set 0.13 | loop 0.71, goal 0.26 | ok | ok |
| 7 | `set %goal% = %parentGoal%` | variable.set | variable.set 0.97 | variable 1.00 | ok | ok |
| 8 | `math.subtract A=%Now.Ticks%, B=%buildStart%, write to %elapsedTicks%` | math.subtract, variable.set | math.subtract 1.00 (s2, conf 1.00), variable.set 0.66, output.write 0.07 | math 1.00 | miss variable.set | ok |
| 9 | `math.divide A=%elapsedTicks%, B=10000, write to %elapsedMs%` | math.divide, variable.set | math.divide 1.00 (s2, conf 1.00), variable.set 0.71, output.write 0.08 | math 1.00 | miss variable.set | ok |
| 10 | `set %trace.durationMs% = %elapsedMs%` | variable.set | variable.set 0.98 | variable 1.00 | ok | ok |
| 11 | `save %trace% to file '/.build/traces/%!trace.id%/%goal.Name%.json'` | file.save | file.save 1.00 (s2, conf 1.00), variable.set 0.21, goal.call 0.05, output.write 0.05, error.handle 0.05 | file 1.00 | ok | ok |
| 12 | `add %goal.Name% to %traceGoals%` | list.add | list.add 1.00 (s2, conf 1.00), variable.set 0.22, goal.call 0.05 | list 1.00 | ok | ok |
| 13 | `build.fold Goal=%goal%, on error call HandleBuildFailure` | build.fold, error.handle | build.fold 1.00 (s2, conf 1.00), error.handle 0.97, goal.call 0.23, variable.set 0.06, output.write 0.05 | build 1.00 | ok | ok |
| 14 | `build.goalsSave Goal=%goal%` | build.goalsSave | build.goalsSave 1.00 (s2, conf 1.00), variable.set 0.06 | build 1.00 | ok | ok |

stage 2 skipped: step 0 goal, step 1 variable, step 2 variable, step 3 goal, step 4 variable, step 5 variable, step 7 variable, step 10 variable

### decide

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `set %threshold% = 0.5` | variable.set | variable.set 0.98 | variable 1.00 | ok | ok |
| 1 | `set %modules% = %!app.module.list%` | variable.set | variable.set 0.95, goal.call 0.07, file.read 0.06, output.write 0.05 | variable 0.85, module 0.08 | ok | ok |
| 2 | `set %state.goal% = %goal.Name%` | variable.set | variable.set 0.96, goal.call 0.08 | variable 0.99 | ok | ok |
| 3 | `foreach %goal.Step%, call AddStepToState item=%step%` | loop.foreach, goal.call | loop.foreach 0.98, goal.call 0.94, variable.set 0.12 | loop 0.66, goal 0.31 | ok | ok |
| 4 | `foreach %modules%, call AddModuleToState item=%module%` | loop.foreach, goal.call | loop.foreach 0.98, goal.call 0.95, variable.set 0.13, output.write 0.05 | goal 0.55, loop 0.38 | ok | ok |
| 5 | `render template "/system/builder/llm/templates/deciderModuleQuestio…` | ui.render, variable.set | ui.render 1.00 (s2, conf 1.00), variable.set 0.72, file.read 0.30, output.write 0.13 | ui 1.00 | miss variable.set | ok |
| 6 | `llm.decider State=%state%, Question=%moduleQuestions%, write to %mo…` | llm.decider, variable.set | llm.decider 1.00 (s2, conf 1.00), variable.set 0.56, output.write 0.08 | llm 1.00 | miss variable.set | ok |
| 7 | `render template "/system/builder/llm/templates/deciderActionQuestio…` | ui.render, variable.set | ui.render 1.00 (s2, conf 1.00), variable.set 0.69, file.read 0.18, output.write 0.11 | ui 1.00 | miss variable.set | ok |
| 8 | `llm.decider State=%state%, Question=%actionQuestions%, write to %ac…` | llm.decider, variable.set | llm.decider 1.00 (s2, conf 1.00), variable.set 0.58, output.write 0.08, goal.call 0.05 | llm 1.00 | miss variable.set | ok |
| 9 | `call /system/builder/EmitBuildEvent kind="goal-decided", goal=%goal%` | goal.call | goal.call 0.95, event.on 0.10, output.write 0.09, variable.set 0.06 | goal 0.58, event 0.23, build 0.19 | ok | ok |

stage 2 skipped: step 0 variable, step 1 variable, step 2 variable, step 4 goal, step 9 goal

### checkout

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `read 'orders/%orderId%.json', write to %order%` | file.read, variable.set | file.read 0.98, variable.set 0.85, output.write 0.07, error.handle 0.05 | file 1.00 | miss variable.set | ok |
| 1 | `count %order.items%, write to %itemCount%` | list.count, variable.set | list.count 0.98 (s2, conf 1.00), variable.set 0.79, output.write 0.06 | list 0.98 | miss variable.set | ok |
| 2 | `if %itemCount% is 0, write out "Your cart is empty", else if %itemC…` | condition.if, condition.elseif, condition.else, output.write | condition.if 0.98, condition.elseif 0.97, output.write 0.96, condition.else 0.95, variable.set 0.07 | condition 0.61, output 0.39 | ok | ok |
| 3 | `if %order.email% does not contain "@", throw "Invalid email address"` | condition.if, error.throw | condition.if 0.97, error.throw 0.92, error.handle 0.31, condition.else 0.05 | error 0.49, condition 0.48 | ok | ok |
| 4 | `if %order.coupon% is not empty, call ApplyCoupon code=%order.coupon…` | condition.if, goal.call | condition.if 0.98, goal.call 0.89 (+s2: module 0.97, conf 1.00), variable.set 0.09, condition.else 0.05 | goal 0.97 | miss goal.call | ok |
| 5 | `if %order.total% is not a number, throw "The total must be a number"` | condition.if, error.throw | condition.if 0.96, error.throw 0.91, error.handle 0.30, variable.set 0.06, condition.else 0.06, output.write 0.05 | error 0.56, condition 0.38, assert 0.05 | ok | ok |
| 6 | `if %order.country% is "IS", set %vat% = 0.24, else set %vat% = 0.25` | condition.if, condition.else, variable.set | condition.if 0.98, variable.set 0.96, condition.else 0.94, condition.elseif 0.18 | variable 0.74, condition 0.26 | ok | ok |
| 7 | `math.multiply A=%order.total%, B=%vat%, write to %vatAmount%` | math.multiply, variable.set | math.multiply 1.00 (s2, conf 1.00), variable.set 0.69, output.write 0.06 | math 1.00 | miss variable.set | ok |
| 8 | `if %order.note% does not start with "#", add %order.note% to %notes%` | condition.if, list.add | condition.if 0.98, list.add 0.96 (s2, conf 1.00), variable.set 0.15, condition.else 0.06 | list 0.96 | ok | ok |
| 9 | `write out "Total: %order.total%, VAT: %vatAmount%"` | output.write | output.write 0.98, variable.set 0.09 | output 1.00 | ok | ok |

stage 2 skipped: step 0 file, step 2 condition, step 6 variable, step 9 output

### weekly_report

| # | step | expected | picks (score) | module choice (p ≥ 0.05) | 0.9 | 0.5 |
|---|---|---|---|---|---|---|
| 0 | `list files in '/logs', pattern "*.log", write to %files%` | file.list, variable.set | file.list 1.00 (s2, conf 1.00), variable.set 0.77, output.write 0.06, file.read 0.06 | file 1.00 | miss variable.set | ok |
| 1 | `set %errors% = []` | variable.set | variable.set 0.97 | variable 0.99 | ok | ok |
| 2 | `foreach %files%, call ScanFile file=%item%` | loop.foreach, goal.call | loop.foreach 0.98, goal.call 0.96, file.read 0.19, variable.set 0.13, output.write 0.05 | loop 0.84, goal 0.16 | ok | ok |
| 3 | `if %errors% is empty` | condition.if | condition.if 0.97, condition.else 0.09, goal.call 0.05, output.write 0.05, condition.elseif 0.05 | condition 1.00 | ok | ok |
| 4 | `write out "No errors this week"` | output.write | output.write 0.98, condition.if 0.09 | output 1.00 | ok | ok |
| 5 | `return` | goal.return | goal.return 1.00 (s2, conf 1.00), goal.call 0.37, condition.if 0.08 | goal 1.00 | ok | ok |
| 6 | `if %files% is a list` | condition.if | condition.if 0.96, condition.else 0.11, goal.call 0.05 | condition 1.00 | ok | ok |
| 7 | `write out "Scanned the log files"` | output.write | output.write 0.96, condition.if 0.12 | output 1.00 | ok | ok |
| 8 | `render template "report.html", errors=%errors%, write to %html%` | ui.render, variable.set | ui.render 1.00 (s2, conf 1.00), variable.set 0.56, output.write 0.17, file.read 0.08, condition.if 0.06, goal.call 0.05 | ui 1.00 | miss variable.set | ok |
| 9 | `save %html% to file '/reports/week.html'` | file.save | file.save 1.00 (s2, conf 1.00), variable.set 0.21, output.write 0.05, error.handle 0.05 | file 1.00 | ok | ok |
| 10 | `call SendReport to="ops@example.com", subject="Weekly report", on e…` | goal.call, error.handle | error.handle 0.96, goal.call 0.91, http.request 0.18, output.write 0.16, variable.set 0.05 | goal 0.62, http 0.36 | ok | ok |
| 11 | `write out "Report sent" to "log" channel` | output.write | output.write 0.97 | output 1.00 | ok | ok |

stage 2 skipped: step 1 variable, step 3 condition, step 4 output, step 6 condition, step 7 output, step 10 goal, step 11 output

## Addendum — output.write example step texts (architect's call)

`os/system/modules/output/write.examples.md` is new. It holds step texts only, worded away from the golden steps:

```
Step text: `write out "Welcome back, %user.name%"`
Step text: `show %message% to the user`
Step text: `print %result%`
Step text: `write %error.Message% to "errors" channel`
```

v5, 1 run (`tools/decider/runs/decider_eval_20260925_214621.json`; requests in `/shared/coder/llm/plang/builder-formal/decider-v5-outexamples/`):
- cut 0.5: **58/58** steps exact, 0 missed, 0 extra;
- cut 0.9: 44/58.

build 5 `build.load, write to %app%` → output.write is now **0.38** (0.51–0.69 in 3 of the 4 earlier runs), so it's no longer an extra. The decide 9 "builder" path didn't fire this run. With one run, this is a single sample.
