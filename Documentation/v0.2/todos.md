# TODOs

## Replace `GoalCall` parameter type with `list<action>` everywhere

**Date:** 2026-04-22

**Vision.** A PLang step should collapse to a thin action chain with no hidden goal-hop. Today every callback or event hook takes a `GoalCall` — forcing the developer to wrap a single clause in a goal:

```plang
Start
- before goal call LogBefore        ← forced goal hop for one line of work

LogBefore
- write out "Before Goal"
```

What we want:

```plang
Start
- before goal write out "Before Goal"          ← no goal wrapping needed

- get http://...file.mp4 on stream write to %video%   ← same idea for streaming
```

The step's trailing clause compiles directly into the callback's `Actions([list<action>])` list. No wrapper goal, no indirection.

**Why this beats decomposing `GoalCall` into primitives.** An earlier change split `goal.call GoalName=[goal.call]<struct>` into `Name + Parameters` (sibling fields). That violated OBP — it took a cohesive object and smeared it across primitives that the caller has to reconstruct. The real problem isn't that `GoalCall` is structured, it's that *callbacks using `GoalCall` force a goal wrapper around simple work*. Replacing callback params with `list<action>` removes the wrapper without decomposing anything. A single `goal.call` is still expressible inside the chain when you actually want to call a goal.

**Precedent.** `error.handle.Actions([list<action>?])` already exists (current branch). Already-working pattern: inline recovery without a wrapper goal. Extend that to all other callback sites.

**Scope.** Every remaining `Data.@this<GoalCall>` on an action class becomes `Data.@this<List<Action>>` (or stays — see "keep as-is" below). Grouped by intent:

| Action | Param | Today | Proposed |
|---|---|---|---|
| `app.run` | `GoalName` | `GoalCall` | `Actions` — same refactor as goal.call (but OBP-safe: it's a chain, not primitives) |
| `event.on` | `GoalToCall` | `GoalCall` | `Actions` — `- before goal write out "..."` compiles inline |
| `mock.action` | `GoalToCall` | `GoalCall` | `Actions` — what runs in place of the mocked action |
| `http.download` | `OnProgress` | `GoalCall` | `Actions` — per-chunk progress chain |
| `http.upload` | `OnProgress` | `GoalCall` | `Actions` — same |
| `http.request` | `OnStream` | `GoalCall` | `Actions` — per-chunk stream chain |
| `llm.query` | `OnToolCall` | `GoalCall` | `Actions` — tool-call handler |
| `llm.query` | `OnValidateResponse` | `GoalCall` | `Actions` — validator chain |
| `llm.query` | `OnStream` | `GoalCall` | `Actions` — stream chunk chain |
| `error.handle` | `Goal` (deprecated) | `GoalCall` | **remove** — `Actions` already landed |
| `goal.call` | `GoalName` | `GoalCall` | **keep as-is** — direct call, OBP-correct as a structured value |

`llm.query.Tools` (`List<GoalCall>`) is a separate concern — it advertises callable tools to the LLM, which expects name+schema. Probably stays `List<GoalCall>` or becomes a named-action catalogue; needs its own design pass.

**Keep as-is: `goal.call.GoalName` and the `[goal.call]` type.** Direct calls stay structured. `goal.call` is the action whose *purpose* is to invoke a named goal with named params — its parameter IS a goal reference, not a list of work-to-do. Keep `GoalCall` as a first-class type for direct invocation, remove it from every "deferred callback" context.

**Migration cost.** Roughly 20 `.pr` files plus the 3 builder `.pr` files reference the old `GoalToCall` / `OnProgress` / etc. shapes. All need a rebuild. Do this on a fresh branch in a single sweep; don't keep `[LegacyAlias]` shims — commit to the new shape, rebuild everything, no legacy baggage. (This is Ingi's explicit preference: clean cut over graceful deprecation.)

