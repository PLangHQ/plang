# coder v5 — stage 5 result: faces and honest facts

Branch `app-systems`. The trace and proposals are in `v5/plan.md`. The rulings came from plang-40;
decisions are logged in `.bot/app-systems/architect/summary.md`.

## Commits
| commit | what |
|---|---|
| 5d4311778 | honest facts: each type's real description and example; the python twin finds a type's class wherever it lives; one eval run |
| 7eec7a6c8 | faces: a type written out in the Out view shows what it is; a type slot is the identity in every view |
| e4c758449 | the obsolete type view, `BuildTypeEntries` and module `Schema` go |
| 1d31e3148 | `goal.Comment` is the one description (`goal.Description` goes); the hash covers comments |
| 99d110412 | a folder's docs are its `start.md`; the repo README ↔ start.md pairs and `StartMdTests` |
| 65a720d0c | the cache merge: a folded indented body is cached like any step |
| ec3c2ad1f | 342 goal files put the description above the name; `.pr` re-saved (comment and hash only) |
| (this) | `Documentation/v0.2/goals-steps.md`: the comment rule, goal/step `Comment`, the hash |

## The eval (one run, C + nano, as ruled)
- 5 golden goals (c_eval round 10): first attempt right 58/58, silent 0, retries 0, warnings 2.
- The builder's 12 goals (bootstrap): 9 first try, 2 right after the per-step retry, 1 refused
  (SourceError step 1: nano joined texts with `+` inside a dict literal; no type fact in play). The
  recorded answers had 2 refused. Not chased; the recorded answers stay.

## Decisions and findings
- **Faces are the Out view only.**
  - `%!app.type%` is the type names; wire and clr declare `Internal` and stay out.
  - One type shows its name, description, example, aliases and kind names.
  - Every other view writes the identity.
  - A Data's `type` slot and a property's type are written with the type's `Write` in every view. They had gone through `Output`, which would have put faces on the outbound wire.
- **Facts on action and goal.** action gets an example only: it already has an instance `Description` (its teaching). goal's description came when `goal.Description` went (twins only, no second eval).
- **The cache-merge bug.** A condition's indented body is folded into the condition in the `.pr`, so it wasn't matched when a goal was rebuilt. Every goal with an indented body re-asked the decider once its hash moved. It's fixed in the merge, with `GoalCacheTests`.
- **show.pr from the os root** (ruled): path `/system/error/Show.goal`, isSystem true. Errors still render (`ErrorShowTests`).

## Waiting on Ingi
- **The kind faces** (`%!app.type.text.kind.md%`, `%!app.type.choice.kind.operator%`). A kind isn't navigable today. The shape that honours Ingi's ruling (a kind is an object answering `.list` and `["md"]`) is making the kind an item. Its cost, the kind verbs to rename, is in `v5/plan.md`: `Walk`, `Render`, `Child`, `Put`, `Bridge`.

## Left as they are (listed as agreed)
- **Old os `.pr` not rebuilt** (a build could ask the LLM): `apps/Git/start`, `apps/Plang/{buildgoal,hello,start}`, `system/{build,debug,run}`, `modules/{mapvariables, ai/builder, db/Builder/build, event/modules, install/installurl, output/askuserllm, ui/createtemplatefile, ui/Builder/{renderuserintent,setframeworks,setlayout}, ui/Builder/tests/setframeworktest}`.
  - `setlayout.pr` is pre-v0.2 (no code). Its goal now reads `start.md`.
- **Goal files left alone:**
  - commented-out steps under the name: the 8 disabled Http tests, HttpStatusRead_DoesNotMaterialiseBody, os/system/events/{SetupApp,Events};
  - a block comment: os/system/modules/db/Builder/Build.goal;
  - a description already above: SettingsCrud;
  - step comments: BuildGoal/Start's Compile, SystemVariables, ForeachDictionary.
- **Tests `.pr`:** `MissingVarIsNotNullValue.pr` stays stale; it predates its goal's text, as in the baseline.

## Verification
- Six suites at or under the baseline after every commit (final: Modules 37, Types 15, Wire 18, Data 44, Generator 18, Runtime 23).
- Twins (`PickListTests`, `BootstrapTests`) equal.
- `plang --test`: 7 pass, 0 fail, 317 stale.
