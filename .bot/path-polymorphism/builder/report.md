# Builder self-rebuild — 3 reproducible regressions

**Status:** builder runs end-to-end, but a fresh self-rebuild on `path-polymorphism` (after merging runtime2) produces broken `.pr` files in two deterministic classes (and one intermittent class). Hand-patched once to land a working snapshot on `runtime2` (commit `1910f1bc5`), but the underlying causes are still present and will regress on the next rebuild unless fixed upstream.

**Cardinal rule applies:** fix the prompt / validator / catalog, **not** the `.pr` files. The `.pr` is downstream of the builder, not a place to compensate for it.

---

## How to reproduce

```bash
cd os/
plang '--build={"cache":false,"files":["Build.goal","BuildGoal.goal","BuildGoal/Start.goal","BuildGoal/Plan.goal","BuildGoal/Validate.goal","BuildGoal/LlmFixer.goal","BuildStep/Start.goal","BuildStep/Validate.goal"]}' '--app={"create":true}' build
```

Then run the audit script at the bottom of this file. Two classes will fire every time.

> Side note: `--app={"create":true}` is currently *required* on `path-polymorphism` because of an inverted check at `PLang/app/modules/builder/this.cs:110` — it prompts to create when the app marker DOES exist (should be `!File.Exists`). Not in scope for this report, but please flag/file separately.

---

## Class 1 — goal.call prPath misses (8 instances)

After self-rebuild, 8 `goal.call` entries land in `.pr` files with `prPath: null`. The runtime then has to resolve at dispatch time and chokes when the name is slash-qualified (`BuildGoal/Start` → searches `.build/buildgoal/start.pr` instead of `BuildGoal/.build/start.pr`).

### Two distinct failure modes

**1a. Slash-qualified cross-folder names** (3 instances)

| .pr file | call name | expected prPath |
|---|---|---|
| `.build/buildgoal.pr` | `BuildGoal/Start` | `/system/builder/BuildGoal/.build/start.pr` |
| `BuildGoal/.build/start.pr` (×2) | `BuildStep/Start` | `/system/builder/BuildStep/.build/start.pr` |

The validator I added on runtime2 (`ResolveGoalCallPaths` in `PLang/app/modules/builder/code/Default.cs`, ~line 848) calls `goalCall.GetGoalAsync(app, context)` to reuse the runtime's own resolver. That resolver does NOT handle slash-qualified names like `BuildGoal/Start` — it expects a bare goal name.

**1b. Nested in `error.handle.Actions` (a `list<action>` parameter value)** (5 instances)

| .pr file | call name | expected prPath |
|---|---|---|
| `.build/buildgoal.pr` (or `BuildGoal/start.pr`) | `HandleBuildFailure` ×2 | `/system/builder/BuildGoal/.build/start.pr` (sub-goal) |
| `BuildGoal/.build/plan.pr` | `LlmFixer` | `/system/builder/BuildGoal/.build/llmfixer.pr` |
| `BuildStep/.build/start.pr` | `RefineActions` | `/system/builder/BuildStep/.build/start.pr` (sub-goal) |
| `BuildStep/.build/start.pr` | `FixValidation` | `/system/builder/BuildStep/.build/start.pr` (sub-goal) |
| `BuildStep/.build/start.pr` | `HandleStepFailure` | `/system/builder/BuildStep/.build/start.pr` (sub-goal) |

The structure is:
```
action (e.g. llm.query)
  modifiers: [
    error.handle
      parameters: [
        { name: "Actions", value: [ {module:goal, action:call, parameters:[GoalName={...}]} ], type: "list<action>" }
      ]
  ]
```

My `ResolveGoalCallPaths` walks `action.Parameters` + direct `action.Modifiers[].Parameters`, but does NOT descend into parameter *values* that are themselves action lists. The 5 nested calls are missed.

### Proposed fix (C#, contained)

In `PLang/app/modules/builder/code/Default.cs` `ResolveGoalCallPaths`:

1. **Recurse into `list<action>` parameter values.** When a parameter's `Type.Value == "list<action>"` and `Value` is a list, walk each entry as an action (apply the same `goal.call` resolution rule). Catches all 5 nested cases.
2. **Resolve slash-qualified names.** Either:
   - **(preferred)** Extend `GoalCall.GetGoalAsync` in `PLang/app/goals/goal/GoalCall.cs` to handle slash-qualified names — split on `/`, treat prefix as a folder hint, walk ancestors of the caller looking for `<folder>/.build/<bareName>.pr`. Build-time and dispatch-time then agree by construction.
   - **(fallback)** Add a slash-fallback branch inline in `ResolveGoalCallPaths` (the version I reverted on runtime2 had this — ancestor walk for `{ancestor}/{folder}/.build/{bareName}.pr`).
