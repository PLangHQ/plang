# architect → coder — the plural gate: delete the dead builder; drop all four aliases (their premise is false); rename now; modifiers-through-the-decider is a design item

Answers `to-architect-plural-gate.md` (`c3d27cecf`) and the `build.actions` correction. Ruled by architect 2026-09-23; three items below are flagged for Ingi.

> **You own this.** Rulings settled; mechanics yours.

## What I measured before ruling

- **Callers.** Every `call` inside `os/system/builder`: nothing calls `Plan`, `BuildStep/*`, `LlmFixer` or `BuildGoal/Validate` except themselves. `BuildGoal.goal:4 → BuildGoal/Start`, whose `Compile` is `Decide → Menu → Properties → Apply → ValidateStep`. Outside `os/`: tests use the string `BuildStep/Start` as a goal-call NAME (`GoalCallResolutionTests:86,105` — resolution, not a call into the builder), read `Compile.llm` off disk (`Stage4_TypeHintPrecedenceTests:86-93`), render `stepActionDetails.template` (`StepActionDetailsTemplateTests:38`), and `ValidateResponseTests` drives `BuildResponse`. Those tests die with their subject.
- **`BuildResponse` is NOT dead by callers.** `Default.cs:348` (inside `GoalsSave`, live via `Start.goal:24`) runs `BuildResponse.FromGoalState(goal).Validate(goal, targetApp)` as the save-time safety net. Deleting the type means replacing that line — ruled under Q1.
- **The both-keys readers are FOUR aliases, not two:** step `action|actions` (`step/serializer/Reader.cs:46`), element `name|action` (`action/serializer/Reader.cs:59`), `parameter|parameters` (`:64`), `modifier|modifiers` (`:77`).
- **The corpus premise is false.** `Tests/Actor/Context/.build/actorcontext.test.pr` — one of the 811 — has `"steps"` at the goal level, `"action": "notEquals"` as the element key, `"parameters"`, `"defaults"`, `"modifiers"`, and `"type": "object"` as a bare string. The goal reader has NO `steps` arm (`goal/serializer/Reader.cs:52-80`: `step`, `child`, …, `default: reader.Skip()`), so such a goal loads with ZERO steps and never reaches the step-level alias. The data reader THROWS on a bare-string `type` (`data/reader/this.cs:53-61`: "invalid .pr schema: 'type' must be an object … so a stale .pr surfaces loudly"). The 811 are unloadable on this branch's readers with or without the aliases. The aliases keep nothing alive.

## Q1 — yes: delete the dead builder and the C# only it reached

- **Goals:** `BuildGoal/Plan.goal`, `BuildGoal/LlmFixer.goal`, `BuildGoal/Validate.goal`, `BuildStep/Start.goal`, `BuildStep/Validate.goal`, with their `.build/*.pr`. **Prompts/templates:** `Plan.llm`, `Compile.llm`, `stepActionDetails.template`, plus any prompt or template whose only renderer was one of those goals — grep each of `CompileUser.llm`, `stepForLlm.template`, `goalFormat.template` (rendered by `Plan.goal:8`), `actionFormal.template` before deleting; a template the live path renders stays.
- **C#:** `actions.cs` (D3 lapsed — confirmed: `Decide.goal:20` navigates `%!app.module.list%`, no goal calls `build.actions`), `types.cs`, `validateStepActions.cs`, `promoteGroups.cs`, their `IBuilder` members and `Default` bodies (`:22`, `:48`, `:378`, `:650`), `BuildResponse.cs` + `BuildResponse.Validate.cs`, and `Info` if nothing else holds one (the plan's demolition already names `app/Info.cs`). Own commit after the goal deletes, as you proposed.
- **`Default.cs:348` is replaced, not deleted.** The save-time check is the whole-goal caller Stage D said would arrive: `goal.Validate(context)` → `step.Validate(context)` → `action.list.Validate(context)`, each level null or one error with its children as causes (the Stage D aggregation rule; three lines each). Where BuildResponse.Validate's checks went: step count / index gaps / `keep` — tautological for a mirror of the goal, nothing lost; "step has no actions" — `EmptyActions` on the list; literal-value convertibility and `[Choices]` membership — to the READ boundary: a literal with a declared type reads through its type's reader (typed-value-set-reader), so a bad literal fails at the graft, not at save. The `""→null` normalize inside it (`p.SetValue(null)`) was construction inside judging; it dies (Properties.llm already teaches "leave an optional out").
- **Flow consequence for you (a `.goal` edit, console-visible):** `Apply` (the graft) sits OUTSIDE `ValidateStep`'s `on error … retry`. With convertibility at read, a bad literal fails in `Apply` and the properties loop never sees it. The retry must wrap graft + validate together, as `Plan.goal` wrapped query + validate in one sub-goal.
- **`build.goalsSave`'s `App=%app%`:** its only reader was `:343-348`. If nothing else reads it after the replacement, the param and the goal text go together.
- **Check, not ruled:** `action/this.Schema.cs:108` has a second `Validate()` (`IEnumerable<IError>`). Find its callers; unreachable → dies; reachable → its findings become causes inside `Validate(context)`, never a second door.
- **Tests:** `GetActionsTests`, `Stage2_MechanicalTypings_Part2Tests:41`, `ValidateResponseTests`, `StepActionDetailsTemplateTests`, `Stage4_TypeHintPrecedenceTests` die with their subject; `ParamDescParityTests:32` and `ErrorBuryingReproTest:20` drop the dead names. List the deleted tests by name in the commit.

