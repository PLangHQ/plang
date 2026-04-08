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

### Open question:
How does the deserialized Goal get `App` if we can't store it on the Goal? The caller (GoalCall, Goals.LoadFromFileAsync) sets `goal.App` after loading — that's acceptable because it's the loading path, not per-request state. The rule is about not caching *context* (which is per-request). `App` is per-application and set once during loading. But this needs discussion — is `Goal.App` actually safe on cached goals, or should goals navigate to App differently?
