# 7f — stage 7's builder-visible changes, one eval (plan, for plang-cd → Ingi)

Nothing here has run yet. No LLM call happens until this plan is accepted.

## 1. What the builder or a plang developer sees differently since the last eval (stage 6, 8df240b2f)

Stage 6's own changes were evaluated in stage 6's eval (golden 58/58, bootstrap 9/1/3, prompts byte-equal). They are listed
here once more so Ingi has the whole list. **New since then** is marked ★.

| # | Change | Decision | What it looks like to a plang developer / the builder |
|---|---|---|---|
| 1 | Methods are opt-in with `[LlmBuilder]` | 27 | `%name.foo()%` on an unmarked method is "text has no method 'foo'" |
| 2 | The silent index fallback is gone | stage 6 plan | `%dict[key]%` with an unset `key` is `IndexNotSet`; a literal key is quoted: `%dict["key"]%` |
| 3 | `%setting.X%` is gone | 25 | settings are `%!path%` (no `.goal` used it) |
| 4 ★ | `%!path%` reads the actor's settings | 7e-2a (43, 48) | `%!llm.cache%`, `%!app.goal.list.setting.os%` read a setting (was NotFound); `%!build.cache%` works again |
| 5 ★ | `set %!path% = x` writes this run's setting; an option that doesn't exist is refused | 62 | `set %!http.request.timeout% = 5` → CannotSetChild (the option is `timeoutInSec`) |
| 6 ★ | `setting.get` / `setting.set` gone; `setting.save` / `setting.remove` take a setting | 50, 70 (7e-2b-ii) | `get settings 'ApiKey'` no longer exists; `save %!llm%`, `remove %!app.goal.list.setting%` |
| 7 ★ | test.start's `Parallel` / `Timeout` params gone | 62 | `set %!app.test.setting.parallel% = 1` before `test.start` |
| 8 ★ | test.report's `Format` param gone | 66 | `set %!app.test.setting.format% = 'junit'` before `test.report` |
| 9 ★ | Identity and permission live in settings | 68 | none in goal text; an existing app's identity regenerates once (old tables left unread) |
| 10 ★ | Concept types are entries of the type list | 16 | `%!app.type.list%` names goal, module, actor, test, variable beside type; their facts are read from the type itself |
| 11 ★ | The prompt teaches `%!app.X["key"]%` once | plan stage 4/7 (architect) | one line in `Properties.llm`: `%!app.goal["/Start"]%` is one element of `%!app.goal.list%` |
| 12 ★ | Setting classes are taught to the LLM | plan 7e (coder v7 :413) | see 2c — the shape is a question |
| 13 ★ | Build.goal's dead `set default %!build.summary%` step is gone | 70 | the builder's own goal, no user-visible change |

Rows 1–9 and 13 are built. Rows 10–12 are 7f's C# work (section 2).

## 2. The C# work (before any eval)

a. **Concept types in the type list (decision 16).** At app construction, `type.list.Replace(goal)`, and the same for module,
   actor, test and variable, the way `type` is already (`app/this.cs:284`). Each answers its facts itself (Description,
   Example from its class). Twin: `PickListTests` and the properties twin hold C# and python equal. The types section
   only lists types an action's property uses (`properties.template:55-89`), so this reaches the prompt only where an
   action takes a concept type (e.g. `build.fold`'s `goal`, `test.start`'s `list<test>`).
b. **The prompt line (row 11).** One line in `Properties.llm`, the values section, after `%x%`:
   `%!app.goal["/Start"]%  one goal: one element of %!app.goal.list%, by its key (the same for module, type, …)`.
   `Properties.llm` is read as is by both C# and `prompt_c.py`, so no twin change.
c. **Teaching the setting classes (row 12). Question — my pick first:**
   - (i) *my pick:* the user message's Types section gains a **Settings** block, but only when a step names a
     `%!path%` whose root is a setting class: that class's options, `name: type = default`. It comes from
     `%!app.type.list["setting"]`'s kinds, rendered by `properties.template`. `prompt_c.py` needs the same data: a
     fixture `os/system/builder/llm/settings.json` written by a C# test (`SettingCatalogTwinTests`) from the classes,
     read by python. That is the pattern `pick_golden.json` uses, and a twin test fails when they drift.
   - (ii) every setting class, always, in the system prompt: ~8 classes, ~30 lines on every build.
   - (iii) nothing in the prompt: the catalog examples (rows 7, 8) teach the form. The LLM writes `set %!x% = v` as
     `variable.set`, and a wrong option is refused at run (row 5), not at build.
d. **The minor from bbbb45925's review:** `Setting.Save(setting)` / `Remove(setting)` read the path from the setting
   itself; `setting.save`/`remove`, identity and permission call it whole.

