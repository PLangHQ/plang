# to-architect — the plural gate (plan §5 acceptance #1)

Ingi's go: close the branch's own purpose. The plural grep gate is not at zero. Inventory below is
from grep on HEAD; proposal per group; **four questions for you (Q1–Q4)** before I touch code.

## Context — the builder was replaced mid-branch

`BuildGoal/Start.goal` now compiles through the decider pipeline:
`Decide → Menu → Properties → Apply → ValidateStep`. The OLD planner/compiler path has **no caller
left** (grep `call Plan|BuildStep|LlmFixer|Validate` in os/ → only Plan.goal calling its own
sub-goals):

| dead goal/prompt | plural hits it carries |
|---|---|
| `BuildGoal/Plan.goal`, `BuildGoal/LlmFixer.goal`, `BuildGoal/Validate.goal` | schema `steps/actions`, `%plan.steps%` |
| `BuildStep/Start.goal`, `BuildStep/Validate.goal` | schema `actions`, `%planStep.actions%`, `%compileResult.actions%` |
| `llm/Plan.llm`, `llm/Compile.llm`, `templates/stepActionDetails.template` | `"steps"`, `"actions"` in teaching JSON |
| C# only those goals reach: `build.validateStepActions`, `build.types`, `build.promoteGroups` (no goal uses it), `BuildResponse*`, the `node?["steps"]` plan-preview in `build/code/Default.cs:398` | `Actions`, `Steps` params, `"steps"` literal |

**Q1. Delete the dead old builder (goals + their `.build/*.pr` + the two prompts + template) and
the C# actions only it reaches?** Most of the gate's hits vanish by deletion instead of rename —
renaming a corpse is what D3 already refused. Git keeps it as reference. If yes, C# action deletes
go in their own commit after the goal deletes.

## Live renames (do regardless of Q1)

1. **`module.Actions` / `module.Modifiers` → `module.Action` / `module.Modifier`**
   (`PLang/app/module/this.cs:66,70`). Consumers: `BuildGoal/Start.goal:45-46`,
   `deciderActionQuestions.template:18,24`, `propertiesUser.template:15`, 2 tests
   (`RealCatalogRenderTests:49`, `SummaryPlannerTemplateTests` doc). Same shape as `step.Action`.
2. **Stage-3 answer shape → the .pr's own keys**: `Properties.llm` teaching JSON, the
   `Properties.goal:16` schema `{steps:[{index, actions:[…]}]}` → `{step:[{index, action:[…]}]}`,
   `Start.goal:51` `%properties.steps[i].actions%` → `%properties.step[i].action%`,
   `tools/decider/build_pr.py` reads the same. Rationale: the answer is grafted straight into
   `goal.step[i].action`, so it should be born in that vocabulary (D1).
3. **`Decide.goal:36`** `%state.steps%` → `%state.step%`.
4. **Handler param `build.validate.Actions` → `Action`** (area-2 miss; D3 exempts only
   `build.actions`). Goal text `build.validate actions=` → `action=`.
5. Doc comments: `IStep.cs:6`, `callstack/call/this.Snapshot.cs:10`, `item/kind/list/this.cs:5,22,47`,
   `item/this.cs:217`, `variable/path/this.cs:5,37`.

## The both-keys readers

`goal/step/serializer/Reader.cs:46` accepts `action` **and** `actions`;
`goal/step/action/serializer/Reader.cs:77` accepts `modifier` **and** `modifiers`. The plan said
"no both-keys reader" — but **811 `.pr` files** (excluding .bot/tools) still carry plural keys, and
plan area 4's "regenerate everything with `plang build`" is not available: the runtime cannot run
the builder yet (Ingi's assessment; builder `.pr` files are hand-edited/python-built).

**Q2. Keep the aliases as a bridge with a named executioner** (removed in the commit that first
regenerates the tree with the plang builder), **or drop them now** and accept that the 811 stale
`.pr` files fail to load until rebuilt? I lean keep-with-executioner: dropping breaks every
`plang --test` goal on this branch for no gain while the rebuild is impossible.

## Remaining questions

**Q3.** `build.validate` param `Actions → Action` changes a live goal step (`action=` in goal text)
— fine, or leave it until the plang builder runs and D2's rebuild catches it?

**Q4.** `module.Action` returns only non-modifier actions while `module.Modifier` returns the
modifiers — the decider menu needs BOTH (known bug: `error.handle` can never reach the menu
because the templates walk `m.Actions`). Rename only here, or do you want the menu fix (walk both,
or one `module.Action` holding all with modifier as a role) folded in?

## Order after your answers
Q1 deletes → renames 1–5 (one commit each, Edit-tool, console-visible) → rebuild builder `.pr`
with `tools/decider/build_pr.py` → Modules suite + grep gate report. No full sweep.
