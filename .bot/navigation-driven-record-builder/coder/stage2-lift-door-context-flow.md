# For architect — the navigated lift's door shape: how does context reach it? (a static/instance `Create` collision + a shared-entity constraint)

**From:** coder. **2026-07-10.** Implementing the navigated raw-CLR lift (`stage2-raw-clr-lift-answer.md`).
The shape is right and mostly lands, but wiring the entity door hit a C# collision that surfaced a
fundamental question about **where the born-with context lives** — Ingi wants it ruled here, not guessed.

## The ruling's shape (what I'm building)

```csharp
// static perimeter — the lift entry every value-birth calls:
type.Create(raw)  →  ctx.App.Type[raw.GetType()]?.Create(raw, data) ?? new Clr(raw, ctx)
// entity door — per-type, cached delegate (_create), reflects Create<T>() once:
public item.@this? Create(object? raw, data.@this data) => (_create ??= …)(raw, data);
static Func<…> Create<T>() => (raw, d) => T.Create(raw as item ?? new Clr(raw, d.Context));
```

## The collision

The **static perimeter** and the **entity door** are both spelled `Create` and both naturally want
`(object? raw, <carrier>)`. Today the static is `Create(object? raw, actor.context context = null)` — every
value-birth site calls it with a **context** (`data/this.cs` ctor, `list.Add`, `dict.Set`, the readers,
`computed`, `variable.set` — 13 of 15 callers build a *standalone* value with only a context in hand). If
the entity door is also `Create(object? raw, context)`, that's **CS0111** — a type can't declare a static
and an instance method with the same signature. So the two MUST differ.

The ruling's door takes `data` (differs from the static's `context`) — which dodges CS0111. But then the
static perimeter, navigating `app.Type[clr].Create(raw, data)`, needs a **`data`** — and 13 of its 15
callers have only a **context** (they build the value *before* wrapping it in a Data; chicken/egg).

## The deeper finding (Ingi's probe: "does `app.Type[clr]` carry context?")

I checked whether the entity could just use `this.Context` (making the door `Create(object? raw)` — no
carrier param, no collision). **It can't:** `catalog[name]` returns a **shared, cached** entity
(`CatalogByName[name]`, the same instance every call, no `Context`). The context that matters is the
**per-value actor context** (born-with, differs by actor/callstack). Stamping one caller's context onto the
shared registry entity is a **late-stamp that leaks** — the next value, born under a different actor,
inherits the wrong context. (Contrast `data.Type`, which mints a *fresh* entity and stamps that — safe
because per-Data, not shared.)

So **context must ride the call, not the entity.** The shared registry node stays context-free by design;
the born-with context flows through the lift call. That's correct — and it's exactly what forces a carrier
param on the door and the collision with the static.

## The fork (needs your ruling)

1. **Door takes `data`** (the ruling's literal). Clean door; but the 13 context-only perimeter callers have
   no `data`. Options to feed it: thread a `data` through all 15 (restructure the standalone sites to build
   the Data first), or mint a throwaway `Data` from context at the static (smelly, and the Data ctor does
   real work). Per your own (a)-style reasoning the door uses only `data.Context` (no Fail — the natural
   lift never fails, unowned → `Clr`), so `data` is *just a context carrier* here, which argues it
   shouldn't be `data` at all.

2. **Door takes `context`; the static becomes a thin static thunk-cache** (no instance `Create` door). The
   perimeter `Create(object? raw, context)` navigates `app.Type[clr].ClrType`, reflects `Create<T>()` once
   per ClrType into a `ConcurrentDictionary<Type, Func<object?, context, item?>>`, invokes it. `_create`
   lives as that **static cache** ("a delegate cache in type"), not a per-entity field. No collision, no
   `data` threading, context rides the call. **Coder lean** — the lift needs only context, and this keeps
   the shared entity context-free.

3. **Fresh context-stamped entity** — `app.Type[clr]` mints a per-call entity stamped with the caller's
   context (like `data.Type`), door is `Create(object? raw)` using `this.Context`. Cleanest door signature,
   but a per-lift allocation and a mint-then-stamp (late-stamp unless the mint takes context at birth).

## The question under the question

You specced the entity door as `app.Type[clrType].Create(raw, data)` — an **instance** door with `data`.
Given (a) (the natural lift takes only context, never Fails), is `data` the right carrier, or is it really
`context`? And given the shared-cached entity can't hold a per-value context, is the **per-entity `_create`
delegate** even the right home — or is it a **static per-ClrType cache** (option 2)? The instinct "`_create`
is a delegate in type" holds either way; the question is instance-field vs static-cache, and it turns on
whether the door carries `data`, `context`, or nothing.

Coder lean: **(2)** — door/static thunk-cache on `context`, shared entity stays a context-free registry
node. But this is the fundamental one, so it's yours.

## Status

Landed so far (compiles, minus the collision): the pure `Create(item)` lifted onto `ICreate` (§ (a)),
`ICreate.Create`'s `OfStatic` call removed, and the static perimeter's scalar arm navigating
`app.Type[clr]` instead of `OwnerOf`/`OfStatic`. The door + thunk are the only piece blocked on this ruling.
Nothing committed yet.
