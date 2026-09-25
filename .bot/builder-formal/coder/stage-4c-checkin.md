# Stage 4c check-in — the builder goals on the new pipeline

Branch `builder-formal`. Traced, nothing built. The blocker comes first; the shape follows.

## The blocker: the builder can't run itself yet

- `os/system/builder/BuildGoal/.build/start.pr` is an older build. It points at `BuildStep/Start` and `BuildGoal/.build/plan.pr`, and has no Compile, Menu, Settle, Apply or FixProperties.
- There is no `decide.pr` or `properties.pr`. The builder's own `.pr` files are in the old format, which the reader now refuses.
- So no 4c piece can be run end to end until the bootstrap (4d). Each piece is proven the way 4a/4b were:
  - C# tests on the owners;
  - **template twin tests**: every template's rendering for the 5 golden goals is compared byte for byte with the python request the eval sends (the decider questions per stage, prompt C's user message). The fixture is written by python, like `formal_golden.json`.

## Today, file:line (against the python pipeline)

| Step | plang today | python (the measured pipeline) |
|---|---|---|
| entry | `Build.goal:14` foreach goal → `BuildGoal.goal:4` → `BuildGoal/Start.goal` | `build_pr.build_goal` |
| decide | `Decide.goal:18-31`:<br>stage 1 = a **noul per (step, module)** (`deciderModuleQuestions.template:13-24`);<br>stage 2 = a choice of action where noul ≥ 0.5 (`deciderActionQuestions.template:18-29`) | decider v5:<br>`harness.stage1:294` (per step one module **choice** + a noul per COMMON action, with criteria);<br>`main_module:321`, `unsure:335`, `RUNNER_UP:340`, `picks:343`;<br>`stage2:363` (action choice, runner-up and main yes/no, else/elseif by name, popular choice when unsure) |
| menu | `Start.goal:41-49` Menu/MenuModule: noul ≥ threshold, then module.action | `prompt_c.listed:25` (≥ 0.5, write-to known, popular ≥ 0.2) |
| stage 3 | `Properties.goal:12-17`: `propertiesUser.template` (F1 catalogue walk :15-19) + `Properties.llm` + `Properties.schema` → JSON `{step:[{index, action:[…]}]}` | `prompt_c.user_message_c:68` + `PropertiesC.llm`, **text** answer `[i] …` |
| match | `Start.goal:35` `build.match` → `step/list/this.cs:57-106` checks the JSON entries (AnswerMismatch) | `formal.parse_steps`: one refusal per step that doesn't parse, whole-answer problems |
| graft | `Start.goal:61` Apply `set %goal.step[i].code% = %properties.step[i].action%` → `step/this.Item.cs:30-50` reads the rows | (the parse gives the rows) |
| checks | `Start.goal:62` `build.validate` → `Default.cs:251-283`: catalogue defaults into Default, `step.Validate`, `Code.Build`; `action.list.Chain` (list/this.cs:86) | `prompt_c.check:320`: drop nulls/defaults, fold, chain, agreement, variable/literal/number coverage, recovery ≠ wrapped action |
| retry | `Start.goal:56` Settle on error → FixProperties (:65-71): the whole answer again, JSON wording | the refused steps only, asked again alone |
| fold | `Start.goal:22` `build.fold` → `Default.cs:137-193` | `prompt_c.fold:190` (a copied body is dropped) |
| write | `Start.goal:23` `build.goalsSave` → `Default.cs:195-247`: `NestRecursive` (:219), Validate, `plang.Text` | — |

## The new shape, per goal

### Decide.goal: decider v5, in templates

```
Decide
- set %modules% = %!app.module.list%
- set %state.goal% = %goal.Name% / foreach step (AddStepToState step=%item%) / foreach module (AddModuleToState module=%item%)
- render "/system/builder/llm/templates/decider1.template", write to %q1%      / stage 1: per step the module choice (criteria = module → description) + a noul per COMMON action (with its criteria)
- llm.decider State=%state%, Question=%q1%, write to %a1%
- render "/system/builder/llm/templates/decider2.template", write to %q2%      / stage 2, from %a1%: action choice of the main module, runner-up (≥ 0.2) and main yes/no,
                                                                               /   else/elseif by name, the popular choice on an unsure step (best < 0.8)
- llm.decider State=%state%, Question=%q2%, write to %a2%
- build.pick Goal=%goal%, First=%a1%, Second=%a2%                              / each step's picks (see "homes")
```

