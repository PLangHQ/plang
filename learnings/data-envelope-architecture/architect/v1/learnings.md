# OBP Learnings — Data Envelope Architecture Session

## 1. No verbs or prepositions in OBP APIs

`Get`, `Resolve`, `From`, `Is` — these are procedural patterns. They describe *operations on* an object, not *behavior of* an object.

- Bad: `TypeMapping.GetCategory(extension)`, `Type.FromExtension(".xlsx")`, `ResolveKind()`, `IsCompressible(category)`
- Good: `Context.Types.Kind(value)`, `data.Type.Kind`, `data.Type.Compressible`

The object navigates to the knowledge it needs. The caller never tells it *how* to figure things out.

**Why it matters:** Verbs create coupling — the caller must know what operation to request and what service provides it. Navigation lets the object find its own answer through the graph.

## 2. Objects navigate, they don't look things up

Instead of a lookup service (`TypeMapping.GetCategory(extension)` — handing data to a service, getting an answer back), the object navigates through its context to whoever owns the knowledge.

```csharp
// Procedural: caller orchestrates the lookup
var category = TypeMapping.GetCategory(data.Type.Value);
var compressible = TypeMapping.IsCompressible(category);

// OBP: Type navigates through context
data.Type.Kind          // Type asks Context.Types
data.Type.Compressible  // derived from Kind, still through navigation
```

The caller says *what* it wants (Kind), the object figures out *where* to get it (Context → Engine.Types).

## 3. Lazy navigation over eager resolution

Properties like `.Kind` resolve on first access by navigating through context. Not through factory methods, not through constructors, not through static lookups. Zero cost until someone asks.

This is the PLang laziness principle applied to OBP navigation — nothing loads until it's needed. 99% of Type instances in the runtime never need their Kind, so it never resolves.

## 4. Static classes break OBP

A static class (`public static class TypeMapping`) has no identity, no state ownership, no lifecycle. It can't be extended at runtime. It's a bag of functions pretending to be an object.

Instance on engine = live object, extensible (Add/Remove at runtime), owns its knowledge, participates in the object graph. `Engine.Types` is something. `TypeMapping` is a utility.

## 5. Object names are nouns, not service descriptions

- `TypeMapping` — describes what it does (maps types). Service name.
- `Types` — what it *is*. The engine's type knowledge. Object name.

One word. Noun. Same pattern as `Engine.Goals`, `Engine.Actions`.

## 6. Factory methods are outside-in

`Type.FromExtension(".xlsx")` tells the object what it is from the outside — the factory knows the construction logic, the object is passive. In OBP, the object is constructed simply (`new Type("string")`) and discovers what it needs lazily through navigation when asked.

Construction is simple. Knowledge is navigated.

## 7. Architect role = think, don't code

When the CLAUDE.md says "Your job is not to write code. Your job is to think" — that means: discuss architecture, show example snippets to illustrate design points, but never draft implementation plans or enter plan mode unprompted. The default mode is whiteboard conversation, not task execution.
