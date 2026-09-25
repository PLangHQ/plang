# Builder map — how `plang build` turns a `.goal` into a `.pr`

Read-only study at `b7b23509b` (get-builder-running), 2026-09-25. Nothing was run, built or called.

Legend: **✔ read** = I read the line(s) cited. **~ inferred** = follows from what I read, not observed running.

---

## 0. Headline findings (details in §6)

| # | Finding | |
|---|---|---|
| A | **`llm.decider` has no provider.** `TypeSafe : IDecider` exists (`llm/code/TypeSafe.cs:14`), but `RegisterDefaults` (`action/code/this.cs:247-266`) registers no `IDecider`, and nothing else does. Stage 1 fails with "No IDecider provider registered" (`code/this.cs:73`). | ✔ read (failure ~) |
| B | **The live `BuildGoal/.build/start.pr` is a different, older builder.** It runs `call Plan` → `render Compile.llm` → `foreach %plan.steps%, call BuildStep/Start`. `Plan`, `BuildStep/` and `Compile.llm` no longer exist. The current `Start.goal` runs Decide → Menu → Properties → Settle. Renaming the keys (`pr_bootstrap.py`) would load it and then fail at `call Plan`. | ✔ read |
| C | **`Decide.goal` and `Properties.goal` have no `.pr`.** `BuildGoal/.build/` holds only `start.pr`. | ✔ read |
| D | **Modifiers can never reach the plang builder's menu.** `module.Action` excludes modifiers (`module/this.cs:67,73-76`). `error.handle`, `cache.wrap` and `timeout.after` are all `[Modifier]`. So `on error call X` gets `error.throw` (error's only standalone action), and `timeout` is never on a menu. The builder's own goals use `on error … retry` three times. | ✔ read (effect ~) |
| E | **The stage-3 schema has no `child`.** An inline `if X, return` step can only come back as sibling actions, and a sibling runs when the condition is FALSE. `build.fold` folds only *indented* sub-steps. `MenuModule` is written like that (`Start.goal:43-45`). | ✔ read (effect ~) |
| F | **`build_pr.py` writes no hash.** `description.md` says it writes fresh hashes. It also writes no `prPath`/visibility/flags and no `indent`, so nothing is folded. | ✔ read |
| G | **Moving `Tests/.build/` removed `Tests/.build/app.pr`.** `build.RunAsync` requires `/.build/app.pr` (`build/this.cs:78-87`). The repro `cd Tests && plang build …` now stops at `NoAppFound` (headless) unless `--app={"create":true}`. | ✔ read |

---

## 1. Entry to exit

### 1a. C#: CLI → the first builder goal (all ✔ read)

```
PlangConsole/Program.cs:18-19        new Executor(cwd).Run(args)
PLang/Executor.cs:29  Configure(args)
  :33-34   "build" → ["--build", <rest>]
  :36      CommandLineParser.Parse → Parameters["!build"] = dict   (CommandLineParser.cs:61-71)
  :42      new app.@this(startupDirectory)
  :107-110 "!build" present → app.Build = new module.action.build.@this(System.Context)
  :111-112 %path% = startupDirectory (unless passed)
  :116     app.Setting.Set(app.Build, buildDict)       → Files, Cache (build/this.cs:25,30)
  :121     %!build.cache% = app.Build.Cache
  :129-131 cache:false → in-memory setting llm.cache=false
PLang/app/this.cs:490  Start() → Launch()
  :538     if (Mode == Build) return await Build!.RunAsync();     // Mode.Build ⇔ Build != null (:196-199)
PLang/app/module/action/build/this.cs:76  RunAsync()
  :78-106  /.build/app.pr must exist in the app, else NoAppFound (headless) / y-n prompt
  :112-113 App.Goal.Load("/system/builder/.build/build.pr")   // /system → <bin>/os/system (path/file/this.Validate.cs:50-58)
  :114     !loaded.Success → return   ◄── TODAY: PrFormatOutdated, build.pr:22 "parameter"
  :115     goal.Run(User.Context)
```

The `--build` settings object is the build module instance itself. It has only two settable fields:

| Field | Where | Default | Used by |
|---|---|---|---|
| `Files` (`list`) | `build/this.cs:25` | empty | `build.goals` filters and orders the `*.goal` listing (`code/Default.cs:72-92`) |
| `Cache` (`bool`) | `build/this.cs:30` | true | `%!build.cache%`, in-memory `llm.cache` |

Any other key gives `UnknownSetting` (`setting/this.cs:88-91`). The file list never becomes a variable; `build.goals` reads `app.Build.Files` directly.

### 1b. plang: the builder's call chain (goals ✔ read; resolution of sub-goal names ~)

```
Build.goal                                   (.build/build.pr — loaded first)
 6-8  set default %path%, %!build.cache%, %!build.summary%
 9    set channel "builder" call BuilderChannel      → BuilderChannel.goal has NO steps: build output goes nowhere
 10   call EmitBuildEvent kind="build-path"          → EmitBuildEvent: render build-output.template → write out … channel:"builder"
 11   build.load            → returns app            [C#]
 12   build.goals path=%path% → list<goal>            [C#] parse .goal + merge old .pr
 13   call EmitBuildEvent kind="goals-found"
 14   foreach %goals%, call BuildGoal goal=%item%
        BuildGoal.goal: call BuildGoal/Start goal=%goal%
          BuildGoal/Start.goal  Start
            8   call Compile, on error call HandleBuildFailure
                  Compile: call Decide                          → BuildGoal/Decide.goal   (NO .pr)
                           foreach %goal.Step%, call Menu       → Menu → MenuModule
                           call Properties                      → BuildGoal/Properties.goal (NO .pr)
                           foreach %goal.Step%, call Settle     → Apply (build.validate) / FixProperties
            12  foreach %parentGoal.Child%, call BuildSubGoal   → Compile again per sub-goal
            19  save %trace% → /.build/traces/<id>/<goal>.json
            22  build.fold Goal=%goal%                          [C#]
            23  build.goalsSave Goal=%goal%                     [C#] → writes the .pr
 15   save %traceGoals% → /.build/traces/<id>/manifest.json
 16   build.appSave                                           [C#] → app.Save()
```

### 1c. What is what

| Kind | Files |
|---|---|
| **plang goals** | `os/system/builder/{Build,BuildGoal,BuilderChannel,EmitBuildEvent}.goal`, `BuildGoal/{Start,Decide,Properties}.goal` |
| **C# actions** (`module/action/build/`, logic in `code/Default.cs`) | `load` :308, `goals` :22, `fold` :137, `validate` :254, `goalsSave` :198, `appSave` :316, `merge` :290 (unused by the current goals) |
| **C# LLM** (`module/action/llm/`) | `llm.query` → `OpenAi` (`code/OpenAi.cs`), `llm.decider` → `TypeSafe` (**not registered**, A) |
| **Templates (Fluid)** | `llm/templates/deciderModuleQuestions.template` (stage 1), `deciderActionQuestions.template` (stage 2), `propertiesUser.template` (stage 3 user), `templates/output/build-output.template` (console events) |
| **Prompt** | `llm/Properties.llm`, the stage-3 system prompt, rendered as a template (`Properties.goal:13`) |
| **Live `.pr`** | `.build/{app,build,builderchannel,buildgoal,emitbuildevent}.pr`, `BuildGoal/.build/start.pr` (old flow, B) |

---

## 2. How one `.goal` becomes a `.pr`