**Nav fix already in the tree.** `error.handle` already stamps the enclosing step onto nested actions in `RunRecovery`. When rolling out `Actions` to `event.on`, `http.*`, `llm.query`, etc., reuse that stamping pattern so nested goal.calls inside the chains resolve sibling sub-goals.

**Runtime injection semantics to preserve.**
- `event.on`'s current `GoalCallbackAttribute.Injects` semantics ("`chunk` becomes the variable name the callback receives") — map to variable injection at chain entry, the same way goal params flow today.
- `error.handle`'s `!error` convention — the failed `IError` was injected as a `!error` param into the called goal. With `Actions`, the failed error should be available as a variable (e.g. `%!error%`) that any action in the chain can read.
- `llm.query.OnStream` etc. — each streamed chunk is a callback invocation. With `Actions`, the chunk becomes a variable the chain references; chain fires once per chunk.

**Related cleanup.**
- `GoalCallbackAttribute` (`App/modules/Attributes.cs`) — its `Injects` concept migrates to a "this variable is populated when the chain fires" convention. Probably becomes a parameter-level attribute on the enclosing action's callback param, or a runtime doc string.
- Dispatch helper in `Action.@this.RunAsync` — same path works for chain entries since each is already a full `Action.@this`.
- Template/catalog — `list<action>` already renders; no change needed.

**Order of work when picking this up.**
1. One new branch off `runtime2-build-trace-viewer`.
2. Convert one non-`error.handle` callback as a pilot (suggest `event.on.GoalToCall` — high impact, narrow surface).
3. Rebuild the builder's own `.pr` files first (will drift; hand-patch if needed, as done earlier on this branch).
4. Sweep the remaining callbacks.
5. Rebuild every user `.pr` that uses any of the touched params.
6. Delete `error.handle.Goal` deprecated alias.
7. Update `BuildGoal.llm` examples.
8. Update `Documentation/v0.2/action-catalog.md` with the "callbacks are action chains" section.

Leaving `goal.call.GoalName` and the `[goal.call]` type in place is deliberate. Revisit only if the sweep reveals a different shape is cleaner.

---

## Move file deserialization from TypeMapping to Channels.Serializers

**Date:** 2026-04-08

**Problem:** `DefaultFileProvider.Read` (`PLang/App/modules/file/providers/DefaultFileProvider.cs:40`) uses `TypeMapping.TryConvertTo` for JSON-to-object deserialization. This is a raw utility that knows nothing about the domain. When it deserializes a `.pr` file into a Goal, the Goal is disconnected — no `App`, no `Step.Goal` back-references, no sub-goal wiring. This causes NullReferenceExceptions when runtime code tries to navigate the object graph (e.g., `Action.RunAsync()` at `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs:55`).

**Solution:** Route file deserialization through `Channels.Serializers` (`PLang/App/Channels/Serializers/this.cs`), which already has a registry keyed by extension and content type.

### Changes needed:

1. **Add context to `ISerializer`** (`PLang/App/Channels/Serializers/Serializer/this.cs`)
   - Add `Context` parameter to deserialize methods (or pass via `DeserializeOptions`)
   - Most serializers (JSON, text) ignore it. Domain-aware serializers use it.

2. **Create a `.pr` serializer**
   - Register for extension `.pr`
   - Deserialize: JSON parse into Goal, then wire back-references
   - Goal should own wiring its children — when something is set on Goal, Goal propagates to Steps and sub-goals internally
   - Back-references needed: `Step.Goal = goal`, `subGoal.Parent = goal`
   - **Cannot store `App` or `Context` on Goal** — goals are shared/cached across requests. Per-request state must be passed as parameters, not stored on shared objects. See CLAUDE.md rule: "Per-request state is a parameter, per-object state is a property."

3. **Simplify `DefaultFileProvider.Read`**
   - Replace `TypeMapping.TryConvertTo(text, clr)` with `serializers.Deserialize(text, extension, context)`
   - File provider reads bytes/text and hands off to the serializer. No more type conversion logic.