- The **questions** are the templates (Liquid can do the stage-1 → stage-2 selection: it filters, compares and reads `probabilities`).
- The **picks** (what a step's scores mean: ≥ 0.5, write-to known, NEAR_CERTAIN, popular ≥ 0.2) are C#, because both the prompt and the checks read them. That's one owner, not Liquid plus a C# copy.

### Properties.goal: prompt C, a text answer

```
Properties
- render "/system/builder/llm/templates/propertiesC.template", write to %user%   / goal as written, one line per step: => decider: => formal: ; Types; each action once
- read "/system/builder/llm/PropertiesC.llm", write to %system%
- llm.query system: %system%, user: %user%, model "gpt-5.4-nano", write to %answer%    / no Schema: the answer is text
```

- The template reads each step's picks (`step.Pick.Listed`, `step.Pick.Formal`), not the catalogue, so **F1 dies**.

### Start.goal → Compile → Settle: parse, match, checks, fold, write `code`

```
Compile
- call Decide
- call Properties
- build.match Goal=%goal%, Answer=%answer%, on error call FixSteps, then retry 1 times
- build.fold Goal=%goal%
Start (unchanged tail): … build.goalsSave Goal=%goal%

FixSteps
/ the refused steps only, asked again alone; the rest of the answer stands
- set %ask% = "Steps %!error.steps% were refused: %!error.Message%\n\nAnswer again only these steps, one line each, starting with its [i], in formal."
- llm.query … continuePreviousConversation=true, write to %answer%
```

- `build.match` reads the text:
  - `[i]` per step, then `Formal(step).Read`, then **every** check per step;
  - it sets `step.Code` on the steps that pass;
  - refused steps are ONE error, with each step's lines, and the error names the steps.
- On retry it reads only those steps' lines and **merges** them (the others keep their code).
- The graft (`Start.goal:61`), Apply, Menu and MenuModule go.

## Where each check lives (C#; none python-only)

| Check (python) | C# owner |
|---|---|
| `[i]` lines, a missing/extra/twice step (`parse_steps`) | `goal.step.list`: `Read(text)` (replaces `Match(entries)`) |
| formal syntax | `action.serializer.Formal` (4a) |
| chain + empty body (`chain`) | `action.list.Chain` (exists) |
| null on an optional property / value equal to its default (S1) | the action: `this.Build.cs`, where `build.validate` already moves catalogue defaults |
| a Recovery that runs the action it wraps | the modifier (`action.modifier`), in its Build |
| variable, literal, number coverage (text ↔ code) | the step: `step.Validate` (`this.Validate.cs`), since it owns its Text and its Code |
| agreement with the decider (certain pick missing, unlisted action naming the list, held ≠ goal.call, possible/popular warnings) | **new `goal/step/pick/list/this.cs`**: the step's picks (score per action, popular), `Listed` (the 0.5 / write-to / 0.2 floor), `Formal` (the pre-fill), and `Agree(code)` giving refusals and warnings. It's held on the step, build-time only (not `[Store]`, like PriorText) |
| fold drops a copied body | `build.fold` (`Default.cs:137`), where the body over indented steps is placed |

## What dies

- `build.match`'s JSON path: `step/list/this.cs:57-106` `Match(entries)`. The action reader (`action/serializer/Reader.cs`) is then `.pr`-only, and its `default: Skip()` refuses unknown keys.
- `step.Nest` (`step/this.cs:67-109`), `goal.NestRecursive` (`goal/this.cs:362-367`) and the call at `Default.cs:219`.
- Prompts A/B:
  - `Properties.llm`, `Properties.schema`, `propertiesUser.template` (with the F1 walk :15-19);
  - `PropertiesB.llm`, `propertiesUserB.template`.
- The old decider templates `deciderModuleQuestions.template` and `deciderActionQuestions.template` (replaced by decider1/decider2).
- `Start.goal`: Menu, MenuModule, Settle/Apply, `%properties.step[step.Index].action%` (:61), and FixProperties (becomes FixSteps).
- `build.merge` (no caller; its doc names a nonexistent `ApplyStep.goal`). Delete it, or keep it for the Keep/PriorText path in 4d?

## Blockers and open points

1. **The bootstrap** (above): 4c is proven with C# tests and template twins, not a plang run.
2. **Template twins need python renderings as fixtures.** `c_eval` writes them already (`/shared/coder/2.0/<goal>/…`); I'd add a fixture writer to the repo, like `formal_fixture.py`.
3. **Liquid for stage 2** (main module, runner-up, unsure, conditions from `%a1%` probabilities) is the most logic in a template. If the twin shows it's unreadable, the stage-2 question selection moves to `pick.list` too (C#), and the template only prints.
4. **FixSteps' merge**: `build.match` on retry has to know it's a partial answer. That's either an argument (`Only=%!error.steps%`) or the list remembers which steps are still open. I'd make it the list's own state (the refused steps), so no flag.
5. `build.merge`: delete, or keep for 4d?

## Your calls

1. `pick.list` as the owner of the picks (listing, pre-fill, agreement), with `build.pick` making it from the two decider answers?
2. Coverage on `step.Validate`, S1 on the action's Build, and the recovery rule on the modifier?
3. `build.match` reads text, merges retries, and gives one error for the refused steps?
4. Template twin tests as the proof until 4d?
5. `build.merge`: delete now?