3. **Strip slash prefix from `goalCall.Name`** when `prPath` is set. The dispatcher matches `name` literally against the loaded .pr's `Name` field, so `{name: "BuildGoal/Start", prPath: ...}` 404s with `GoalNotFound`. Should be `{name: "Start", prPath: ...}`. This is what I hand-patched.

---

## Class 2 — `builder.actions ..., write to %x%` mis-compile (recurring)

Step text in `os/system/builder/BuildStep/Start.goal:16` reads:
```
- builder.actions include=%planStep.actions%, write to %actions%
```

The Compile LLM consistently emits this two-action chain:
```json
[
  { "module":"builder", "action":"actions", "parameters": [] },          // ← missing Include
  { "module":"variable","action":"set", "parameters": [
      {"name":"Name","value":"%actions%"},
      {"name":"Value","value":"%planStep.actions%"}                       // ← should be "%!data%"
  ]}
]
```

Result: `%actions%` ends up as the input *list* (`["assert.equals"]`), not the rendered catalog. The downstream `stepActionDetails.template` then iterates `%planStep.actions%` and tries to match against `%actions%`, finds nothing, and renders empty `%actionDetails%`. The Compile LLM looking at step 4 (`assert %message% equals 'hello plang'`) sees no `assert.equals` catalog row and returns `missing-actions`.

**This is the same regression that hit on runtime2 last week.** Hand-patched in `1910f1bc5`. Will recur on every rebuild.

### Why the LLM gets this wrong

The `write to %x%` idiom in PLang means *take the previous action's return (`%!data%`) and store it in `%x%`*. The LLM keeps reading `write to %actions%` as a directive to write the *named parameter* (`include=%planStep.actions%`) into `%actions%`, dropping the `Include` parameter from the producer action and routing its input — not its output — into the target variable.

### Proposed fix (structural validator — preferred)

Extend `builder.validate` (`PLang/app/modules/builder/code/Default.cs:Validate`) with a `write to` post-condition check:

- **Detection:** if `step.Text` contains the literal token `write to %<name>%` (case-insensitive), record `<name>` as the expected target.
- **Required shape:** the *last* action in the compiled chain must be `variable.set` with `Name="%<name>%"` and `Value="%!data%"`. Anything else → validation failure with key `WriteToShape`.
- **Producer-param check:** the *action immediately before* the trailing `variable.set` must carry all parameters explicitly named in the step text (e.g. `include=%planStep.actions%` → `Include` param must be present, with value `%planStep.actions%`). Captures the dropped-Include half.

Either failure surfaces through the existing `LlmFixer` retry loop with the validator's complaint as `previousConversation` — the LLM gets a targeted correction message.

### Fallback fix (prompt tightening)

Add an explicit teaching example to `os/system/builder/llm/Compile.llm` showing the `module.action k=v, write to %x%` idiom → two-action emission pattern, with the `Value=%!data%` and `Name=k` both spelled out. Less robust than the validator since the LLM already has many examples and still gets this one wrong.

---

## Class 3 — `%messages%` JSON-string with embedded multi-line `%var%` (intermittent)

Compile step 7 in `BuildStep/Start.goal`:
```
- set %messages% = [{"Role":"system", "Content":"%buildStepPrompt%"}, ...], type=json
```

Some rebuild runs emit this as `variable.set Value="[{\"Role\":\"system\",\"Content\":\"%buildStepPrompt%\"}...]"` (a JSON-encoded *string* containing `%var%`). At runtime, `%buildStepPrompt%` (a multi-line LLM prompt with raw `\n` bytes) gets substituted into the string and the subsequent `JsonParse` chokes on the unescaped newlines.