```
.goal text ──Goal.Parse (goal/this.cs:400)──► goal{Step[text,indent,comment], Child = other goals in the file}
   │                         + MergePrData: old .pr merged in; corrupt → CorruptPrFile warning (Default.cs:329-371)
   ▼
Stage 1  Decide.goal:25-26   llm.decider  State=%state%  Question=<rendered deciderModuleQuestions>
   ▼       → %moduleAnswer%["s<i>_<module>"].noul
Stage 2  Decide.goal:28-29   llm.decider  Question=<rendered deciderActionQuestions>
   ▼       → %actionAnswer%["s<i>_<module>"].choice
Menu     Start.goal:36-45    per step: modules with noul ≥ 0.5 → "module.action" strings → %menu[i]%
   ▼
Stage 3  Properties.goal     llm.query  gpt-5.4-nano  system=Properties.llm  user=propertiesUser.template
   ▼       Schema = {step: list<{index, action: list<{module, name, property?, modifier?{…recovery?}}>}>}  (no child)
Settle   Start.goal:48-57    Apply: set %goal.step[i].action% = %properties.step[i].action%
   │                                build.validate step=… → freeze [Default]s, step.Validate, step.Action.Build
   │     on error → FixProperties: llm.query continuePreviousConversation=true "rejected by the validator: …"; retry 2
   ▼
build.fold   indented steps → the preceding condition action's Child (Default.cs:166-196); else IndentUnderNonCondition
build.goalsSave  NestRecursive (modifiers onto their action) → goal.Validate → serialize application/plang,
                 View.Store → file.Save goal.PrPath (Default.cs:198-250)
```

| Stage | Input | Output | Calls | Prompt / template |
|---|---|---|---|---|
| 1 module set | `%state%` = `{goal, step:[{index,text}], modules:{name: description}}` (`Decide.goal:21-41`) | noul per (step, module) | `llm.decider` (TypeSafe, `jev-latest`) | `deciderModuleQuestions.template`: instruction only, names the state path |
| 2 action | same state | choice per (step, module) with noul ≥ 0.5 and >1 action | `llm.decider` | `deciderActionQuestions.template`: `criteria` = action **names** only |
| menu | the two answers | `%menu[i]` = list of `"module.action"` strings | plang only | — |
| 3 properties | goal + per step its menu actions with their property rows | `{step:[{index, action:[…]}]}` | `llm.query` (OpenAI, gpt-5.4-nano, `cache=%!build.cache%`) | `Properties.llm` (system) + `propertiesUser.template` (user) |
| validate | the step with grafted actions | ok / error naming the step | C# `build.validate` → `step.Validate`, `step.Action.Build` | — |
| fixer | the validator's message | a new `%properties%` | `llm.query` continue-conversation, retry 2× | inline text, `Start.goal:64` |
| write | the goal | `<folder>/.build/<name>.pr` | C# `build.goalsSave` | — |

**Where the catalogs come from (✔ read):**

