# Stage 1 of builder-formal — the decider with common actions and scores

Branch `builder-formal`. One run of the decider alone (stages 1 and 2, no stage-3 call) on the 5 golden goals (58 steps):
- Raw data: `tools/decider/runs/decider_eval_20260925_162944.json`.
- Every request and response: `/shared/coder/llm/plang/builder-formal/decider/<goal>/{1,2}.decider.{request,response}.json`. Beside each are the readable `state.txt`, `questions.json` and `answers.json`.
- The expected actions per step are the golden's menus: every action the step uses, with its conditions' bodies and modifiers (`tools/decider/child_golden.json`).

**One run can't show the decider's variance.** A first run (discarded because of a scoring bug of mine, fixed below) gave `goal.call` 0.46 where this run gives 0.60 for the same step.

## Result

| cut | steps with exactly the expected actions | actions hit | missed | extra |
|---|---|---|---|---|
| **0.9** (pre-filled) | 37/58 | 67/90 | 23 | **0** |
| **0.5** (pre-filled + possible) | **53/58** | 87/90 | 3 | 3 |

- **Above 0.9, every pick is right** (0 extras). It is a safe cut for pre-filling, but it holds only 74% of the actions.
- **The 0.5–0.9 band holds 23 picks: 20 right, 3 wrong.** "Possible" is the right name for it.
- **The 3 misses at 0.5 are all `condition.else` / `condition.elseif`.** That's structural (see finding 2), not a score.
- **Nothing expected scored below 0.5** in this run. So under ruling 4 no step would fail loudly; in the first run, `set channel "builder" call BuilderChannel` → goal.call at 0.46 would have.

## Cost

| | questions | requests | seconds (sum; slowest goal) | tokens in / out |
|---|---|---|---|---|
| stage 1 | 2,146 (37 per step: 31 modules + 6 common actions) | 5 (one per goal) | 8.2; 1.8 | 179,471 / 39,408 |
| stage 2 | 40 | 5 | 4.2; 0.9 | 34,005 / 3,161 |
| **stage-2 questions skipped** | **40** (a near-certain common action answered the module) | | | |

Half of stage 2 is skipped. Stage 2 costs about 16% of stage 1's tokens, so skipping it saves less than it sounds.

- **USD is not known.** The typesafe service reports tokens only, and no price for it exists in the repo or in /shared. Give me the price and I'll add the column.
- Stage 1 grew by 5 questions per step: 6 common-action questions instead of the one `@store` question.

## The common actions — counted

Each count is the share of steps that use the action, over 2,818 steps: tools/decider/labels/ (1,033 .pr, 2,701 steps), the builder's .pr files (59 steps) and the golden set (58):

```
action                    all  %steps |   labels  builder   golden
variable.set             1178   41.8% |     1124       27       27
assert.equals             460   16.3% |      460        0        0
goal.call                 246    8.7% |      215       17       14
output.write              179    6.4% |      171        3        5
error.handle              176    6.2% |      168        5        3
assert.isTrue             124    4.4% |      124        0        0
condition.if              119    4.2% |      110        1        8
assert.isNotNull           98    3.5% |       98        0        0
identity.archive           77    2.7% |       77        0        0
file.read                  64    2.3% |       63        0        1
```

**Picked (6):** variable.set, goal.call, output.write, error.handle, condition.if, file.read.
- The assert.* actions are left out: they rank high only because the labels are mostly tests, and a non-test goal never uses them. Asking them only for `.test.goal` files would be a later refinement.
- list.add (in the plan's guess) is not in the top 16.
- identity.archive is one test family.

## The code

`tools/decider/harness.py`: the common actions are asked by name beside the modules. `variable.set` replaces the `@store` question; the module question lost its "(asked separately)" tail.

```python
COMMON = ['variable.set', 'goal.call', 'output.write', 'error.handle', 'condition.if', 'file.read']
NEAR_CERTAIN = 0.9   # a common action scored at or above this is picked; its module skips stage 2

        for s in win:
            for a in COMMON:
                m, an = a.split('.', 1)
                qs[f's{s["index"]}_{a}'] = {'type': 'noul', 'instructions':
                    f'Step {step_no(s)} of this goal is `{s["text"].strip()}`. Does step {step_no(s)} — its own work, or '
                    f'anything it guards behind a condition, repeats in a loop, or hands to an error handler — use the '
                    f'action `{a}`: {cat[m]["actions"][an]["description"]}?'}

def picks(probs_i, cat, threshold=0.5):
    """One step's stage-1 answer as (common-action picks {action: score}, modules stage 2 must still ask).
    A module is skipped in stage 2 when one of its common actions is near-certain: that action is its answer."""
    common = {a: probs_i.get(a) for a in COMMON if probs_i.get(a) is not None}
    settled = {a.split('.', 1)[0] for a, p in common.items() if p >= NEAR_CERTAIN}
    ask2 = [m for m, p in probs_i.items() if m in cat and p is not None and p >= threshold and m not in settled]
    return common, ask2
```

Every score is kept: `probs[i]` holds all 31 module scores and all 6 common-action scores, whatever their value.
- `build_pr.menu_for` puts a common action scored ≥ 0.5 on the menu.
- `report.py` reads either `@store` (older runs) or `variable.set`.
- The eval is `tools/decider/decider_eval.py`. A pick's score is:
  - a common action's own stage-1 noul (when stage 2 names it too, its module noul and the choice's confidence ride beside it);
  - any other action's module noul, with the choice's confidence.

