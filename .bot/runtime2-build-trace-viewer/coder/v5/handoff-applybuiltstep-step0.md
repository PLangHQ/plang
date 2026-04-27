# Handoff — `ApplyBuiltStep` step 0 saves with 0 actions

## The symptom

After rebuilding `/system/builder/ApplyStep.goal` (from `/workspace/plang/system` root), `system/builder/.build/applystep.pr` ends up with **one step that has an empty `actions: []` array**, even though the LLM response for that step contained valid actions.

Structure of the saved `.pr` after a successful rebuild:
```
ApplyStep:
  step 0 (if keep is true, call ApplyKeptStep):          2 actions  ✓
  step 1 (if keep is not true, call ApplyBuiltStep):     3 actions  ✓ (technically one too many — LLM drift; separate issue)
ApplyKeptStep:
  step 0 (write out "Kept prior mapping..."):            1 action   ✓
ApplyBuiltStep:
  step 0 (builder.validate actions=%stepResult.actions%, on error call HandleValidationError):   0 actions  ✗
  step 1 (builder.merge step=..., stepFromLlm=..., write to ...):                                2 actions  ✓
  step 2 (if %stepResult.level% != "high", call BuildStep):                                      2 actions  ✓
HandleValidationError:
  step 0–2:                                                                                      all OK     ✓
```

The broken step is uniquely `builder.validate actions=%stepResult.actions%, on error call HandleValidationError`. That `.pr` entry is unusable at runtime — `builder.validate` needs to run with the LLM-emitted actions passed in, but there's no action on the step to run.

## Trace proves the LLM emitted actions

`system/.build/traces/<ticks>_ApplyBuiltStep.json` for the most recent build contains:
```json
{
  "index": 0,
  "keep": null,
  "source": "new",
  "guidance": "...",
  "actions": [
    {"module": "builder", "action": "validate", "parameters": [...]},
    {"module": "error", "action": "handle", "parameters": [...]}
  ]
}
```
So:
1. The LLM response has actions for step 0.
2. `builder.enrichResponse` tagged it `source=new` (no prior, no keep hint) — correct.
3. `builder.validateResponse` accepted it (no validation errors).
4. But `builder.merge` into `goal.Steps[0]` didn't land — final `.pr` has `"actions": []`.

All *other* fresh-build steps landed correctly. Only this one step is broken.

## What's common to this step (hypothesis)

The step text is:
```
builder.validate actions=%stepResult.actions%, on error call HandleValidationError
```

This step is the only one in the whole file where **a `%variable%` reference is bound to a list-of-actions parameter** (`builder.validate.Actions` is `Data.@this<List<Action>>?`).

The compiled action for the LLM's response looks like:
```json
{"module":"builder","action":"validate","parameters":[
  {"name":"Actions","value":"%stepResult.actions%","type":"list<this>"}
]}
```

The parameter value is the literal string `"%stepResult.actions%"` — a variable reference that only resolves at runtime. `type` is `list<this>` / `list<action>`.

The strong hypothesis: the merge pipeline that moves an LLM response `stepResult` into `goal.Steps[N]` via `builder.merge step=…, stepFromLlm=%stepResult%` **loses or drops the action when one of its parameter values is `%variable%` typed `list<action>`**. The deserializer or the TypeMapping converter likely can't materialise `%stepResult.actions%` (a string) into `List<App.Goals.Goal.Steps.Step.Actions.Action.@this>` (the parameter's declared CLR type), so it either null-coalesces the whole parameter, silently drops the action, or fails the whole step-level merge.

## Why this matters

`keep:true` + merge-from-LLM is how the builder compiles every goal, including its own. This one step belongs to `ApplyBuiltStep`, which is part of the builder's self-rebuild path. If we can't compile it, the builder can't self-update that sub-goal. The next time ApplyStep.goal changes, the same broken .pr gets carried forward. It also hurts any user step that passes a `%var%` of type `list<action>` — e.g. `error.handle Actions=[...]` inline forms could trip the same bug.

## Upstream context (all the fixes that landed en route)

Fresh context will want to know these were already applied:

1. **`Step.PriorText`** — new `[JsonIgnore]` transient property on `Step.@this` capturing the prior .pr's step text. Used by template to distinguish matched-by-text vs fresh.
2. **`Steps.MergeFrom` — exact-text match only** (revert of my earlier two-pass). Positional fallback was dropped because it paired unrelated steps across refactors and produced wrong `@known` hints.
3. **`Goal.MergeFrom` recurses into sub-goals** — matches by `Name`, so sub-goal `Steps.Actions` get their prior values merged.
4. **`Goal.GroupModifiersRecursive`** — walks sub-goals and calls `Steps.GroupAllModifiers` on each, so sub-goal modifiers are nested into their preceding action's `Modifiers` at save time. Called from `DefaultBuilderProvider.GoalsSave`.
5. **`@known:` template marker + prompt rule** (`@hint` dead, removed). `@known` means "step text unchanged since last build; respond with `{index, keep:true}` and nothing else". LLM follows reliably now.
6. **Scheme loosening in `BuildGoal.goal` / `LlmFixer`** — `guidance/formal/level/confidence` optional, `keep?: bool` added.
7. **`ApplyStep.goal` split** — `ApplyStep` → `ApplyKeptStep` (no-op) or `ApplyBuiltStep` (original validate+merge chain).
8. **`validateResponse.cs` keep:true guards** — `keep:true` + no prior actions = error, `keep:true` + actions present = error.
9. **`builder.enrichResponse` C# action** — backfills prior actions/formal into trace response when `keep:true`, tags each step with `source: new|known`.
10. **Failure-capturing `HandleBuildGoalFailure`** — saves a trace with raw LLM response + details when `llm.query` returns an error. `Error.Details` property added for provider-attached context. `OpenAiProvider` now detects `finish_reason: length` / `content_filter` *before* JSON parsing and returns a dedicated `ResponseTruncated` / `ResponseFiltered` error.
11. **`MaxTokens` default raised to 16000** in `llm/query.cs`.
12. **`Fluid` `formal` filter** in `FluidProvider` — smart value rendering for the template (`%vars%` bare, strings quoted, dicts/lists JSON). Template uses `{{ p.Value | formal }}`.
13. **Filter bug fix in `DefaultBuilderProvider.Goals`** — path-qualified filter entries (`/foo/bar.goal`) match by Relative path only, not bare FileName. Skips picking up same-named goals in unrelated app trees.
14. **Server `ROOT`** — now `../..` (lands at `/workspace/plang/system/`), since `system/` is its own app. Viewer URL: `http://localhost:8080/builder/web/index.html`.
15. **Debug formatter** (`Debug/this.cs`) — dicts/lists JSON-serialized (no longer truncated to 3 keys). Uses `SensitivePropertyFilter.Strip` so `[Sensitive]` properties never leak into debug output.
16. **Trace viewer** (`system/builder/web/index.html`) — sidebar tree grouped by file/goal/trace, file-click shows raw `.goal` source, per-goal + per-file issue lists with drill-down, build-failure block rendering raw response when trace has `buildError`.
17. **Hand-edit on `system/builder/.build/buildgoal.pr`** — `HandleBuildGoalFailure` rewritten to save a trace before throwing; `BuildGoalCore` step 5 had `error.handle` moved into `llm.query.modifiers` (pre-existing flat-modifier bug in the builder's own .pr). Those edits are necessary bootstrap for the feature to take effect.

All of (1)–(17) landed and compile cleanly. The one remaining failure is the step described at the top.

## Files to investigate for the ApplyBuiltStep step 0 bug

Start here, in this order:

1. **`PLang/App/modules/builder/merge.cs`** — the merge action. Reads `Step` and `StepFromLlm` params. Calls `action.Step.Value!.Merge(action.StepFromLlm.Value!);`. Need to confirm `StepFromLlm.Value` has Actions populated when the LLM dict is deserialised into a `Step`. Log `action.StepFromLlm.Value?.Actions.Count` right before `Merge`.

2. **`PLang/App/modules/builder/providers/DefaultBuilderProvider.Merge`** — the provider method that merge.cs delegates to. Does `action.Step.Value!.Merge(action.StepFromLlm.Value!)`. Same question — what does StepFromLlm.Value look like?

3. **`PLang/App/Goals/Goal/Steps/Step/this.cs` line 190 `Merge`** — the actual merge on Step. Copies Actions, Errors, Warnings. If `from.Actions.Count == 0`, it leaves `this.Actions` alone (short-circuit at line 192). Suspect: `from.Actions.Count` is 0 because deserialisation dropped them.

4. **`PLang/App/Utils/TypeMapping.cs`** — where `Data.@this<Step>` gets populated by converting a `Dictionary<string, object?>` (the LLM response dict) into a `Step` object. This is where the LLM's `actions: [{module:…,action:…,parameters:[{name:"Actions",value:"%stepResult.actions%",type:"list<this>"}]}]` gets materialised. If the inner parameter's `type` is `list<this>` / `list<action>` and `value` is `"%stepResult.actions%"` (a string variable reference), TypeMapping may:
   - try to convert the string to a `List<Action>` and fail silently, leaving `null`;
   - or skip the parameter entirely;
   - or fail the whole step conversion, leaving `Actions` empty on the Step.

5. **`PLang/App/Data/this.cs`** — the `@this(name, value, type)` constructor calls `UnwrapJsonElement(value)`. For a string value with `type = list<action>`, no unwrapping happens. The Data keeps the string internally. When something later tries to access `.Value` as `List<Action>`, conversion runs through TypeMapping. If TypeMapping fails the string→List conversion, the Data returns null or errors quietly.

## How to reproduce

```
cd /workspace/plang/system
/workspace/plang/PlangConsole/bin/Debug/net10.0/plang build '--build={"files":["/builder/ApplyStep.goal"],"cache":false}'
```

After build, inspect the saved .pr:
```
python3 -c "
import json
d = json.load(open('/workspace/plang/system/builder/.build/applystep.pr'))
for g in [d] + d.get('goals', []):
    for s in g.get('steps', []):
        a = len(s.get('actions') or [])
        mark = ' NO_ACTIONS' if a == 0 else ''
        print(f\"{g.get('name'):25} step {s['index']}: {a} actions{mark}\")"
```

Expected: `ApplyBuiltStep step 0: 0 actions NO_ACTIONS`.

Trace for that step is at `system/.build/traces/<ticks>_ApplyBuiltStep.json` — the response under `pass1.response.steps[0].actions` is populated, confirming the LLM did return the actions.

## Suggested fix path (in order of likelihood)

### 1. Diagnose first with `--debug`
Use the already-enhanced debug with action-level visibility on `%stepResult%` and the target step inside ApplyBuiltStep. Specifically:
```
'--debug={"goal":"ApplyBuiltStep","level":"action","variables":["%stepResult%","%goal.Steps[0]%"],"maxLength":3000}'
```
Watch for `%stepResult.actions` being populated but `%goal.Steps[0].Actions` staying empty after the `builder.merge` action. That pins the loss to the merge path.

**Don't add `Console.Error.WriteLine` in C# for this** — use the debug system. If a module needs clearer diagnostics (specifically `builder.merge` showing its input/output), file that as a sub-task under the already-recorded "Module-scoped debug instrumentation" todo rather than adding ad-hoc prints. The current `--debug level:action` + variable watch should be sufficient to see StepFromLlm before/after merge.

### 2. If merge.cs receives StepFromLlm.Value with empty Actions
It's a TypeMapping / Data deserialisation bug. Fix it at the `list<this>` / `list<action>` conversion site: when the value is a `%variable%` string and the target is a list-of-CLR-objects, TypeMapping should **preserve the string as a variable reference** rather than return null. The Data object keeps raw string `"%stepResult.actions%"`, and `.Value` access resolves the variable at that point.

Alternative: the action parameter itself might be correctly preserved as a string at the outer Step level. Then the issue is that `builder.merge` copies the outer step's `Actions` list (which contains this action), but the list contains a malformed Action whose `Parameters` collection is empty. Look at whether `Actions[0].Parameters` has 1 or 0 entries after merge.

### 3. If Actions is correctly populated up through merge.cs but something drops it between merge and save
The save path is `DefaultBuilderProvider.GoalsSave` → `GroupModifiersRecursive` → `JsonSerializer.Serialize(goal, Json.PrWrite)`. Verify the in-memory goal.Steps[0].Actions right before `JsonSerializer.Serialize` (one `Console.Error.WriteLine` is acceptable here — one line, one probe, ripped after). If it's populated in memory but empty in JSON, the issue is in the `Json.PrWrite` / `StoreOnlyModifier` config (unlikely but possible — some edge case with nested variable-valued parameters).

### 4. Most likely root cause (guess based on the pattern)
The LLM emits a parameter whose `value` is a string (`"%stepResult.actions%"`) and whose declared `type` is a list type (`list<this>`). The parameter gets stored in the Data as the literal string, and *at serialisation time* `StoreOnlyModifier` or some custom converter notices the type/value mismatch and omits it. If omission cascades to the parent Action or the parent Step, that's how we lose the whole Action.

A non-invasive fix would be: don't omit — preserve `%variable%` string values even when the declared type is a complex CLR type. Resolution happens at runtime via MemoryStack, not at serialise time.

## Out-of-scope for this investigation

Nice-to-haves that came up but shouldn't be fixed in the same PR:
- ApplyStep step 1 having 3 actions instead of 2 (LLM added a phantom `variable.set`). Classic builder drift; separate instance of LLM non-determinism, not related to the list<action> bug.
- Template rendering of goal.call values showing `Fluid.Values.ObjectDictionaryFluidIndexable` instead of the goal name — needs the Fluid `formal` filter to unwrap Fluid's wrapping types. Separate template-filter issue.
- Module-scoped `--debug` system (see `Documentation/v0.2/todos.md`). Big architectural change; track there.

## Bot session state

- Branch: `runtime2-build-trace-viewer`
- Version: v5 (this session's directory)
- Plan: `.bot/runtime2-build-trace-viewer/coder/v5/plan.md`
- Tasks #1–#9 (see TaskList; all completed except #6 which is effectively done and #7 which covers viewer chips, pending)
- Active viewer server: `python3 /workspace/plang/system/builder/web/server.py 8080` (may or may not be running; restart if needed)

---

## Next-session update — what a follow-up investigation learned (2026-04-22)

### Key finding: Dict → Step conversion is NOT the bug
Added `PLang.Tests/App/Modules/builder/StepFromDictConversionTests.cs` — three tests
(including one with the literal JSON from `639124604435330102_ApplyBuiltStep.json`
trace) that exercise the exact path `StepFromLlm` takes:
`Dictionary<string,object?>` → `TypeMapping.TryConvertTo(typeof(Step))` →
`JsonSerializer.Serialize(dict)` → `JsonSerializer.Deserialize<Step>`.
**All three pass.** `Actions.Count == 2` end-to-end. The `list<this>` /
`list<action>` hypothesis in the original handoff is wrong — the string-typed
`%stepResult.actions%` parameter deserializes cleanly as a param value.

### Real cause is upstream of the merge
Ran `plang build --build={"files":["/builder/ApplyStep.goal"],"cache":true}`
with `--debug={"level":"action",...}`. The debug log shows:
- Build starts, reads `/builder/ApplyStep.goal` as one goal (with 3 sub-goals).
- `BuildGoal` fires for ApplyStep (one message: "Building goal: ApplyStep").
- Only 2 `ApplyStep/ApplyKept/ApplyBuilt` mentions in the whole log
  (both informational — "Building sub-goal: X"). Neither the `ApplyStep`
  runtime goal nor its sub-goals (`ApplyKeptStep`, `ApplyBuiltStep`) executes.
- `%traceGoals%` is `[]` at both Build step 2 and Build step 8 (end-of-run).
  Nothing got appended → `ProcessGroup` / `ApplyStep` / `ApplyBuiltStep` are
  NEVER called for this file.

### Why the whole apply chain no-ops: Fluid-wrapped goal names in applystep.pr
`system/builder/.build/applystep.pr` has the broken goal.call params:
```json
{"name":"GoalName","value":{"name":"Fluid.Values.ObjectDictionaryFluidIndexable`1[System.Object]","parameters":[]},"type":"goal.call"}
```
`GoalName` is the Fluid wrapper class identity string, not `"ApplyKeptStep"` /
`"ApplyBuiltStep"`. So every `condition.if ... call ApplyKeptStep` resolves
to `goal.call ApplyKeptStep=<garbage>` → `goal not found` → silently no-ops
(or throws and is swallowed somewhere in `loop.foreach`'s error flow).
**That's the reason step 0 stays empty.** The merge is never called because
ApplyBuiltStep is never called because the goal.call name is wrong.

The handoff lists this as "out of scope / Fluid template-filter issue",
but it's actually the **load-bearing** bug for the whole ApplyStep chain.
Without fixing it, the builder's self-rebuild cannot work — any step in
any goal that produces a Fluid-wrapped goal name becomes uncallable and
nothing gets merged/saved for that step.

### What's producing the Fluid-wrapped goal name
Almost certainly the Fluid template rendering in
`system/builder/templates/v2/goalFormatForLlm.template` when it emits
something like `{{ p.Value }}` for a `goal.call` parameter: Fluid wraps
the underlying dict in `ObjectDictionaryFluidIndexable<object>` and the
`.ToString()` of that wrapper is the type-identity string. Somehow that
string is getting written into the `.value.name` field of the parameter
when serialized to the .pr.

The `formal` filter fix (part of upstream fix #12 in this handoff) was
supposed to dodge this — `{{ p.Value | formal }}` unwraps before
rendering. But the .pr still has the bad value, meaning either:
(a) the filter isn't applied everywhere the goal.call value is rendered, or
(b) the bad value is coming from a different path entirely — e.g. the LLM
    response itself once carried a Fluid-rendered string and it got
    persisted before the filter was added, and the `keep:true` flow is
    then preserving it across rebuilds.

### Reproduce from scratch
```bash
cd /workspace/plang/system
/workspace/plang/PlangConsole/bin/Debug/net10.0/plang build \
  '--build={"files":["/builder/ApplyStep.goal"],"cache":true}' \
  '--debug={"level":"action","variables":["%goals%","%traceGoals%"],"maxLength":400}' \
  > /tmp/debug_all.log 2>&1
grep -c "ApplyStep\|ApplyKept\|ApplyBuilt" /tmp/debug_all.log      # ≈ lots (all from goal.Steps[*].Text dumps)
grep -c "of ApplyBuiltStep\|of ApplyStep "  /tmp/debug_all.log     # should be 0 — no runtime execution
grep Fluid.Values /workspace/plang/system/builder/.build/applystep.pr
```

### Suggested next path
1. Fix the root: find where `goal.call.Value.name` is getting set to the
   Fluid wrapper type-string and preserve the real name instead. Candidates:
   (a) the `formal` filter isn't applied consistently in the LLM prompt
   rendering for `goal.call` values → LLM echoes back the type string as
   the goal name; (b) `ResolveGoalCallPaths` in
   `DefaultBuilderProvider.cs` is fed a Fluid-wrapped object; (c) some
   `ConvertValue` path turns a dict with a Fluid-stringified inner key
   into a GoalCall whose Name field holds the wrapper identity.

2. Once `.value.name == "ApplyKeptStep"` / `"ApplyBuiltStep"`, the whole
   Apply chain starts running again. THEN — and only then — re-check
   whether the original symptom (step 0 saved with 0 actions) still
   reproduces. It may well have been a secondary effect of the silent
   no-op, in which case it vanishes once goal.call works.

3. Diagnostic shortcut: before diving into Fluid, manually hand-edit
   `applystep.pr` to put the correct goal names in the two broken
   `goal.call` params. Then re-run the build with `cache:true` and
   confirm step 0 of ApplyBuiltStep gets non-empty actions. That
   proves the hypothesis and tells you whether any other bug is still
   lurking in the merge path.

### New test landed this session
`PLang.Tests/App/Modules/builder/StepFromDictConversionTests.cs` — keep.
It pins down the dict→Step conversion path so future drift in TypeMapping
or STJ behaviour is caught immediately. 3 tests, all passing.