4. **Remove JSON deserialization from `TypeMapping.TryConvertTo`**
   - `PLang/App/Utils/TypeMapping.cs:317-322` — the `string → complex type via JsonSerializer.Deserialize` path moves to serializers
   - TypeMapping goes back to being about primitive type conversion only

### Key files:
- `PLang/App/modules/file/providers/DefaultFileProvider.cs` — current file read logic
- `PLang/App/Channels/Serializers/this.cs` — serializer registry
- `PLang/App/Channels/Serializers/Serializer/this.cs` — ISerializer interface
- `PLang/App/Utils/TypeMapping.cs:297` — TryConvertTo with JSON deserialization
- `PLang/App/Goals/Goal/this.cs` — Goal entity, needs to own its child wiring
- `PLang/App/Goals/this.cs:306` — LoadFromFileAsync, currently does manual wiring

---

## Replace Console.Write with AskUser in build app confirmation

**Date:** 2026-04-10

**Location:** `PLang/App/this.cs` — `Start()` method, build mode section

**Problem:** `Console.Write("No app found... Create new app? (y/n)")` uses raw Console I/O. When the AskUser module is implemented, this should use it instead so the prompt works through any UI channel (CLI, web, IDE), not just console.

**Fix:** Replace `Console.Write`/`Console.ReadLine` with `AskUser` action when available.

---

---

## %Now.ToString("format")% doesn't resolve in variable.set

**Date:** 2026-04-10

**Problem:** `- set %traceId% = %Now.ToString("yyyyMMdd-HHmmss")%_%goal.Name%` produces `_Start` — the `ToString("...")` call doesn't resolve. Fell back to `%Now.Ticks%` which works. Method calls with string arguments inside `%variable%` expressions may not be supported or the quotes conflict with the step's own quoting.

**Expected:** `%Now.ToString("yyyyMMdd-HHmmss")%` should resolve to e.g. `20260410-183500`.

---

---

## Goal-Backed Statics (Dynamic Properties)

**Date:** 2026-04-10

**Context:** While building `IStatic` for the timer module, we realized module statics shouldn't be a C# class (`AppStatics`) or a helper method (`GetOrCreateStatic`). They should be a PLang goal that the runtime calls through property access.

**The Design:**

`app.Statics["timer"]` resolves to a goal call — runs `/system/app/statics/this.goal` via `app.run`. The Statics goal manages the dictionary. It's a PLang service, like events are goals.

```plang
Statics
/ System goal managing module-scoped static storage
- list.get %key% from %app.static%, return %data%
```

When a key doesn't exist, return uninitialized Data (null/nothing), not an error.

**Scope levels via the same pattern:**
- `app.Statics["timer"]` — app lifetime, survives across contexts
- `goal.Statics["timer"]` — goal-scoped, cleared when goal ends  
- `context.Statics["timer"]` — context lifetime (current default)
- `step.Statics["timer"]` — step-local

Each scope level is backed by a goal at the appropriate system path. `timer.start scope=app` writes to `app.Statics["timer"]`, `scope=goal` writes to `goal.Statics["timer"]`.

**Bigger idea: Goal-backed dynamic properties.** This pattern generalizes. Any property on app/goal/context/step could be a dynamic property backed by a goal. `app.Statics` is one instance. `app.Config`, `app.Secrets` could follow the same pattern — access triggers a goal, the goal manages the storage. The C# runtime provides the property resolution mechanism. The behavior lives in PLang goals.

**Current state:** `IStatic` exists with `ConcurrentDictionary` on context. Timer module works. The goal-backed approach replaces the C# backing with PLang goals. `IStatic` interface stays — the source generator wires it to the goal-backed storage instead of a direct dictionary.

**Key files:**
- `PLang/App/modules/IStatic.cs` — current interface
- `PLang/App/Actor/Context/this.cs` — `GetModuleStatic()` (to be replaced)
- `PLang/App/this.cs` — app-level statics (to be replaced with goal-backed property)
- `PLang.Generators/LazyParamsGenerator.cs` — wires IStatic to Static property
- `PLang/App/modules/timer/` — first consumer of IStatic

