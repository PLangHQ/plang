# TODOs

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