Each of a–d: its own commit, six suites against the baseline, twins green, plus the builder rebuild check and the
`.goal`/template consumer sweep.

## 3. The prompt diff (for Ingi)

Rendered requests for round 12 go to `/shared/coder/2.0` (current round only; round 11 moves to
`rounds/round11-run1` as is, round 12 lands in `rounds/round12-run1` too). Plus `/shared/coder/2.0/round12-diff.md`:
per golden goal and per builder goal, `diff -u` of round 11 → round 12 for `decider/1.decider.state.txt`,
`decider/2.decider.state.txt`, `system.txt`, `user.txt`, with one line naming which change (row #) each hunk comes
from. The expected hunks:
- stage 1's state: the `setting` module's description, `save` instead of `get`/`set` (row 6), test.start/test.report
  examples (rows 7, 8) — every goal's stage 1 state lists all modules, so every goal's state changes here;
- `system.txt`: the one line (row 11);
- `user.txt`: a Settings block only where a step names a `%!path%` (row 12, if (i)); concept-type facts only where a
  listed action takes one (row 10).
Any hunk not traced to a row is a stop point (section 6).

## 4. The eval

- **Golden set:** `MODELS=gpt-5.4-nano PROMPTS=C ROUND=12 python3 tools/decider/c_eval.py`. 5 goals, 58 steps: 10
  decider calls (typesafe), 5 nano calls plus retries (≤5). **Pass bar:** final 58/58 right, silent 0, failed 0;
  first attempt ≥ 56 (round 11 was 56, round 10 58).
- **Bootstrap (the builder's own goals):** `python3 tools/decider/bootstrap.py`. 7 files, 12 goals: 24 decider calls,
  12 nano calls plus retries. **Pass bar:** ≥ 9 first try and ≤ 3 refused (stage 6: 9/1/3). Each refusal is diagnosed,
  and none may trace to a stage-7 change (a nano slip on an unchanged step is accepted, as ruled).
- Cost: ~40 typesafe calls and ~20–30 nano calls in total; well under a dollar.
- `TYPESAFE_API_KEY` is passed to the process only (never printed); `OPENAI_API_KEY` as configured.

## 5. Re-records and first builds

- **PickListTests' golden:** `python3 tools/decider/pick_fixture.py /shared/coder/2.0/rounds/round12-run1` (no calls:
  recorded answers). Green = `PickListTests` passes, and the only fixture diff is catalog text from rows 6–8/12.
- **Build.goal's bootstrap:** `bootstrap.py` above re-records every builder goal's answers, Build.goal's included.
  Green = `BootstrapTests.EveryBuilderStep_TakesItsCode_WhereAndOnlyWherePythonAccepted` passes.
- **First builds of the 10 rewritten test goals:** `cd Tests && plang '--build={"files":[…]}'` (TypeSafe key passed),
  then read each `.pr`, then run the test. Green for each = the build refuses no step, the `.pr`'s actions match the
  step text (read, not assumed), and the test passes, or fails for a named reason outside stage 7 (a missing fixture
  folder, say), written down per goal:
  - `Tests/TestModule/Run/TestRunEnforcesTimeout`, `Tests/Modules/Test/Run/TestRunEnforcesTimeout`: `variable.set`
    on `%!app.test.setting.timeoutSeconds%`, then `test.start`; the fixture comes back `Timeout`.
  - `Tests/TestModule/Run/TestRunIsolatesMemoryStackBetweenTests`, `Tests/Modules/Test/Run/…`: `…parallel% = 1`.
  - `Tests/TestModule/Report/TestReportWritesJunitXml`, `…MasksSensitiveVariablesJunit` (both trees):
    `…format% = 'junit'`, then `test.report`; `%report.content%` holds `<testsuites>`.
  - `Tests/Modules/Settings/SettingsCrud`, `Tests/App/Actors/Datasource/ActorDatasource`: `variable.set` on a
    `%!…%` option, `setting.save`, `setting.remove`.

## 6. Order and stop points

1. 2a → 2b → 2c (once the question is answered) → 2d, each a commit: suites, twins, builder check, sweep.
   **Stop** if a twin needs more than the lines named here.
2. Render round 12's requests without asking (a dry render, if `c_eval.py` can; else step 3 renders them) and write
   `round12-diff.md`. **Stop and report** if a hunk traces to no row.
3. Golden eval. **Stop and report** below the bar: final < 58/58, any silent, or first attempt < 56 with a cause in
   stage 7.
4. Bootstrap. **Stop and report** on > 3 refused, or any refusal on a step whose prompt changed.
5. `pick_fixture.py`; PickListTests and BootstrapTests green.
6. First builds of the 10 test goals; report per goal.
7. Report: the counts, the diff file's path, the per-goal build results. Then the all/Every fold (decision 36).
