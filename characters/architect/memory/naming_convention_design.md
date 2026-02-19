# Law of Names — Naming Convention as Architecture

## Status: Design phase (2026-02-17)

## Core Rule
Class name = `{Owner}{Capability}`. Source generator reads the prefix, generates a property on the owner.

- `EngineGoals` → owner: Engine, property: Goals
- `GoalSteps` → owner: Goal, property: Steps
- `StepActions` → owner: Step, property: Actions

Known owners: `Engine`, `Goal`, `Step`, `Action`

## File Naming: `this.cs`
The folder IS the identity. The main class file is always `this.cs` (like index.js). Other files in the folder are what that node owns.
```
Engine/Goals/this.cs      → EngineGoals class
Engine/Goals/Goal.cs      → Goal entity (owned by Goals)
Engine/Goals/Steps/this.cs → GoalSteps class
```

## Namespace = Dot-path
- `EngineGoals` lives in `PLang.Runtime2.Engine.Goals`
- `GoalSteps` lives in `PLang.Runtime2.Engine.Goals.Steps`
- `StepActions` lives in `PLang.Runtime2.Engine.Goals.Steps.Actions`

## Two-Layer Dispatch
1. **Convention-wired** (compiled, source-generated): `engine.Goals` → EngineGoals instance
2. **Key-value store fallback**: `engine.MyProperty` → `engine.Property["MyProperty"]`

Same dot-path syntax for both. User doesn't know which layer resolves. Property supports GoalCall values, so `engine.Summary` can trigger goal execution transparently.

## Why OBP Makes This Possible
- Behavior belongs to the owner → each node is self-contained and addressable
- Navigate, don't pass → dot-path navigation IS OBP navigation as syntax
- Keep object references → the object IS the value, methods work on the live object
- Smart collections → nodes own their domain operations (the things you dispatch TO)

OBP makes the graph semantically meaningful. The convention makes it mechanically navigable. The generator wires them. Closed system.

## Key Principles (confirmed with Ingi)
1. Naming convention is law — follow it, you're in the community
2. The object IS the value — no default Run method. `engine.Goals` returns the EngineGoals instance.
3. The object lives where it's created — Data/Type at root, Goal inside Goals, all the way down
4. Source generator wires everything — ConventionWiringGenerator creates properties on owners
5. Libraries — PARKED. Convention may subsume the module system.

## Resolved Decisions
1. **Serialization** — .pr files adapt to new names. No backward compatibility.
2. **Modules** — Everything follows the naming pattern. Everything source generated. `variable.set` becomes a convention-wired class.
3. **External libraries** — Mount under `engine.Libraries["astro"]`. Never touch engine root properties. Only built-in code gets convention-wired properties.
4. **Lazy initialization** — Everything in PLang and OBP is lazy. Generated properties use lazy init.
5. **Error handling** — MethodRun returns error on unknown path. Follows KPR: every action returns full observable context (result, error) so caller can self-correct.

## Root Types
Data and Type live in the Engine namespace directly (no prefix, no sub-namespace) because they're value types used everywhere. `engine.Data == Data`.

## Entities
Goal, Step, Action live in their parent capability's namespace:
- Goal in `Engine.Goals` (created by EngineGoals)
- Step in `Engine.Goals.Steps` (created by GoalSteps)
- Action in `Engine.Goals.Steps.Actions` (created by StepActions)

## Per-request State
PLangContext, Actor, MemoryStack live in `Engine.Context` but don't follow the `{Owner}*` prefix — they're parameters, not engine structure.

## Source Generator
ConventionWiringGenerator:
1. Scans classes matching `{KnownOwner}{Suffix}` pattern
2. Generates partial class extension on owner with property
3. Generates dispatch table for runtime navigation

## Full design doc
See `output/v1/result.md` for complete mapping, code examples, and open questions.