---

---

## Step-level rebuild in builder

**Date:** 2026-04-15

**Problem:** The builder rebuilds all steps in a goal — no way to rebuild a single step. When one step has a bad .pr mapping (e.g., LLM encoded JSON value as string), you have to rebuild the entire goal and risk the LLM breaking other steps.

**Solution:** Add `steps` filter to `--build` options: `--build={"files":"BuildGoal.goal","steps":[8]}`. Skip the full BuildGoal LLM pass, go straight to BuildStep for the specified indexes. The infrastructure exists — BuildStep already does single-step LLM passes.

**Changes needed:**
1. `PLang/App/Build/this.cs` — add `public List<int> Steps { get; set; } = new();`
2. `PLang/Executor.cs` — parse steps from build JSON, sync to `%!build.steps%`
3. `system/builder/BuildGoal.goal` — conditional: if `%!build.steps%` has items, call BuildStep directly for each index instead of BuildGoalCore
4. New `RebuildStep` sub-goal wrapping BuildStep with template rendering and trace

---

---

## Rename file.save Value property to Content

**Date:** 2026-04-15

**Problem:** `DefaultFileProvider.Save` has `action.Value?.Value` — the parameter is named `Value` and `Data` also has `.Value`, making `Value.Value`. Confusing naming collision.

**Fix:** Rename the `Value` property on `file.save` to `Content`. Update all .pr files that reference `"name": "Value"` on file.save parameters. Update builder prompt examples.

**Key files:**
- `PLang/App/modules/file/save.cs` — rename property
- All `.pr` files with `file.save` parameters
- `PLang/App/modules/file/providers/DefaultFileProvider.cs` — update reference

---

### Open question:
How does the deserialized Goal get `App` if we can't store it on the Goal? The caller (GoalCall, Goals.LoadFromFileAsync) sets `goal.App` after loading — that's acceptable because it's the loading path, not per-request state. The rule is about not caching *context* (which is per-request). `App` is per-application and set once during loading. But this needs discussion — is `Goal.App` actually safe on cached goals, or should goals navigate to App differently?

---

## 2026-04-21 — LLM error: reuse --debug truncation/grep options

User (Ingi) asked, while diagnosing List/Math JsonParseError: can we apply the same `maxLength`, `grep`, and variable-filter options that `--debug={...}` supports to the LLM provider's error reporting (and maybe the successful-response trace too)?

Context: today `OpenAiProvider` throws `JsonParseError` with a fixed short message. To see the raw response I had to temporarily edit the source. If LLM errors accepted the same debug options shape (`--debug={"llm":{"maxLength":2000,"grep":"module"}}` or similar), we'd get truncated raw responses on demand without hand-editing the provider.

Scope: probably a `App.Debug.Llm` counterpart to the existing debug plumbing, consumed by `OpenAiProvider.QueryAsync` and any other provider. The same options struct used by --debug's goal/step output would make developer mental model consistent.

Not fixed — captured as a developer-ergonomics improvement.

---

## 2026-04-21 — Builder bug: LLM emits dotted module paths, builder should auto-resolve

Observed during Wave 4 per-folder rebuild of `Tests/` on `runtime2-green-plang-tests`. Even after adding explicit prompt rules ("Module names never contain dots") to `system/builder/llm/BuildGoal.llm`, the LLM still periodically produces actions like:

- `condition.assert.equals` — should be two actions: `condition.if` (or similar) + `assert.equals`
- `condition.event.on` — should be `event.on` alone
- `signing.error.handle` — should be `signing.sign` + `error.handle` modifier
- `timeout.after.after` — should be `timer.sleep` or `timeout.after` as a modifier
- `convert.jsonToObject` — should route to whatever real conversion action exists

The builder currently propagates these as `ActionNotFound` build failures. They break the build and require a rebuild attempt to see if the LLM recovers.

