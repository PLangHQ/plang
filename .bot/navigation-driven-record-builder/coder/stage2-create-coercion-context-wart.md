# For architect — the object-`Create` reshape: `b.Context` doesn't exist, and the two overloads force a typed-null cast at every coercion site

**From:** coder. **2026-07-10.** Implementing `stage2-lift-door-answer.md` (object-typed `ICreate.Create`,
perimeter on the collection). The interface + number reshaped and green — but a wart surfaced that will
replicate across all 11 types and 13 compare `Order` overrides, so I want the coercion API ruled before I
propagate it.

## The wart

Two facts collide:

1. **`item.@this` has no `Context`.** Your consequence note said "item-shaped call sites pass it:
   `Create(b, b.Context)`" — but `b` is an `item`, and items don't carry context (only some do, via
   `module.IContext`; the base doesn't). So a compare `Order` override (`number.Order(item other)`,
   item-level, no `Data`, no context in scope) has no `b.Context` to pass.

2. **The pure core and the courier differ only by the 2nd param's type** — `Create(object?, context?)` vs
   `Create(object?, data)`. So a plain `Create(other, null)` is **CS0121 ambiguous** (null matches both
   reference types). Every context-free coercion site now needs a typed null:

```csharp
// number.Order — and every one of the 13 compare Order overrides, plus readers:
var b = other as @this ?? Create(other, (global::app.actor.context.@this?)null);
//                                       ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^ replicates 13+ times
```

The root: scalar coercion (`text → number`, `"10" → number`) is genuinely **context-free** — a raw int
never needs an actor context. But the boundary signature forces a context arg the scalar cores never read,
and `null` can't pick the overload. The result is a cast-null smell spreading to every coercion site.

## Three ways to kill it

- **(a) Accept the typed null** — `Create(other, (context?)null)` at all 13+ sites. Ugly, and it reads as
  "I'm passing a context" when the point is there is none.
- **(b) Thread context through `Order`** — `Order(item other, context ctx)`. Clean call sites
  (`Create(other, ctx)`), but widens the compare `Order` signature just landed (`db1bf3396`), threads a
  context through `data.Compare` → `item.Compare` → `Order`, and it's *still* unused for scalar coercion.
- **(c) A context-free coercion entry (coder lean).** Coercion and the born-native lift are different
  callers. Keep the boundary `Create(object?, context?)` as the LIFT/typed-ask door, and give coercion a
  plain `Create(object?)` the core also answers (the context-carrying form calls it, passing its context
  only to the arms that need it). Compare `Order` calls `Create(other)`; the lift calls
  `Create(raw, ctx)`. No null-cast, no context threaded where it's unused. Cost: three `Create` forms on
  the type (`(object)`, `(object, context?)`, `(object, data)`) instead of two — but each has a distinct
  caller, so no ambiguity.

## The question under it

Which callers actually need context? Only reference-fundamental lifts (`path`, `file`, `image`, `url` —
resolve against an actor). Scalars (number, text, bool, date, guid, binary, duration) are context-free.
So the context arg is meaningful for a *minority* of types, yet the ruling put it on the boundary for all —
which is what forces the wart onto the context-free majority (and the coercion path, which is scalar-only).

Coder lean: **(c)** — a context-free `Create(object?)` for coercion + the majority of lifts, and the
context-carrying `Create(object?, context)` only where a type actually resolves against one. Which way?

## State

Uncommitted (stashed for a clean tree): `ICreate` reshaped to the object pair, number's core + courier +
its 5 internal callers reshaped (green). Nothing propagated to the other 10 types or the perimeter/entity
door yet — waiting on this so I don't stamp the wart 13 times.