## Findings (prompt-first)

1. **`write to %x%` → variable.set is under-scored.** Over the 13 steps with a trailing `write to`, it scores 0.50, 0.59, 0.60, 0.61, 0.63, 0.65, 0.66, 0.67, 0.74, 0.79, 0.85, 0.86, 0.90. Over the 13 `set %x% = …` steps it scores 0.93–0.98. This is 12 of the 23 misses at 0.9.
   - **Cause (teaching):** `variable/set.examples.md` has 10 step texts, all `set %x% = …`, none `…, write to %x%`. The common question shows the action's description ("Assign a value to a named variable…"), and a trailing "write to" doesn't read as that.
   - **Proposal:** add `write to %x%` step texts to variable/set.examples.md (ruling 4's route: the decider's teaching). Then give each common-action question its own example steps, as the plan has stage 2 do. Re-measure.
2. **else / elseif can't be picked.** Stage 2 asks one choice per (step, module), so a step using two actions of one module (`condition.if` + `condition.else`) can only get one. With condition.if ≥ 0.9, the condition module isn't asked at all. That's the 3 misses at 0.5 (checkout steps 2 and 6). This predates the change: stage 2 always gave one action per module.
   - **Proposal:** when condition.if is picked (≥ 0.5), ask noul for `condition.elseif` and `condition.else` on that step. Two questions, in stage 1's request or as a stage-2 follow-up. Generally: a module whose actions combine in one step asks noul per action, not a choice.
3. **The remaining 0.9 misses are near-certain actions at 0.81–0.89:**
   - error.handle 0.87 / 0.89 (the `on error` steps)
   - goal.call 0.88 (EmitBuildEvent), 0.63 (the call inside `on error`)
   - output.write 0.86 (an indented `write out`)
   - file.save 0.81, list.count 0.82 (module noul)
   
   They are all right at 0.5. Ruling 2 says 0.9 is set from measured calibration. On this run, 0.8 would have pre-filled 13 more right actions and 0 wrong ones. One run isn't enough to move it; I'd re-measure after finding 1.
4. **The extras at 0.5** (each would be "possible", never pre-filled):
   - variable.get 0.54 / 0.50 on `save %trace% to file …` and `add %goal.Name% to %traceGoals%` (stage 2 answering the variable module);
   - list.add 0.54 on `foreach %modules%, call AddModuleToState item=%module%`.
5. **`set channel "builder" call BuilderChannel` → goal.call** scored 0.46, then 0.60. The golden expects it because channel.set's Goal property holds a goal.call.
   - The decider's STRUCTURE teaching says the opposite: "Naming a goal to be run later is not calling it" (harness.py:204). The decider is following its teaching, so the golden and the teaching disagree.
   - **Decide which is right.** If the Goal value is the LLM's to write inside channel.set, goal.call isn't a pick of its own, and I'd drop it from the golden menu.

## Per step

