# The Object-Based Pattern

## 1. Introduction

The Object-Based Pattern is an architectural pattern for organizing behavior in object-oriented systems. It places every operation on the object that owns the data the operation acts upon. Instead of external managers, services, or controllers that reach into objects to manipulate their state, objects expose behavior as methods and expose their sub-components as navigable properties.

The result is a system that reads like natural language: `order.Items.CalculateTotal()` — "the order's items calculate their total."

This document defines the pattern formally so that a developer encountering it for the first time can understand and apply it consistently. It is language-agnostic — the principles apply to any object-oriented system.

## 2. Definitions

**Owner**: The object that holds the data. If an `Order` contains a list of items, the `Order` owns that list.

**Behavior**: Any operation that reads, transforms, or produces data. Loading, running, merging, serializing.

**Navigation**: Reaching a sub-component through a property chain. `engine.Projects.Get("alpha")` navigates from the engine to its project collection, then queries it.

**Root**: The top-level object from which all capabilities are reachable. In a runtime system, this is typically the engine or application object.

**Context**: A request-scoped object carrying execution state (memory, call stack, current position). It is passed as a parameter and never stored on shared objects.

**Collection Wrapper**: A typed class that extends a generic collection (e.g., `Stages : List<Stage>`) and adds domain-specific operations.

## 3. Core Principles

### 3.1 Behavior Lives on the Owner

Every operation belongs to the object whose data it operates on. If an operation iterates a collection, it belongs on the collection. If it transforms a result, it belongs on the result type.

**Test**: Ask "whose data does this method read or write?" The answer is the owner. If the method is not on the owner, move it.

```
Stages.Load(context)             -- Stages owns the list, so Load belongs here
Operations.RunAsync(engine, context)  -- Operations owns the list, so RunAsync belongs here
Result.Merge(other)              -- Result owns the data, so Merge belongs here
```

A parent delegates to its children. It does not iterate them:

```
Task.Load(context):
    Task.Stages.Load(context)       -- Task delegates to Stages; it does not loop over stages itself

Stage.RunAsync(engine, context):
    Stage.Operations.RunAsync(engine, context)  -- Stage delegates to Operations
```

### 3.2 Navigate, Don't Inject

All capabilities are reachable by navigating from a root object through properties. A handler that needs file I/O accesses `engine.FileSystem`. A handler that needs to resolve a project accesses `engine.Projects`. There is no need to pass individual dependencies as parameters — pass the root, navigate from there.

```
engine
  .Projects       -- find/load projects
  .Handlers        -- find operation handlers
  .Channels        -- named channel routing
  .FileSystem      -- file operations
  .Serializers     -- serialization
```

**Test**: If a method signature has more than two domain parameters (excluding context and cancellation tokens), something should be reachable through navigation instead.

### 3.3 Names Describe What the Object Is

Every property name should tell you what the object *is*, not what it *does*. When you look at the object graph, the name alone should tell you where to navigate.

**Good names** describe the thing:
```
engine.Goals          -- "Goals" — manages goals
engine.Libraries      -- "Libraries" — manages action handler libraries
engine.Channels       -- "Channels" — manages named I/O channels
engine.FileSystem     -- "FileSystem" — manages file access
engine.Serializers    -- "Serializers" — manages serialization
```

**Bad names** describe a verb or are too broad:
```
engine.IO             -- "IO" — input/output of what? Files? Channels? Everything?
engine.Run            -- "Run" — is this a method? A runner object?
engine.Data           -- "Data" — what data?
```

**Test**: If the name could reasonably describe two different things in the system, it is too broad. `IO` could mean file I/O or channel I/O — rename to `Channels` or `FileSystem` depending on what it actually is.

**Consequence**: When a name is accurate, responsibilities don't bleed. Nobody asks "should filesystem operations go through Channels?" because the name makes the boundary obvious. But when a name is vague like `IO`, the boundary is unclear and responsibilities drift.

#### Structures Are Things

A class that exists as a compositional structure — with properties that each take care of themselves — IS a thing. Name it after what it is.

A `Lifecycle` with `.Before` and `.After` IS a lifecycle. A `Bindings` with `.Add()` and `.Run()` IS a collection of bindings. These are not managers, dispatchers, or handlers — they are the things themselves.

```
// Correct: the structure IS the thing
goal.Lifecycle              -- the goal's lifecycle
goal.Lifecycle.Before       -- the before bindings
goal.Lifecycle.Before.Run() -- run them

// Wrong: naming the structure after what it does
goal.EventManager           -- what events? How does it manage them?
goal.EventDispatcher        -- "dispatch" is a verb, not a thing
```

Do not rename structures to "Manager", "Dispatcher", "Handler", or "Service". These suffixes describe behavior, not identity. The structure already does what it does — each property takes care of itself.

#### Properties Are Nouns, Methods Are Verbs

A property describes what something IS. It is a structure sitting there. If something needs to happen to it, that is a method on it.

Never put a verb in a property name.

```
// Wrong: "Load" is a verb — this property is not loading anything
lifecycle.Load.Before

// Correct: it is just "Before" — a structure with bindings
lifecycle.Before            -- the before bindings
lifecycle.Before.Run()      -- need to run them? Call a method
```

**Test**: Read the property name. If it sounds like an instruction — "load", "run", "create", "process" — it is a verb and belongs as a method. Properties answer "what is this?", methods answer "what should happen?"

### 3.4 Properties Read as Natural Language

Property chains should form a readable phrase. The pattern is **Subject.Component.Operation** or **Subject.Component.Sub-component**.

