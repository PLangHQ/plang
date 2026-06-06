# Handoff to coder — builder compile prompt is dark (Fluid can't read native collections)

From: builder (scalars-as-native). This is the **root cause** behind the
`assert → error.throw` mis-map you flagged in `builder/handoff-from-coder.md` #2.
It is **not** a prompt/teaching bug and **not** LLM non-determinism — it's a C#
binding regression in the Fluid template engine. Handing it back to you because it
lives in `PLang/` C# (Ingi's "treat C# runtime as fixed, fix at PLang layer" rule),
and I confirmed there is **no PLang-layer workaround**.

Cache fix (your #1) is verified sound — built `ListOps` twice, cache-write and
cache-read paths produce byte-identical correct mappings (`assert.equals`,
`assert.isTrue`, `assert.isFalse`, `contains`, `greaterThan`). Nothing to do there.

---

## The bug in one sentence

Every step-compile's **user message is missing two whole sections** — the planner's
picked actions *and* all per-action schemas/notes/examples — because Fluid can't
read PLang native `dict`/`list`/`JsonNode` values, so the compiler LLM guesses
blind on every build.

## Evidence (deterministic)

Repro (working dir `Tests/`, binary `PlangConsole/bin/Debug/net10.0/plang`):

```
cp App/CallStack/.build/handledflagsetwhenrecoverysucceeds.test.pr /tmp/hf-good.pr
../PlangConsole/bin/Debug/net10.0/plang build \
  --build='{"files":["App/CallStack/HandledFlagSetWhenRecoverySucceeds.test.goal"],"cache":false}' \
  '--debug={"llmTrace":true,"maxLength":20000,"goal":"Compile"}' > /tmp/t.txt 2>&1
cp /tmp/hf-good.pr App/CallStack/.build/handledflagsetwhenrecoverysucceeds.test.pr   # restore
```

1. **Planner is correct.** Raw planner response for this goal:
   ```
   steps: [
     {index:0, actions:[error.throw, error.handle, variable.set]},
     {index:1, actions:[assert.isTrue]},
     {index:2, actions:[assert.isNull]}
   ]
   ```

2. **The compile user message (`%buildStepUserMsg%`) renders these sections EMPTY:**
   ```
   ## What to map to — the planner picked these actions
   (empty)
   ## Action detail (parameters + notes)
   (empty)
   ## Variables in scope (from prior steps)
   (empty)
   ```

3. **The compiler self-reports it every call** (this is accurate, not an excuse):
   `confidence: VeryLow`, `errors:[{key:insufficientContext, message:"Planner's
   available actions … were not provided…"}]`, and it invents an `error.throw`
   `Condition` param that doesn't exist. Steps 1 & 2 → `error.throw` instead of
   `assert.isTrue`/`assert.isNull`.

4. **Global, not assert-specific.** `Modules/List/ListOps.test.goal` has the SAME
   empty sections — it only survives because `set`/`add`/`equals`/`sort` are obvious
   enough for nano to guess from step text alone. The whole per-action markdown
   teaching layer (`os/system/modules/*/*.{notes,examples}.md`) has been dark on
   **every** build. This is why the suite sits at 271/309 and why rebuilds are
   non-deterministic.

## Root cause

`os/system/builder/llm/CompileUser.llm` iterates `{% for a in planStep.actions %}`
and `stepActionDetails.template` iterates `{% for actName in planStep.actions %}`.
`%plan%` is built with `set … type=json`, which TypeMapping maps to
`System.Text.Json.Nodes.JsonNode` (`PLang/app/module/variable/set.cs:127`). So
`planStep` is a `JsonObject` and `planStep.actions` a `JsonArray`.

- PLang's own resolver reads it fine (`%planStep.actions%` works) — `app/variable/
  navigator/Dictionary.cs` has a dedicated `IDictionary<string,JsonNode?>` arm whose
  comment documents this exact trap.
- **Fluid does NOT use PLang's navigator.** Binding site:
  `PLang/app/module/ui/code/Fluid.cs` →
  ```csharp
  foreach (var kvp in action.Context.Variable.GetAll())
      fluidContext.SetValue(kvp.Key, FluidValue.Create(kvp.Value.Value, options));
  ```
  `FluidValue.Create` on a `JsonObject` reflects over CLR members and finds only
  `Count/Options/Parent/Root` — never the JSON keys → `planStep.actions` is nil →
  empty loop.

## Why there's no PLang-layer fix

Coercing to a native `dict`/`list` (`app.type.{dict,list}.@this`) does NOT help:
those types extend `app.type.item.@this` (only `IBooleanResolvable`) and implement
domain interfaces (`IEquatableValue`, `IListLeaf`, …) — **not** `IEnumerable` or
`IDictionary`. Fluid can only iterate/navigate real .NET `List<T>`/`Dictionary`/
POCOs (that's why the action *catalog* `%actions%`, a real `List<…>`, renders, but
the plan doesn't). So the only shapes Fluid reads are plain CLR collections.

## Suspected fix (your call on shape)

In `Fluid.cs`, unwrap PLang native collections / `JsonNode` to plain
`Dictionary<string,object?>` / `List<object?>` **before** `FluidValue.Create`, in the
variable-binding loop (and the explicit-parameters loop right below it). There's
already an `UnwrapFluid` helper but it unwraps Fluid wrappers *after* creation for
the `formal` filter — you need the input-side mirror (CLR-materialize the native/
Json value going in). Likely the native `dict`/`list` types already expose a
to-CLR/normalize path (`text.Convert serializes native dict/list` per the recent
commit; `data.Normalize`) you can reuse for JsonNode too.

## Validate broadly after the fix

This touches **every** build, so re-validate widely (watch `builderVersion`):
- `HandledFlag` rebuild: steps 1,2 must be `assert.isTrue`/`assert.isNull`, step 0
  `error.throw | error.handle(variable.set)`, `on error` modifier preserved.
- `ListOps` rebuild: must stay all-green (regression guard).
- The compile user message's "What to map to" / "Action detail" / "Variables in
  scope" sections must populate.
- Then the full `Tests/` suite should climb above 271/309, and
  `Tests/ScalarsAsNative/Stage*/` should build.

## Done on the PLang side (builder)

- Added `rawResponse: %compileResult!RawResponse%` to the compile trace record in
  `os/system/builder/BuildStep/Start.goal` (Ingi's request — compiler raw response
  wasn't in `trace.stepPasses`, only the parsed `%compileResult%`). **The `.pr` is
  NOT rebuilt** — rebuilding the builder while the compiler is blind risks
  self-rebuild corruption. Rebuild `BuildStep/Start.goal` after your Fluid fix lands
  so source and `.pr` re-sync.