| What the templates read | Source |
|---|---|
| `%modules%` = `%!app.module.list%` | module registry; one `module.@this` per action module |
| `module.Description` | lazy file `os/system/modules/<name>/module.description.md` (`module/this.cs:131`) |
| `m.Action` / `m.Modifier` | catalog elements registered from the C# `[Action]` classes; `Action` excludes `[Modifier]` (`module/this.cs:67-76`) |
| `a.Property` → `p.Name`, `p.Type.Name/Kind/Values`, `p.Nullable`, `p.Default` | reflected from the handler's `Data<T>` properties (`Property = new(this, actionName)`, `module/this.cs:59-61`) |
| action descriptions / notes / examples (`os/system/modules/<m>/<a>.*.md`) | **not used by any current builder template** (#22) |

---

## 3. The LLM side

| | Decider (stages 1-2) | OpenAI (stage 3 + fixer) |
|---|---|---|
| Action | `llm.decider` (`llm/decider.cs`) | `llm.query` (`llm/query.cs`) |
| Provider | `TypeSafe` (`llm/code/TypeSafe.cs`), **not registered** | `OpenAi` (`llm/code/OpenAi.cs`), registered at `code/this.cs:263` |
| Endpoint | settings `DeciderConfig.decider.endpoint` → `TYPESAFE_ENDPOINT` → `https://api.typesafe.ai/v1/systemone` (`TypeSafe.cs:27`) | settings `LlmConfig.llm.endpoint` → `OPENAI_API_ENDPOINT` → `https://api.openai.com/v1/chat/completions` (`OpenAi.cs:67`) |
| Key | `decider.apiKey` → `TYPESAFE_API_KEY` (`TypeSafe.cs:29`) | `llm.apiKey` → `OPENAI_API_KEY` (`OpenAi.cs:69`) |
| Model | `jev-latest` (`decider.cs` default) | step says `gpt-5.4-nano`; default `llm.model` → gpt-5.4-nano |
| Cache | **none** | `LlmCache` table in `/.db/system.sqlite` (`OpenAi.cs:39`, `app/this.cs:575`); key = SHA256 over messages + model + temp + schema + format (`OpenAi.cs:895-913`); skipped when tools are present |
| Trace | none | `--debug={"llm":…}` → `/.build/traces/<traceId>/llm/<goal>_<step>.txt` (`debug/this.cs:369-414`) |

`--build={"cache":false}` flows: `Executor.cs:121` → `%!build.cache%` (passed explicitly as `cache=` in `Properties.goal:16`, `Start.goal:65`), and `Executor.cs:129-131` → in-memory `llm.cache=false`, which `llm.query`'s `Cache` resolves through. Builder traces: `/.build/traces/<id>/<goal>.json` and `manifest.json`. The python harness writes prompts to `/shared/coder/llm/plang/<file>/<Goal>/`; C# never writes there.

---

## 4. The python stand-in (`tools/decider/build_pr.py`, ✔ read)

```
main (:291-307)   arg = folder or one .goal (default os/system/builder); one thread per GOAL (WORKERS=16)
 parse (:87-106)  "- " = step (indent = spaces//4), "/" = comment for the next step, any other line = goal header
 menu_for (:136-149)
   h.stage1 (harness.py:239-258)  decider noul per (step, module) + one extra noul per step "s<i>_@store"
   h.stage2 (harness.py:260-281)  decider choice, criteria = {action: description}; single-action modules skip
   menu[i] = ["module.action", …]; @store ≥ 0.5 → + "variable.set"
 properties (:172-187)   OpenAI chat/completions (:180), system = Properties.llm VERBATIM, user = user_message(), json_object
 pr_goal / pr_action (:221-261)  answer → {name, path, step:[{index,text,lineNumber,comment?,waitForExecution,action}]}
 write (:278-287)  out/<folder>/.build/<name>.pr  (+ .pr.raw.json: menu + answer); first goal = root, rest = child
 DEVIATIONS (:190)  printed at the end, not saved: plural keys, missing type/name, dict-for-list
```

| Aspect | plang builder | `build_pr.py` |
|---|---|---|
| Parser | `Goal.Parse`: block comments, continuation lines, default `Start` goal, visibility | own line parser; comments above a header are dropped |
| Stage 1 prompt | template, state = plang value, steps from 0, one request | `STRUCTURE` teaching + text state, steps from 1, **15-step windows**, **extra `@store` question** |
| Stage 2 criteria | action **names** | action **descriptions** (the #22 direction) |
| Modifiers on the menu | never (D) | tagged `[modifier]` from the C# `[Modifier(` attribute |
| Property rows | reflected catalog (`p.Type`, `Nullable`, `Default`) | **regex over the C# handler source** (`:25`, `:61-84`) |
| Stage-3 user message | `propertiesUser.template` | `user_message()` (same layout, plus `SHAPES` for `action`-typed slots) |
| Schema | JSON schema in the step | `json_object` only |
| Validation / fixer | `build.validate` + FixProperties, retry 2 | **none**: one call, no retry |
| Frozen defaults | `build.validate` writes `action.Default` | **none** |
| fold / Child | `build.fold` on indent | **none**: indent is not written |
| Hash / prPath / flags | written by `goalsSave` (Store view) | **none** (F) |
| Output | live `<folder>/.build/` | `tools/decider/out/…` staging |

`pr_bootstrap.py` rewrites named `.pr` files in place. It renames plural/old keys (`steps`→`step`, `parameter(s)`→`property`, `action`→`name`, …), folds a flat inline `condition.*` into a child step, and rewrites `.Steps[` / `.Actions.` navigations. It keeps everything else, including stale hashes and the old flow (B).

---

## 5. Outputs

| | Where / how (✔ read) |
|---|---|
| `.pr` path | `goal.PrPath` = `<goal folder>/.build/<lowercase stem>.pr` (`goal/this.cs:120-124`) |
| Writer | `build.goalsSave`: `goal.NestRecursive(app.Module)` → `goal.Validate` → the `application/plang` serializer `SerializeItemAsync(goal, View.Store)` → `file.Save` (`Default.cs:219-242`) |
| One file per `.goal` | the first goal is the root; the other goals in the file are its `child` |
| Hash | `goal.Hash` `[Store]` = SHA256(Name + concat of step.Text), cached in `_hash` (`goal/this.cs:129-144`) |
| Staleness (#5b) | 3 of 6 live builder goals have stale hashes (open-items #5b). ~ A `.pr` with no hash (python) should compute a fresh one on read, since the getter derives it when `_hash` is null. Whether the reader sets `_hash` from a missing key is unverified. |
| App marker | `/.build/app.pr` via `build.appSave` → `App.Save()`; required by `RunAsync` before building (G) |
| Traces | `/.build/traces/<trace.id>/{<goal>.json, manifest.json, llm/…}` |

---

## 6. What we expect to break, and the unknowns

### The chain, as it will unfold (order ~)

| Step | Break | Evidence |
|---|---|---|
| 0 | `build.pr` load → `PrFormatOutdated` ("parameter"; `build.pr` has 17, `emitbuildevent.pr` 3, `buildgoal.pr` 2, `BuildGoal/start.pr` 59) | ✔ `action/serializer/Reader.cs:70-74` |
| 0' | `Tests/` has no `app.pr` → `NoAppFound` (G) | ✔ |
| 0'' | the old `start.pr` calls `Plan` / `BuildStep/Start` / `Compile.llm`, which are gone; `decide.pr` and `properties.pr` are missing (B, C) | ✔ |
| 3 | channel "builder" lookup: the generator now reads `await __channelParam.Value()` (`Emission/Action/this.cs:317-318`), unverified live | ✔ code, ~ live |
| — | `BuilderChannel` has no steps → every build event is written to a goal that does nothing: a silent build | ✔ |
| 1 of the pipeline | `llm.decider` → no IDecider provider (A) | ✔ |
| menu | modifiers missing (D); `%choices%` is never reset, so ~ each step's menu = all choices so far (a plain `call` shares the caller's variables, per `Tests/Modules/Variable/Scoping`) | ✔ goals, ~ runtime |
| stage 3 | no `child` in the schema (E); `Properties.llm` never teaches conditions | ✔ |
| settle | `set %goal.step[i].action% = <LLM rows>`: rows become action items at the assignment (~ untested path) | ~ |
| #22 | stage 2 criteria are names only; notes/examples reach no stage; the menu is strings, not actions | ✔ |
| if/child | `condition/if.examples.md:2` "the call is its own action" (reads as a sibling) vs the child rule | ✔ |

### Open questions

1. Should `TypeSafe` be registered as the built-in `IDecider` in `RegisterDefaults`, beside `OpenAi`?
2. Step 0: rebuild the builder's `.pr` with `build_pr.py` (fresh flow) or `pr_bootstrap.py` (keys only)? The bootstrap keeps the dead Plan/BuildStep flow (B), so it cannot run the current builder.
3. `build_pr.py` writes no fold, no frozen defaults, no hash and no flags. Is its output loadable by the goal reader as-is (missing `prPath`/`visibility`/`isSystem`), and do we accept un-folded conditions in the builder's own `.pr` for the first bootstrap?
4. How should modifiers reach the menu: add `m.Modifier` to the templates and MenuModule, or make stage 1/2 ask about modifiers separately?
5. Should the stage-3 schema and `Properties.llm` gain `child` for condition bodies (inline `if X, call Y`), or should fold also handle inline conditions?
6. `%choices%`: reset per step in `Menu`, or scope it by `%menu[step.Index]%` directly?
7. `Properties.goal:14` pastes `%propertiesSystemMsg%` (prose with quotes and newlines) into a JSON literal. `Decide.goal`'s own comment warns against exactly that. Does variable substitution run before or after the literal is parsed?
8. The `Tests/` repro needs an `app.pr`. Pass `--app={"create":true}`, or restore `Tests/.build/app.pr` (it was moved with the labels)?
9. Decider calls have no cache and no trace. Is that acceptable for development cost and debugging?
10. `BuilderChannel` is empty, so build output is invisible. Restore the forward (its comment points at a goal-backed-channel input bug) or point the channel at output for now?
