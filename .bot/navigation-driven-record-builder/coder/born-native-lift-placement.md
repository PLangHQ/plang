# Finding: the born-native lift (`Create`) lives on the wrong owner

From coder, for architect. Surfaced with Ingi 2026-07-14 while draining the PLNG004 worklist
(the STJ-collapse follow-up). Not urgent — a placement/OBP smell on a core dispatcher. Sending it
up to scope before touching it, because it sits in a web of three `Create` doors and has ~35 callers.

## The smell

`app.type.list.@this.Create(object? raw, actor.context.@this? context)` — the **born-native lift**
(a raw CLR value / already-native item → its plang `item.@this`) — lives on the type-**collection
registry** (`app/type/list/this.cs`). But it is a **factory that produces an `item.@this`**. A factory
for `item` living on the type-collection registry is the "factory on the wrong owner" smell: the type
that IS produced should own its construction.

Its own doc already states the principle it then doesn't follow at the top level:

> "A raw CLR value or an already-native item becomes its plang value: the type OWNING the raw's CLR
> shape builds it through its own entity `Create` door; a CLR type no type owns rides a `Clr` carrier.
> Navigated, not switched."

So the *inner* dispatch is honest (it navigates the CLR-ownership index and calls each type's own
door). It's the *home of the dispatcher itself* that's off — it's the perimeter factory for `item`,
and it lives on the registry rather than on `item.@this`.

## What it does (so the smell is concrete)

It's the **.NET perimeter** — the single place raw CLR crosses into the plang type system:
`int → number`, `"x" → text`, `Dictionary → dict`, `List → list`, an unowned CLR type → a `clr`
carrier (rung 2). Rungs: `is item` (pass-through) → container narrowing → clr-ownership-index lift →
`Clr` fallback. NOT `Convert` (value→value), NOT dead — it's the live born-native entry.

## The move (traced)

- **~35 callers**, nearly all through the accessor `context.App.Type.Create(raw, ctx)` (data binding,
  dict/list slot reads, the json kind, the type/object readers, computed, list modules).
- Because they go through one accessor, the move is body-not-call-sites: the lift **body** relocates to
  a static on `item.@this` — e.g. `item.@this.Create(object? raw, context)` — reaching
  `context.App.Type` for the CLR-ownership index it dispatches on; `app.type.list.@this.Create`
  forwards to it (or the accessor is retargeted). Call sites don't churn.

## Why we're flagging, not just doing it

It sits in a **web of three `Create` doors** that should be traced together before moving one:

| Door | Location | Role |
|---|---|---|
| entity | `type.@this.Create(object? raw, context)` (`type/this.cs:246`) | a SPECIFIC type entity builds a value of itself |
| per-type | `ICreate<T>.Create` (each value type) | the type's own three faces (pure core / context / courier) |
| **registry lift** | `type.list.@this.Create(raw, context)` (this finding) | dispatches raw CLR → the owning type's door |

The entity door and the registry lift already call into each other (the entity door's leaf-retype path
re-enters the family courier; the lift calls the owning entity's `Create`). Moving the lift onto
`item.@this` without mapping that back-and-forth risks a tangle. That trace is the real work; the
relocation is mechanical once it's clear.

## Context (independent of placement)

This surfaced because the lift's container-narrowing rung used `JsonSerializer.SerializeToElement(raw)`
to turn a non-generic `IDictionary`/`IList` into a `JsonElement` and re-parse it to native — a PLNG004
hit. That's **already fixed** (build the native `dict`/`list` directly from the entries, matching the
generic-container rung right above it; zero new reds). So the STJ removal is done regardless of where
the method ends up — this finding is purely about the method's **home**.

## Ask

Scope the three-`Create`-door web (one trace), then relocate the lift onto `item.@this` — its own
piece, not riding an STJ commit. Or, if the door web has a reason to keep the lift on the registry
(the ownership index is registry state), say so and we close the finding as "correct as-is."