**The rule:** the builder owns the semantic mapping layer. If the LLM produces a module name that doesn't exist in the action registry, the builder should NOT pass the failure through — it should either:
1. Detect the dotted-path pattern (module name contains `.`), split on the first dot, and validate the resulting two-action form against the registry; accept if both halves resolve.
2. Otherwise, re-prompt with an explicit "your `module` value '%offending%' doesn't exist — available modules: [...]" hint and try again. Same retry loop that `validateResponse` already uses.

Where: `system/builder/ApplyStep.goal` / `MergeStep` / `ValidateResponse` chain. The validation error is caught today (I saw `Validation error: Actions not found: ...` followed by abort) — just needs a retry-with-hint path instead of giving up.

Motivation: LLM non-determinism means a prompt rule alone cannot guarantee clean output. The builder is the deterministic layer; it should validate and recover. A dotted-path failure reaching the test suite is a builder bug, not a test bug.


## Module-scoped debug instrumentation

**Date:** 2026-04-22

**Problem.** `--debug` today has a growing pile of top-level ad-hoc flags: `llmTrace`, `resolveTrace`, eventually `httpTrace`, `cacheTrace`, `settingsTrace`, etc. Each is a one-off Console.Error.WriteLine block baked into a specific module's provider, gated by a global Debug property. This doesn't scale — every module that wants diagnostic output invents its own flag, and the Debug class accumulates dozens of booleans the source generator has to wire through.

Worse, when a module lacks a flag for the signal you need, the only recourse is to edit C# (Console.Error.WriteLine). We hit this during the builder investigation: the LLM provider logged its **request** (`llmTrace`) but not its **response**, so pinning down a truncation-vs-parse-error distinction required C# prints that then had to be stripped. The same pattern will recur for every module.

**Vision.** Formalize debug as a per-module / per-action opt-in:

```
--debug={"modules":["llm"]}                         → all actions in the llm module emit their diagnostic info
--debug={"modules":[{"name":"llm","actions":["query"]}]}  → just llm.query
--debug={"modules":[{"name":"http"},"llm"]}         → multiple modules, mixed granularity
```

Each module declares what debug info is *useful* for each action — that's codebase knowledge that accrues over time. A module with a `[DebugOutput]`-style attribute (or a generated hook) publishes what it will emit; the runtime suppresses calls when the module isn't in the debug scope.

**Zero-cost when off.** The source generator already sees every action at build time. It can:
- Generate a per-module `IsDebugEnabled(actionName)` lookup populated once at startup from `--debug.modules`.
- For each `DebugLog(...)` call site, wrap the emission in `if (__debug_enabled) { ... }` so inactive modules pay only a boolean check. Or go further and generate two variants of ExecuteAsync — one with debug calls inlined, one without — and dispatch at Initialize based on scope.
- Alternative: an `ILogger`-style handle passed into the action context, with a no-op implementation as default. Source gen registers the correct logger per module based on the scope.