Working shape (from `RefineActions` step 1 in the same file) stores `Value` as a native list:
```json
"value": [
  { "Role": "system",  "Content": "%buildStepPrompt%" },
  { "Role": "user",    "Content": "%__userMsg__%" }
],
"type": "list`1"
```

### Why this is intermittent

Both shapes are valid JSON for the LLM to choose. The Compile examples in the catalog probably show one form for some actions and the other form elsewhere. Whichever shape the LLM picks for `%messages%` is random across runs.

### Proposed fix (structural validator)

In `builder.validate`, when `variable.set` has `Type=json` (or `Type=list*` etc.) and `Value` is a *string*, reject if the string contains a `%var%` reference. The safe shape is a native list/dict where `%var%` appears at field-value position. Surfaces with key `JsonStringVarLeak` → `LlmFixer` retry with the targeted message.

---

## Audit script

Pre/post rebuild check that flags all three classes statically — drop into any utility location:

```python
import json, glob
files = sorted(glob.glob('os/system/builder/.build/*.pr') +
               glob.glob('os/system/builder/*/.build/*.pr'))
issues = []
for path in files:
    pr = json.load(open(path))
    def walk(o, where):
        if isinstance(o, dict):
            # Class 1: goal.call prPath / slash-qualified name
            if o.get('module')=='goal' and o.get('action')=='call':
                for p in o.get('parameters') or []:
                    if p.get('name','').lower()=='goalname' and isinstance(p.get('value'),dict):
                        gc = p['value']
                        name = gc.get('name') or ''
                        if name and '%' not in name and not gc.get('prPath'):
                            issues.append(f'[prPath] {path} {where}: {name!r} prPath=None')
                        if '/' in name and gc.get('prPath'):
                            issues.append(f'[slash-name] {path} {where}: name {name!r} has slash with prPath set')
            # Class 3: variable.set Type=json with %var% inside string Value
            if o.get('module')=='variable' and o.get('action')=='set':
                params = o.get('parameters') or []
                v = next((p for p in params if p.get('name')=='Value'), None)
                t = next((p for p in params if p.get('name')=='Type'), None)
                if v and t and t.get('value')=='json' and isinstance(v.get('value'),str) and '%' in (v.get('value') or ''):
                    issues.append(f'[json-str-var] {path} {where}')
            for k,val in o.items(): walk(val, where)
        elif isinstance(o, list):
            for v in o: walk(v, where)
    for g in [pr] + (pr.get('goals') or []):
        for s in g.get('steps') or []:
            walk(s.get('actions'), f"{g.get('name')}:step[{s.get('index')}]")
            # Class 2: write to %x% shape
            text = (s.get('text') or '').lower()
            if 'write to %' in text:
                actions = s.get('actions') or []
                if actions:
                    last = actions[-1]
                    if last.get('module')!='variable' or last.get('action')!='set':
                        issues.append(f'[write-to-tail] {path} {g.get("name")}:step[{s.get("index")}]: tail not variable.set')
                    else:
                        val = next((p for p in last.get('parameters') or [] if p.get('name')=='Value'), None)
                        if val and val.get('value') != '%!data%':
                            issues.append(f'[write-to-tail] {path} {g.get("name")}:step[{s.get("index")}]: tail Value={val.get("value")!r} (want %!data%)')
for i in issues: print(i)
print(f'{len(issues)} issue(s)')
```

---

## Suggested ordering

1. **Class 1 first** — pure C# fix in two contained spots (`ResolveGoalCallPaths` + `GoalCall.GetGoalAsync`). Deterministic. Removes 8 errors per rebuild.
2. **Class 2 next** — validator extension in `builder.validate`. Removes the recurring builder.actions bug + serves as the template for any other `write to %x%` idiom misuse.
3. **Class 3 last** — same validator file, different rule. Lowest priority since it's intermittent.

Add the audit script to the build verification path (e.g. run it after `builder.goalsSave` and reject the save if any issue fires) so any future regression of these classes — or new ones — gets caught at build time instead of at the next self-rebuild.

---

## Files touched on runtime2 that this report depends on

- `PLang/app/modules/builder/code/Default.cs` — `ResolveGoalCallPaths` (added by me, calls `GoalCall.GetGoalAsync`).
- `PLang/app/modules/builder/validateStepActions.cs` — new validator action (drops planner hallucinations, appends explicit module.action tokens from step text).
- `PLang/app/modules/builder/code/IBuilder.cs` — added `ValidateStepActions`.
- `os/system/builder/BuildStep/Start.goal:15` — calls `builder.validateStepActions` pre-compile.

All of these are already on `path-polymorphism` via the runtime2 merge (commit `1910f1bc5`, hand-patched .pr snapshot included so the merged tree is buildable).