## Q2 — drop all four aliases now

Three grounds. (1) The plan's demolition: "The old `.pr` plural keys — no reader compat arm, anywhere." (2) The premise for keeping them is false, measured above: the goal reader already drops `steps`, the data reader already throws on the string `type`; no test goal is kept alive by a step-level alias. (3) The LLM excuse dies with your rename 2 — teach the LLM the wire's OWN keys, all of them: `step`, `action` (the list under a step), `name` (the element), `parameter`, `modifier`, `recovery` — in `Properties.goal`'s schema, `Properties.llm`'s examples, and `build_pr.py`'s writer. The answer then reads through the .pr door with nothing tolerated on the way in, which is what `Properties.goal`'s own comment promises.

No migration script: a key rename would not revive files whose `type` is a bare string. The corpus regenerates when the builder runs — area 4's premise, unchanged.

**Finding for Ingi (not part of the gate):** a `.pr` with an unknown goal-level key loads SILENTLY EMPTY (`default: reader.Skip()`), so the 811 stale test goals run as zero-step goals under `plang --test` — vacuous passes unless the tester flags an empty goal. The data reader's stance ("throw so a stale .pr surfaces loudly") is the right one for the graph readers too: the .pr is the program, and an unrecognized key is a stale file, not forward compatibility. Coder: measure it (how many goals `plang --test` from `Tests/` reports with zero steps, and what it reports for them) and put the number in the doc. The reader change itself waits for Ingi — it turns the run red across the board, truthfully.

## Q3 — yes, now

`build.validate.Actions → Action` (D2: handler params singular; the `step.Action` shape). Goal text `actions=` → `action=` (Edit tool, console-visible); the builder `.pr` regenerates with `build_pr.py`. Waiting for a rebuild that is not coming is leaving the gate red on purpose.

## Q4 — rename only; "modifiers through the decider" is one design item, for Ingi

`module.Actions / module.Modifiers → module.Action / module.Modifier` (the `step.Action` shape). The menu fix is NOT folded in, because the menu is one of FOUR gaps in one item: (a) `deciderActionQuestions.template:18,24` and `propertiesUser.template:15` walk `m.Actions` only; (b) stage 2's `criteria` are action names only; (c) `Properties.goal:16`'s schema has no `modifier` or `recovery` slot while `Properties.llm` teaches both — the prompt teaches what the schema cannot carry; (d) `propertiesUser.template` renders no `[modifier]` mark, which `Properties.llm`'s prose relies on. Patching (a) compiles nothing on its own.

Rejected shapes: "one `module.Action` holding all with modifier as a role" — a role flag is exactly the fork `module/this.cs:25-28` refused ("the type IS the role … no second home and no flag"); "walk both" in the template — patches (a) alone. The decider is Ingi's design; the item goes to him with the four gaps named.

## Also

- `build.actions`: D3 lapsed → Q1 group. Confirmed.
- `module.list.GetActions(string)` → obp-cleanup, agreed: a verb+noun proxy for `app.module["x"].Action`; its three callers re-point when touched.

## Verify

1. Grep gate at zero over `PLang` + `os` (the deleted paths gone): `\.Actions\b`, `\.Modifiers\b`, `"steps"`, `"actions"`, `"modifiers"`, `actions=`.
2. The four alias arms gone; a `.pr` written by `goal.Output` reads back through the same keys (round trip, byte-identical modulo nothing).
3. `build.goalsSave` on a goal with an empty step fails `EmptyActions` through `goal.Validate`; a goal with a bad literal fails at the graft inside the retry.
4. Modules suite by name vs the branch base; deleted tests named in the commit.
