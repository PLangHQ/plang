# Handoff — branch `variable-as-value`

**State:** all work committed + pushed. HEAD = `61329b492`. `--debug` is usable again.
**Goal of branch:** a full-match `%x%` is a first-class `variable` (type `variable`, name-only),
resolved at the consumer's `.Value<T>()`, never coerced at load. Driving it through the
PLang builder until `plang build Tests/Scratch/Hello.goal` (just `- write out "hello world"`) builds.

---

## THE ONE OPEN BUG (start here)

`plang build Tests/Scratch/Hello.goal` crashes with `BuilderPlannerFailed(400)`. That message
is **misleading** — it is NOT the planner/LLM. Diagnosed via `--debug`:

`os/system/builder/BuildStep/Start.goal:6` — `- set %step% = %goal.Steps[planStep.index]%` —
resolves `%step%` to **null**, then converting null→`step` declines
("'step' cannot be created from it" — same null→type class as the actor work).

Run to reproduce + see the dispatch panel:
```bash
cd /workspace/plang
PlangConsole/bin/Debug/net10.0/plang.exe build Tests/Scratch/Hello.goal \
  '--debug={"variables":[{"name":"step","event":"onchange"},{"name":"planStep","event":"onchange"}],"maxLength":500}' > /tmp/dbg.txt 2>&1
```
The dispatch panel (near the end of `/tmp/dbg.txt`) shows:
```
Step    .pr value: step [variable]            final: Error: %Step% holds a null — 'step' cannot be created from it.
Actions .pr value: planStep.actions [variable] final: planStep.actions      <-- KEY TELL
```

**Root suspicion (verify, don't trust):** dotted/indexed navigation in the variable-as-value
chain isn't applying the path. `%planStep.actions%` resolves to the *bare variable*
`planStep.actions` (its name), not the navigated value; `%goal.Steps[planStep.index]%`
resolves to null. So either:
  1. `variable.Value()` (`PLang/app/variable/this.cs`) = `Context.Variable.Value(Name)` resolves
     `Get(Name)` to the ROOT and drops the `.actions` / `[index]` navigation that the old
     text-template path used to apply, **or**
  2. the foreach `%item%` injection in the goal-call by-value path isn't setting `planStep`
     (so `planStep.index`/`planStep.actions` are null).

Trace `%planStep%` first: is it set at all inside `BuildStep/Start`? (The `--debug` watch only
showed `planStep=(undefined)` in `BuildGoal/Start`, the wrong "Start" — it's set, if at all,
by the foreach injection one goal deeper.) If `planStep` IS set but `%planStep.actions%` still
comes back as the bare variable → it's #1 (navigation drops the path). If `planStep` is unset
→ it's #2 (foreach `%item%` injection). The navigation fix belongs in `variable.Value()` /
the store's `Value(name)`/`Get(name)` — apply the path (`.x`, `[i]`) after resolving the root.
**This is a careful core-resolution change — do it fresh, small, and re-run the build each step.**

Tracing: use the `LSP` tool (ToolSearch `select:LSP`), not grep — symbol→type→impl by data flow.

---

## Foreach path of the builder (the call chain to Hello build)
`Build.goal` → `BuildGoal.goal` → `BuildGoal/Start.goal:16`
`- foreach %plan.steps%, call BuildStep/Start planStep=%item%, on error call HandleBuildFailure`
→ `BuildStep/Start.goal:6` `- set %step% = %goal.Steps[planStep.index]%` (← crash) → `:7 call Compile`
→ `:15 - builder.validateStepActions step=%step%, actions=%planStep.actions%, write to %planStep.actions%`
`HandleBuildFailure` does `throw %!error%` (re-raise — works now).

---

## What landed this session (all pushed; do NOT re-derive)
- `%ref%` is a first-class `variable` (name-only); `IRawNameResolvable`; resolved at `.Value<T>()`.
- `Data.As<T>()` / `As<T>(context)` — typed view, no clone, no resolve (shares `_type`/Properties/Context).
- `Data.Value<T>()` short-circuits `variable` (`T.Create(nameRef, this)`).
- container deep-render (list/dict `Value(data)` door); `!data` snapshot (breaks `!data`↔`msg` loop);
  goal-call **by-value** injection (`if param.Peek() is variable → Set(name, await param.Value())`);
  `operator` type registration (`RegisterModuleChoiceTypes`, `app/this.cs:322`).
- codeanalyzer **F1 swallow class** fixed (a/b/c/e) — element-conversion errors now surface
  (`type/catalog/Conversion.cs` `ConvertElementsInto`; navigator sites in `variable/list/this.cs`).
- `throw %!error%` re-raises from the Message slot too (`module/error/throw.cs:45`).
- **debug tooling**: `BeforeStepHandler` now PEEKs params for display (was `await p.Value()` →
  perturbed execution + NRE'd). `--debug` is non-perturbing again — this is why the build now
  runs to its real failure instead of dying in the debug handler.
- `validateStepActions` reads Step/Actions via `.Value()` (async) — lazy-param contract.

## Deferred (todos.md)
- **Source-gen object-initializer dispatch rewrite** — full plan in
  `.bot/variable-as-value/coder/source-gen-lazy-params-plan.md`. Only enablers landed this branch.
- `Lower<T>` removal.

## Watch-outs / Ingi's standing calls
- **Assume Context is never null** — let NullExceptions surface; don't add `?`-guards to hide them.
- **Returning null is "very very bad"** / **never swallow errors** — surface them.
- **No null→null short-circuit** in `ICreate.Create` / `Conversion.cs` — Ingi rejected it twice.
  (So the null→`step` decline is *correct* behavior; the fix is to stop `%step%` being null,
   not to make null→type silently pass.)
- Editing C# for debug is **last resort** — the debug Peek fix was the exception (the tool itself
  was broken). Prefer `--debug` property bag over code edits.
- Use `--debug` per `Documentation/v0.2/debugging-builder-failures.md`; validate the *data* before
  blaming the *code*.
- Production edits via Edit/Write only (console-visible). Grep scoped to `PLang/`, not all of `/workspace/plang`.