**Migration path.** Keep `llmTrace` and `resolveTrace` as temporary aliases that translate to `modules:["llm"]` and `resolveTrace:true` (the resolveTrace kind is orthogonal — it's about variable resolution, not any single module, so it probably stays top-level). New module diagnostics never get a top-level flag; they go through the module-scoped mechanism from day one.

**First targets to onboard.**
- `llm.query` — currently logs request; should log response (content, finish_reason, token counts, cached flag, model) when enabled. Closes the specific gap we hit.
- `http.request` — URL, method, status, timing, headers (redacted).
- `cache.wrap` — hit/miss + key.
- `settings.get/set` — key + value type.
- `file.read/save` — path + size.

**Action-return logging.** Related but distinct: regardless of per-module debug, `--debug={"level":"action"}` should log each action's **returned Data** as a single line at the boundary (`→ module.action returned: Value=<...>, Success=True`). Today it shows the variable state BEFORE/AFTER but not the action's own return artifact, so "what did this action produce, exactly?" requires Console prints. This addition is independent of the module-scoped scheme and can land sooner.

Where: `PLang/App/Debug/this.cs` (debug scope parser + formatter), `PLang.Generators` (codegen for zero-cost gating), per-module handlers (replace Console.Error.WriteLine with DebugLog calls).

---

## `on rollback` — compensating actions on post-retry failure

**Date:** 2026-04-22

**Idea.** Give every step a *rollback clause* that fires when the step ultimately fails (retries exhausted, error not handled). Two forms:

```plang
- call MakeManyApiCalls, on rollback call DoRollbackOnManyApis
- add rollback call CleanupTempFiles          # standalone registration
```

The first form attaches a rollback to a single step (like `on error` does, but distinct semantics — rollback runs *after* the step is definitively failed, not as a recovery attempt). The second form accumulates rollbacks in a step-ordered stack so that a mid-flow failure can unwind the prior successful steps in reverse.

**Why it's useful.** PLang workflows often chain API calls, DB writes, or file operations. Today there's no first-class way to say "if this transaction aborts, undo steps 1..N in reverse." Users hand-roll the compensating logic inside `on error` chains, which mixes recovery intent with compensation intent.

**Relation to `error.handle`.** `on error` is *during-action* recovery (retry, replace, continue). `on rollback` is *post-failure* compensation (unwind effects already committed). They compose: a retry can succeed (no rollback) or fail (triggers rollback).

**Out of scope for now.** This is a sketch, not a spec. Revisit when we finalize the transaction / saga story.

Motivation surfaced: during the build-trace-viewer branch, chasing a null-valued `%!data%` required four ad-hoc C# Console.Error.WriteLine additions spread across `OpenAiProvider`, `Action.RunAsync`, `Variables.Set`, and `validateResponse`. Only two of those were truly missing from `--debug`; the others were me flailing. With action-return logging + module-scoped LLM response trace, zero would have been needed.

### Further design (2026-04-23)

**Closure capture at registration time.** The killer property is that rollback arguments bind *when the step succeeds*, not when the rollback fires. Booking flow: airline call succeeds → `%ticket.id%` is captured into the rollback's argument list → hotel call fails → rollback fires with the real ticket id, not a placeholder. The rollback "knows what to undo" because the successful step's outputs were frozen into its closure. This is what makes it feel native — variables are already PLang's universal currency, and the rollback just binds them.

**Per-request scope invariant.** The rollback stack lives for the duration of one request and is always empty when the request ends. Success path discards the stack (nothing to undo, the work is done). Failure path drains it LIFO. No persistence, no cross-request contamination, no "yesterday's rollbacks firing tomorrow." This answers the scope question automatically — you don't need a named transaction concept.

**Mental model is database rollback, not try/finally.** `try/finally` is resource-local cleanup ("close this file"). Database rollback is *semantic*: N committed operations form one logical unit; if anything fails, undo them in reverse. PLang rollback is the DB model extended past the DB boundary to anything with side effects (HTTP, filesystem, cloud APIs). SQL's vocabulary is free for the taking: `BEGIN`, `COMMIT`, `ROLLBACK`, `SAVEPOINT`.

**`SAVEPOINT` — partial rollback to a named checkpoint.** `- savepoint after-booking` before the hotel call lets a failure roll back just the hotel leg and retry, instead of unwinding all the way to zero. Useful when some work is expensive to redo (the airline booking is still good — only the hotel leg failed). Borrow directly from SQL semantics.

**Rollback-of-rollback failure.** If airline-rollback succeeds but hotel-rollback returns 500, the stack shouldn't halt — a half-rolled-back state is usually worse than a fully-attempted one. Policy: best-effort, continue unwinding, collect all rollback failures into the final error. The caller sees "request failed AND these compensations also failed" and can surface both.

**Distinct from `on error`.** `on error` is *during-action* recovery (retry, substitute, continue). `on rollback` is *post-failure* compensation (unwind already-committed effects). They compose cleanly: if `on error retry 3 times` exhausts retries, the step is definitively failed and `on rollback` fires. Both primitives in one step is legal and expected.

---

## Builder — skip the JSON mapping pass

**Date:** 2026-04-23

**Idea.** The builder today runs two LLM passes per step: (1) natural language → formal action-summary language, (2) formal language → JSON `.pr`. Pass 2 is mostly syntactic shuffling, not thinking. If the formal language has bounded grammar, C# can parse it deterministically *and* validate against the module registry in the same pass. One whole LLM call skipped per step. At build scale, that's massive.

**Why this isn't "parsing natural language".** CLAUDE.md's hard rule is *never parse user step text* — because natural language is unbounded. But the formal action summary is an IR we designed ourselves, with known grammar. Parsing your own IR is fine; parsing user English is the sin. The rule stays intact.

**Tradeoff — expressiveness.** The formal language must losslessly encode everything `.pr` JSON does today: nested objects, lists, lazy `%var%` refs, `on error` modifiers, `into` bindings, callbacks for `foreach`, action return mappings, `on rollback` chains. The moment you need escape rules for strings containing `=` or `%`, you've built a real grammar with a real parser. Worth it if the formal stays tight; painful if it grows horns.

**Middle path — structured output.** Keep JSON, but switch pass 2 from "LLM generates text that happens to be JSON" to "LLM fills a schema via structured output / tool-use." Same determinism as parsing (no malformed JSON), but the grammar is the schema, not a DSL you maintain. Saves drift, not the call.

**Decision point.** Go full skip-the-JSON only if the formal IR is rich enough to be unambiguous across all current action shapes AND we're willing to own a parser. Otherwise, structured-output is the pragmatic win. Worth prototyping pass 2 via tool-use first to measure token savings vs full DSL commitment.

**Connection to `.pr` format.** If the formal language becomes the storage format too (`.pr` = formal text, not JSON), the runtime loader parses it at startup the same way the builder does. Single IR, one parser, no JSON round-trip anywhere. Biggest prize, biggest commitment.

---

## Catalog — extend `[PlangType]` / `Catalog` to actions too

**Date:** 2026-04-24

**Status.** Types are now structured — `App.Catalog.@this` exposes `Types` (list of `TypeEntry`, each carrying Name + Kind + Fields/Values) and is JSON-serializable. The Liquid prompt template consumes pre-rendered `TypeNames`/`TypeSchemas` convenience strings; the structured form is there for trace viewers, docs, UI.

**The gap.** Actions are still rendered the old way. `App.Modules.Describe()` returns an ad-hoc `Actions.@this` record list that `summary.md` walks via ~30 lines of Fluid to format headers, `/ description` lines, parameters with optional/default decorations, examples. Any other consumer (trace viewer, future docs page) has to re-parse that Markdown to understand what actions exist.

**Proposal.** Add `ActionEntry` / `ParameterEntry` / `ModifierEntry` records in `App.Catalog`, mirroring the type side. `Catalog.@this` grows `Actions: IReadOnlyList<ActionEntry>` and `Modifiers: IReadOnlyList<ActionEntry>`. `summary.md` becomes a thin stitcher: iterate the structured entries and emit the same Markdown it emits today, but the formatting logic lives in C# (a single renderer with unit tests) rather than in a Liquid file that nobody wants to touch.

**Why this matters.**
- `Catalog.ToJson()` finally describes the *whole* builder prompt — every module, action, modifier, parameter, type, enum. A trace viewer can render it as a tree the user can explore.
- Liquid template shrinks to something like `{% for module in catalog.Modules %} ## {{ module.Name }} ... {% endfor %}` — mostly structure, no formatting tricks.
- Tests can assert on `ActionEntry.Parameters[0].Name == "Path"` instead of grepping Markdown.
- Same discovery path for third-party modules once we support them — they publish `ActionEntry` instances, catalog picks them up.

**Scope.** One new file (`App/Catalog/ActionEntry.cs`), refactor `Modules.Describe()` to return or populate these, replace `summary.md` body with structured iteration, update any consumers. Don't change the rendered Markdown shape — the LLM has been trained (by us) on the current shape, and this is infrastructure not wire contract.

**Risk.** Small. The renderer has to produce byte-identical output to the current Liquid template (or LLMs rebuild differently and every `.pr` changes). Snapshot test against a representative catalog before/after cutover.

## Build-time variable type registry for cross-step validation

**Date:** 2026-05-03

During `builder.validate` (per-step), walk the `Parameters` list<Data> and record every variable reference with its declared type into a build-time store keyed by variable name. On subsequent steps the validator (and the next LLM build pass) can look up known variables and:

1. **Validate type consistency** — if step 3 reads `%foo%` typed as `int` and step 7 writes `%foo%` as a `string`, the validator flags the mismatch instead of letting it manifest as a runtime TypeMismatch.
2. **Inform the LLM** — pass the known-variables map to the system prompt so the LLM emits step mappings with correct type tags for vars already in scope, instead of guessing.

Belongs in the validator (not `variable.set`) so we capture the type as declared *at the parameter slot*, not just the writer's intent.

**Context:** came up while diagnosing `Actor: ""` LLM hallucinations — the .pr type slot says `actor` but the value is empty/wrong-typed, and we only catch it at conversion time. A type registry would surface "variable %x% used as actor here, but it was set as string in step 2" much earlier.

## Go over events — `App.Events` vs `Context.Events`

**Date:** 2026-05-07

Two `AppEvents` registries exist (one on `App`, one per-actor on
`Context`). Suspect smell — same concept twice. Worth a pass to decide
which scope owns what, and whether they should be one thing.

## Callback PLang surfaces — durability, timeout, signature tamper, AskVars

**Date:** 2026-05-07

Four `.test.goal` stubs were removed from `Tests/Callback/` because they
documented missing PLang surface, not real test work:

- **`callback.configure` (or similar)** — verb-level surface for writing
  `app.Callback.Signature.ExpiresInMs`. C# already covers the contract
  via `AppCallbackConfigTests`.
- **Byte-level callback persistence across processes** — `DurabilityRoundTrip`
  needs a PLang surface to drive the round-trip. C# coverage in
  `PLang.Tests/App/CallbackTests/ErrorCallbackTests.cs`.
- **Byte-level mutation of a serialized callback envelope** —
  `TamperedSignature` needs the same kind of surface to corrupt and
  re-verify. C# coverage in `CallbackRun_HardErrors_WhenSigningVerifyFails`.
- **`vars:` annotation builder validation** — `AskVarsOnNonAsk` blocked
  on builder pass that flags `vars:` on non-`ask` actions.

All four are nice-to-have PLang-side complements; the behaviour is
already proven in C#. Worth a pass when callback work resumes.

## Static mutable state in C# — sweep for multi-App safety

**Date:** 2026-05-07

When PLang gains the ability to host multiple `App` instances in one
process (`%app%` + `%app2%`, etc.), any `private static` mutable field
becomes a cross-App leak. Found one concrete hotspot during the v10
Console.* purge audit:

- **`PLang/App/modules/builder/providers/DefaultBuilderProvider.cs:18`**
  — `private static readonly Stopwatch _buildTimer = new();` is
  process-global. Two concurrent builds (`%app%` and `%app2%` both
  building) share one Stopwatch; `Restart()` from one corrupts the
  elapsed measurement the other reads. Move to instance field on the
  provider.

The rest of the area is fine (`VarRefPattern` Regex, `_debugJsonOptions`
JsonSerializerOptions, the `private static` lifecycle handlers in
`App/Debug/this.cs` — those are stateless dispatchers that read state
from `context.App.Debug`, not from static fields). But the *rule* to
enforce: no `static` mutable state in any code path that an `App`
instance touches. Worth a sweep when multi-App lands.
