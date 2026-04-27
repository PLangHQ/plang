# Code Analysis Plan — Action Modifiers (v1)

## Scope

Analyze all production C# code changed on `runtime2-action-modifiers` vs `runtime2`. This is a significant feature branch: modifier infrastructure (IModifier, ModifierAttribute, smart collections), three modifier handlers (timeout.after, cache.wrap, error.handle), builder pipeline changes (GroupModifiers, promoteGroups), source generator extensions (IStatic, Data<T> wrapping), and legacy cleanup.

## Key files to analyze (production code only — not tests)

### New modifier infrastructure
- `PLang/App/modules/IModifier.cs`
- `PLang/App/modules/ModifierAttribute.cs`
- `PLang/App/modules/IDataWrappable.cs`
- `PLang/App/modules/IStatic.cs`
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/Modifiers/this.cs`

### Modifier handlers
- `PLang/App/modules/timeout/after.cs`
- `PLang/App/modules/cache/wrap.cs`
- `PLang/App/modules/error/handle.cs`
- `PLang/App/modules/error/throw.cs`

### Modified core infrastructure
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — RunAsync, WrapAround, Modifiers property
- `PLang/App/Goals/Goal/Steps/Step/Actions/this.cs` — GroupModifiers
- `PLang/App/Goals/Goal/Steps/Step/this.cs` — simplified RunAsync, Clone
- `PLang/App/Modules/this.cs` — IsModifier, GetModifierOrder, Describe
- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` — GoalsSave, Validate, PromoteGroups

### Source generator
- `PLang.Generators/LazyParamsGenerator.cs` — IStatic, Data<T> wrapping, __ResolveData

### Data model changes
- `PLang/App/Data/this.cs` — IsVariable, HasVariableReference, Clone/ShallowClone
- `PLang/App/Actor/Context/this.cs` — GetModuleStatic, GetOrCreate wrapper cache
- `PLang/App/Errors/Error.cs` — verbose debug, context capture

## 5-pass analysis

1. **OBP Compliance** — Check every file against the 5 OBP rules
2. **Simplification** — Dead abstractions, over-parameterization, nested conditionals
3. **Readability** — Naming, method length, flow clarity
4. **Behavioral Reasoning** — Trace data origins, clone family, generic catches, type boxing
5. **Deletion Test** — "If I deleted this, would any test fail?"

## Delivery

- `v1/result.md` — Full per-file analysis
- `v1/verdict.json` — Pass/fail
- `v1/summary.md` — What was found
