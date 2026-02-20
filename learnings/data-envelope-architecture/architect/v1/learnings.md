# Learnings — Data Envelope Architecture Session

## OBP API Design

### 1. No verbs or prepositions in APIs

`Get`, `Resolve`, `From`, `Is` — these are procedural patterns. They describe *operations on* an object, not *behavior of* an object.

- Bad: `TypeMapping.GetCategory(extension)`, `Type.FromExtension(".xlsx")`, `ResolveKind()`, `IsCompressible(category)`
- Good: `Context.Types.Kind(value)`, `data.Type.Kind`, `data.Type.Compressible`

The object navigates to the knowledge it needs. The caller never tells it *how* to figure things out.

**Why it matters:** Verbs create coupling — the caller must know what operation to request and what service provides it. Navigation lets the object find its own answer through the graph.

### 2. Objects navigate, they don't look things up

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

### 3. Lazy navigation over eager resolution

Properties like `.Kind` resolve on first access by navigating through context. Not through factory methods, not through constructors, not through static lookups. Zero cost until someone asks.

This is the PLang laziness principle applied to OBP navigation — nothing loads until it's needed. 99% of Type instances in the runtime never need their Kind, so it never resolves.

### 4. Static classes break OBP

A static class (`public static class TypeMapping`) has no identity, no state ownership, no lifecycle. It can't be extended at runtime. It's a bag of functions pretending to be an object.

Instance on engine = live object, extensible (Add/Remove at runtime), owns its knowledge, participates in the object graph. `Engine.Types` is something. `TypeMapping` is a utility.

### 5. Object names are nouns, not service descriptions

- `TypeMapping` — describes what it does (maps types). Service name.
- `Types` — what it *is*. The engine's type knowledge. Object name.

One word. Noun. Same pattern as `Engine.Goals`, `Engine.Actions`.

### 6. Factory methods are outside-in

`Type.FromExtension(".xlsx")` tells the object what it is from the outside — the factory knows the construction logic, the object is passive. In OBP, the object is constructed simply (`new Type("string")`) and discovers what it needs lazily through navigation when asked.

Construction is simple. Knowledge is navigated.

---

## Design Process

### 7. Context solves most dependency problems

When an object needs something, the answer is almost always "it has context, context has engine, engine has the graph." Don't invent injection mechanisms or service locators. Just navigate.

Type needs kind resolution? It has context → `Context.Types.Kind(Value)`.
Data needs to compress? It has context → navigates to compress action.
Encryption needs keys? It has context → navigates to identity service.

The cost of a context reference is a pointer. The benefit is access to everything.

### 8. Late-bound beats never-bound

When an ideal OBP solution (context at construction) would require changing hundreds of call sites (like `Data.Ok()` static factories), a pragmatic late-bound approach works: create the object simply, stamp context when the pipeline has it. Not pure, but the static factories survive and the envelope methods still work because they only run at IO boundaries where context is guaranteed.

Pragmatic > pure when the alternative is a rewrite of every action handler.

### 9. One object, many concerns — use partial classes

Data serves four roles: variable wrapper, result type, parameter carrier, transport envelope. That's a lot of surface on one class. But splitting into separate types would force the PLang developer to know about multiple types.

Partial classes solve this: `Data.cs` (core), `Data.Result.cs`, `Data.Navigation.cs`, `Data.Envelope.cs`. One type to the consumer, four focused files for the developer. Envelope surface is zero-cost until IO boundaries activate it.

### 10. Naming is design

`Kind` not `Category` — shorter, less formal, fits the PLang voice.
`Types` not `TypeMapping` — what it *is*, not what it *does*.
`Out` not `Channel` or `Transport` or `Wire` — one word, exactly what's happening: data going out.

Ingi consistently pushes for shorter, more direct names. If you need two words, you probably haven't found the right one word yet.

---

## Workflow

### 11. Architect = think, not code

The CLAUDE.md is explicit: "Your job is not to write code. Your job is to think." This means:
- Don't enter plan mode unprompted
- Don't propose implementation steps
- Don't draft code changes
- DO show example snippets to illustrate design points
- DO walk through the design as a conversation
- DO write the spec when asked to hand off to the coder

The default mode is whiteboard conversation. Ingi throws seeds (often half-formed, mixing Icelandic and English), I think them through, he corrects. The corrections themselves are the signal — that's where the real design decisions land.

### 12. Let Ingi steer

Don't dump a full design. Walk through it piece by piece. When I proposed the TypeMapping rewrite with six methods, Ingi said "those are verbs, that's not OBP" — and that correction led to the much cleaner `Type.Kind` navigation pattern. If I'd written the implementation plan first, that insight would have been buried in a review comment instead of shaping the design.

The process is: present understanding → get corrected → the correction is the design.