```
engine.Goals.Get("alpha")                -- "the engine's goals — get alpha"
stage.Operations.Load(context)           -- "the stage's operations — load them"
goal.Lifecycle.Before.Run(context)       -- "the goal's lifecycle, before — run the bindings"
```

If a chain does not read naturally, the structure is wrong. Likely a component is on the wrong object or named poorly.

### 3.5 Distinguish Object State from Request State

Some data belongs to the object (its structure, its configuration, its event bindings). Other data belongs to the current request (which user is executing, what the current position is, the memory stack). These two categories must not be mixed.

- **Object state** is stored as properties on the object. It is set once (typically at load time) and shared across requests.
- **Request state** is carried in a context object and passed as a method parameter. It is per-request and per-thread.

**Violation**: Storing a request-scoped context reference on a shared object.

```
// Wrong: Task is shared across threads, _context is per-request
class Task {
    Context _context
}
```

**Correct**: Pass context as a parameter to every method that needs it.

```
task.RunAsync(engine, context)
stage.RunAsync(engine, context)
events.Before.Run(context)
```

**Rule**: If multiple threads can access the same object instance, that object must not hold any request-scoped state. All request-scoped data flows through parameters.

### 3.6 Preserve Object Identity

When referencing an object from another object, store the object itself — not a decomposed copy of its fields. If a `StageError` needs to know which stage caused it, it holds a reference to the `Stage`, not a copy of `Stage.Text`.

**Rationale**: Decomposing objects into primitive fields discards information. The consumer can always access `.Text` on the stage, but cannot reconstruct the stage from just a text string. Keeping the reference preserves the full object graph and avoids redundant data.

```
// Wrong: decomposing Stage into a string
class StageError {
    text: string
}

// Correct: keeping the Stage reference
class StageError {
    stage: Stage
}
```

### 3.7 Results Own Their Composition

When multiple operations produce results that must be combined, the merge logic belongs on the result type. The result knows its own internal structure (what fields exist, how duplicates are resolved), so it is the correct owner of the merge operation.

```
class Result {
    merge(other: Result) -> Result    -- knows how to combine two results
}
```

The collection that produces multiple results calls `merge` in a loop. It does not inspect or restructure the result — it delegates composition to the result itself.

### 3.8 No Redundant Wrapper Objects

If the information a callee needs already exists on an object the caller has, pass that object. Do not construct a new object that copies fields from the existing one into a different shape.

**Violation**: Creating a `NotificationContext` with `userName`, `requestId`, `timestamp` when those are already on `RequestContext` as `User.Name`, `Id`, `CreatedAt`.

**Correct**: The handler receives `RequestContext` directly. It accesses what it needs through the existing properties.

**Test**: Before creating a new class to pass data, check if every field on the new class already exists (or is trivially derivable) from an object the caller already holds. If yes, pass the existing object.

## 4. Collection Wrappers

A typed collection wrapper extends a generic collection and adds domain operations:

```
class Stages : List<Stage> {
    load(context)
}

class Operations : List<Operation> {
    load(context)
    runAsync(engine, context, ct) -> Result
}
```

The wrapper:
- Inherits standard list operations (indexing, iteration, Count)
- Adds domain-specific batch operations (load, runAsync)
- Exposes a `value` property returning the underlying list for uniform access

Parents delegate to the wrapper. They do not iterate the collection themselves.

**When to use**: When you have an ordered collection of domain objects and need batch operations on them.

**When not to use**: When the collection needs to intercept individual add/remove (use composition instead of inheritance in that case).

## 5. The Root Object

The root object serves as the single access point for all system capabilities. Any object in the system that receives the root can reach any other capability through navigation.

The root holds:
- **Domain collections** (Projects, Handlers) — registered behavior and data
- **Infrastructure** (IO, FileSystem, Serializers) — system capabilities
- **Actors** (System, User, Service) — identity and trust boundaries

The root does not hold business logic. It is a composition point. Actual behavior lives on the leaf objects (Task.RunAsync, Stage.Load, Operations.RunAsync).

## 6. Decision Procedure

When adding a new operation, follow this sequence:

1. **Identify the data**: What data does this operation read or modify?
2. **Identify the owner**: Which object holds that data?
3. **Place the method on the owner**.
4. **Check readability**: Does `owner.Method()` read naturally? If not, the owner may be wrong, or an intermediate collection wrapper is needed.
5. **Check parameters**: Does the method need request-scoped state? Pass context. Does it need system capabilities? Pass the root (or navigate from root).
6. **Check for redundancy**: Are you creating a new class to pass data? Check if the data already exists on an object the caller holds.

## 7. Summary of Rules

| # | Rule | Test |
|---|------|------|
| 1 | Behavior lives on the object that owns the data | "Whose data does this method touch?" |
| 2 | Navigate through properties; don't pass individual dependencies | "Can I reach this from the root?" |
| 3 | Names describe what the object is, not what it does | "Could this name mean two different things?" |
| 3a | Structures are things — name them after what they are | "Am I naming the structure or the behavior?" |
| 3b | Properties are nouns, methods are verbs | "Does this property name sound like an action?" |
| 4 | Property chains read as natural language | "Does this read like a sentence?" |
| 5 | Request state is passed as parameters, never stored on shared objects | "Can two threads see this field?" |
| 6 | Store object references, not decomposed fields | "Am I discarding information?" |
| 7 | Results own their composition logic | "Who knows how to merge these?" |
| 8 | Don't create wrapper objects for data that already exists elsewhere | "Is every field already on an existing object?" |
| 9 | Collections own batch operations on their items | "Who owns the loop?" |
| 10 | The root is a composition point, not a behavior owner | "Does the root do work, or does it delegate?" |
| 11 | Methods do one thing | "Can I describe this method in one clause without 'and'?" |