Scores ≥ 0.05. `(s2, conf c)` = a stage-2 pick (score = its module's noul). `(+s2: …)` = a common action stage 2 also named. The last two columns are the verdict at 0.9 and at 0.5.

### build

| # | step | expected | picks (score) | 0.9 | 0.5 |
|---|---|---|---|---|---|
| 0 | `set default %path% = "/"` | variable.set | variable.set 0.97 | ok | ok |
| 1 | `set default %!build.cache% = true` | variable.set | variable.set 0.96 | ok | ok |
| 2 | `set default %!build.summary% = true` | variable.set | variable.set 0.96 | ok | ok |
| 3 | `set channel "builder" call BuilderChannel` | channel.set, goal.call | channel.set 0.98 (s2, conf 1.00), goal.call 0.60, output.write 0.11, variable.set 0.06 | miss goal.call | ok |
| 4 | `call EmitBuildEvent kind="build-path", path=%path%` | goal.call | goal.call 0.96, output.write 0.34, variable.set 0.05 | ok | ok |
| 5 | `build.load, write to %app%` | build.load, variable.set | build.load 0.98 (s2, conf 1.00), variable.set 0.61 (+s2: module 0.80, conf 1.00), output.write 0.30, file.read 0.19 | miss variable.set | ok |
| 6 | `build.goals path=%path%, write to %goals%` | build.goals, variable.set | build.goals 0.98 (s2, conf 1.00), variable.set 0.65 (+s2: module 0.83, conf 0.99), output.write 0.12, file.read 0.11, goal.call 0.07 | miss variable.set | ok |
| 7 | `call EmitBuildEvent kind="goals-found", goals=%goals%` | goal.call | goal.call 0.97, output.write 0.36, variable.set 0.05 | ok | ok |
| 8 | `foreach %goals%, call BuildGoal goal=%item%` | loop.foreach, goal.call | goal.call 0.98, loop.foreach 0.92 (s2, conf 1.00), variable.set 0.07, output.write 0.06 | ok | ok |
| 9 | `save %traceGoals% to file '/.build/traces/%!trace.id%/manifest.json'` | file.save | file.save 0.81 (s2, conf 1.00), output.write 0.06, variable.set 0.05, goal.call 0.05 | miss file.save | ok |
| 10 | `build.appSave` | build.appSave | build.appSave 0.99 (s2, conf 1.00), output.write 0.08, variable.set 0.05 | ok | ok |

stage 2 skipped: step 0 variable, step 1 variable, step 2 variable, step 4 goal, step 7 goal, step 8 goal

### start

| # | step | expected | picks (score) | 0.9 | 0.5 |
|---|---|---|---|---|---|
| 0 | `call /system/builder/EmitBuildEvent kind="goalHeader", goal=%goal%` | goal.call | goal.call 0.88 (+s2: module 0.93, conf 1.00), output.write 0.09, variable.set 0.06, error.handle 0.05 | miss goal.call | ok |
| 1 | `set %buildStart% = %Now.Ticks%` | variable.set | variable.set 0.97 | ok | ok |
| 2 | `set %trace% = {"id": "%!trace.id%", "timestamp": "%Now%", "goal": %…` | variable.set | variable.set 0.94, goal.call 0.08 | ok | ok |
| 3 | `call Compile, on error call HandleBuildFailure` | goal.call, error.handle | goal.call 0.95, error.handle 0.89 (+s2: module 0.92, conf 1.00), variable.set 0.06, output.write 0.05, file.read 0.05 | miss error.handle | ok |
| 4 | `set %trace.menu% = %menu%` | variable.set | variable.set 0.96 | ok | ok |
| 5 | `set %parentGoal% = %goal%` | variable.set | variable.set 0.98 | ok | ok |
| 6 | `foreach %parentGoal.Child%, call BuildSubGoal subGoal=%item%` | loop.foreach, goal.call | goal.call 0.97, loop.foreach 0.92 (s2, conf 1.00), variable.set 0.09, error.handle 0.06 | ok | ok |
| 7 | `set %goal% = %parentGoal%` | variable.set | variable.set 0.98 | ok | ok |
| 8 | `math.subtract A=%Now.Ticks%, B=%buildStart%, write to %elapsedTicks%` | math.subtract, variable.set | math.subtract 0.99 (s2, conf 1.00), variable.set 0.60 (+s2: module 0.75, conf 0.95), output.write 0.06 | miss variable.set | ok |
| 9 | `math.divide A=%elapsedTicks%, B=10000, write to %elapsedMs%` | math.divide, variable.set | math.divide 0.99 (s2, conf 1.00), variable.set 0.66 (+s2: module 0.84, conf 0.95), output.write 0.06 | miss variable.set | ok |
| 10 | `set %trace.durationMs% = %elapsedMs%` | variable.set | variable.set 0.98 | ok | ok |
| 11 | `save %trace% to file '/.build/traces/%!trace.id%/%goal.Name%.json'` | file.save | file.save 0.94 (s2, conf 1.00), variable.get 0.54 (s2, conf 0.78), variable.set 0.10, goal.call 0.06, error.handle 0.06 | ok | extra variable.get |
| 12 | `add %goal.Name% to %traceGoals%` | list.add | list.add 0.97 (s2, conf 1.00), variable.get 0.50 (s2, conf 0.59), variable.set 0.14, goal.call 0.05 | ok | extra variable.get |
| 13 | `build.fold Goal=%goal%, on error call HandleBuildFailure` | build.fold, error.handle, goal.call | build.fold 0.96 (s2, conf 1.00), error.handle 0.90, goal.call 0.63, variable.set 0.10, file.read 0.08, output.write 0.05 | miss goal.call | ok |
| 14 | `build.goalsSave Goal=%goal%` | build.goalsSave | build.goalsSave 0.98 (s2, conf 1.00), variable.set 0.07, goal.call 0.07, output.write 0.05, error.handle 0.05, file.read 0.05 | ok | ok |

stage 2 skipped: step 1 variable, step 2 variable, step 3 goal, step 4 variable, step 5 variable, step 6 goal, step 7 variable, step 10 variable, step 13 error

### decide

| # | step | expected | picks (score) | 0.9 | 0.5 |
|---|---|---|---|---|---|
| 0 | `set %threshold% = 0.5` | variable.set | variable.set 0.98 | ok | ok |
| 1 | `set %modules% = %!app.module.list%` | variable.set | variable.set 0.93, goal.call 0.14, output.write 0.05 | ok | ok |
| 2 | `set %state.goal% = %goal.Name%` | variable.set | variable.set 0.96, goal.call 0.05 | ok | ok |
| 3 | `foreach %goal.Step%, call AddStepToState item=%step%` | loop.foreach, goal.call | loop.foreach 0.93 (s2, conf 1.00), goal.call 0.90, variable.set 0.09 | ok | ok |
| 4 | `foreach %modules%, call AddModuleToState item=%module%` | loop.foreach, goal.call | loop.foreach 0.94 (s2, conf 1.00), goal.call 0.93, list.add 0.54 (s2, conf 0.55), variable.set 0.07 | ok | extra list.add |
| 5 | `render template "/system/builder/llm/templates/deciderModuleQuestio…` | ui.render, variable.set | ui.render 0.97 (s2, conf 1.00), variable.set 0.74 (+s2: module 0.75, conf 0.98), file.read 0.35, output.write 0.07, goal.call 0.05 | miss variable.set | ok |
| 6 | `llm.decider State=%state%, Question=%moduleQuestions%, write to %mo…` | llm.decider, variable.set | llm.decider 0.98 (s2, conf 1.00), variable.set 0.50 (+s2: module 0.58, conf 0.59), goal.call 0.06, output.write 0.06 | miss variable.set | ok |
| 7 | `render template "/system/builder/llm/templates/deciderActionQuestio…` | ui.render, variable.set | ui.render 0.97 (s2, conf 1.00), variable.set 0.67 (+s2: module 0.76, conf 0.98), file.read 0.25, output.write 0.06, goal.call 0.05 | miss variable.set | ok |
| 8 | `llm.decider State=%state%, Question=%actionQuestions%, write to %ac…` | llm.decider, variable.set | llm.decider 0.98 (s2, conf 1.00), variable.set 0.59 (+s2: module 0.59, conf 0.79), output.write 0.07, goal.call 0.05 | miss variable.set | ok |
| 9 | `call /system/builder/EmitBuildEvent kind="goal-decided", goal=%goal%` | goal.call | goal.call 0.93, output.write 0.10, variable.set 0.05 | ok | ok |

stage 2 skipped: step 0 variable, step 1 variable, step 2 variable, step 3 goal, step 4 goal, step 9 goal

### checkout

| # | step | expected | picks (score) | 0.9 | 0.5 |
|---|---|---|---|---|---|
| 0 | `read 'orders/%orderId%.json', write to %order%` | file.read, variable.set | file.read 0.96, variable.set 0.90, output.write 0.06 | ok | ok |
| 1 | `count %order.items%, write to %itemCount%` | list.count, variable.set | variable.set 0.86 (+s2: module 0.91, conf 0.97), list.count 0.82 (s2, conf 1.00), output.write 0.08, goal.call 0.05, file.read 0.05 | miss list.count, variable.set | ok |
| 2 | `if %itemCount% is 0, write out "Your cart is empty", else if %itemC…` | condition.if, condition.elseif, condition.else, output.write | output.write 0.95, condition.if 0.95, variable.set 0.07 | miss condition.else, condition.elseif | miss condition.else, condition.elseif |
| 3 | `if %order.email% does not contain "@", throw "Invalid email address"` | condition.if, error.throw | error.throw 0.92 (s2, conf 1.00), condition.if 0.91, output.write 0.09, variable.set 0.06, error.handle 0.06 | ok | ok |
| 4 | `if %order.coupon% is not empty, call ApplyCoupon code=%order.coupon…` | condition.if, goal.call | condition.if 0.95, goal.call 0.94, variable.set 0.10, output.write 0.06, file.read 0.05 | ok | ok |
| 5 | `if %order.total% is not a number, throw "The total must be a number"` | condition.if, error.throw | condition.if 0.92, error.throw 0.91 (s2, conf 1.00), output.write 0.10, variable.set 0.07, error.handle 0.06 | ok | ok |
| 6 | `if %order.country% is "IS", set %vat% = 0.24, else set %vat% = 0.25` | condition.if, condition.else, variable.set | variable.set 0.98, condition.if 0.97 | miss condition.else | miss condition.else |
| 7 | `math.multiply A=%order.total%, B=%vat%, write to %vatAmount%` | math.multiply, variable.set | math.multiply 0.99 (s2, conf 1.00), variable.set 0.79 (+s2: module 0.84, conf 0.76), output.write 0.08 | miss variable.set | ok |
| 8 | `if %order.note% does not start with "#", add %order.note% to %notes%` | condition.if, list.add | list.add 0.95 (s2, conf 1.00), condition.if 0.94, variable.set 0.10 | ok | ok |
| 9 | `write out "Total: %order.total%, VAT: %vatAmount%"` | output.write | output.write 0.94, variable.set 0.09 | ok | ok |

stage 2 skipped: step 0 file, step 0 variable, step 2 output, step 2 condition, step 3 condition, step 4 goal, step 4 condition, step 5 condition, step 6 condition, step 6 variable, step 8 condition, step 9 output

### weekly_report

| # | step | expected | picks (score) | 0.9 | 0.5 |
|---|---|---|---|---|---|
| 0 | `list files in '/logs', pattern "*.log", write to %files%` | file.list, variable.set | file.list 0.99 (s2, conf 1.00), variable.set 0.85 (+s2: module 0.93, conf 0.99), output.write 0.07 | miss variable.set | ok |
| 1 | `set %errors% = []` | variable.set | variable.set 0.98 | ok | ok |
| 2 | `foreach %files%, call ScanFile file=%item%` | loop.foreach, goal.call | goal.call 0.96, loop.foreach 0.94 (s2, conf 1.00), file.read 0.31, variable.set 0.12, output.write 0.06, error.handle 0.06, condition.if 0.05 | ok | ok |
| 3 | `if %errors% is empty` | condition.if | condition.if 0.93, output.write 0.48, goal.call 0.10, variable.set 0.07 | ok | ok |
| 4 | `write out "No errors this week"` | output.write | output.write 0.90, condition.if 0.18, goal.call 0.08, variable.set 0.06 | ok | ok |
| 5 | `return` | goal.return | goal.return 0.92 (s2, conf 1.00), goal.call 0.36, condition.if 0.13, output.write 0.07, variable.set 0.05 | ok | ok |
| 6 | `if %files% is a list` | condition.if | condition.if 0.93, output.write 0.33, goal.call 0.09, variable.set 0.07 | ok | ok |
| 7 | `write out "Scanned the log files"` | output.write | output.write 0.86 (+s2: module 0.96, conf 1.00), condition.if 0.22, variable.set 0.07, goal.call 0.06 | miss output.write | ok |
| 8 | `render template "report.html", errors=%errors%, write to %html%` | ui.render, variable.set | ui.render 0.97 (s2, conf 1.00), variable.set 0.63 (+s2: module 0.81, conf 0.51), file.read 0.30, output.write 0.09, condition.if 0.08, goal.call 0.07, error.handle 0.07 | miss variable.set | ok |
| 9 | `save %html% to file '/reports/week.html'` | file.save | file.save 0.96 (s2, conf 1.00), variable.set 0.10, goal.call 0.06, error.handle 0.06, output.write 0.05 | ok | ok |
| 10 | `call SendReport to="ops@example.com", subject="Weekly report", on e…` | goal.call, error.handle | goal.call 0.94, error.handle 0.87 (+s2: module 0.92, conf 1.00), output.write 0.18, variable.set 0.08, condition.if 0.06 | miss error.handle | ok |
| 11 | `write out "Report sent" to "log" channel` | output.write | output.write 0.93, error.handle 0.08, goal.call 0.07, variable.set 0.05 | ok | ok |

stage 2 skipped: step 1 variable, step 2 goal, step 3 condition, step 4 output, step 6 condition, step 10 goal, step 11 output
