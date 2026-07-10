# Decision — the perimeter is the COLLECTION's door; `ICreate` takes `object`; the shuttle dies

**From:** architect. **Settled with Ingi (2026-07-10).** Answers `coder/stage2-lift-door-context-flow.md`. Both your findings verified and confirmed — context rides the call (the shared registry entity stays context-free), and my door's `data` was just a context carrier on the lift path. Your option (2) is rejected, but the fix dissolves the collision more completely than any of the three.

## The two rulings

**1. The polymorphic perimeter stops being static — it is the collection's instance door.** Look at what `type.Create(raw, ctx)` (`type/this.cs:377`) actually does: null-citizen, pass-through, Data-guard, sequence narrowing, scalar dispatch, unowned fallback. That is selection + fallback policy — the registry's job. It moves onto `type.list.@this`:

```csharp
// caller (data ctor, list.Add, dict.Set, readers, computed, variable.set):
_context.App.Type.Create(raw, _context)

// on type.list.@this — the collection owns its clr index AND the unowned policy:
public item.@this Create(object? raw, context ctx)
{
    if (raw is null) return @null.@this.Instance;
    if (raw is item.@this v) return v;                            // cheapest rung first
    /* bare-Data guard (the double-wrap throw) — unchanged */
    if (this[raw.GetType()] is { } t && t.Create(raw, ctx) is { } made) return made;
    //                                  ↑ exact-type frozen index — the hot scalar path
    /* container narrowing rungs (assignable checks, can't be a dictionary hit) — unchanged */
    return new Clr(raw, ctx);                                     // genuinely unowned → rung 2
}
```

This kills CS0111 for free (perimeter and entity door live on different classes) and kills a static the ban never sanctioned (it's neither a factory on the created type nor the thunk — it's lookup + dispatch, the collection's job).

Why not your (2): a static `ConcurrentDictionary<Type, Func<…>>` is the `OwnerOf` map reborn as a delegate cache — a second clr-keyed map beside the collection's index, with creation behavior back in a static hub. Stored-twice + registry-behaves; the exact shape this branch kills. Your instinct that the shared entity must stay context-free was right — but that constraint is about *context*, not the delegate: `_create` is context-free plumbing (context rides as a parameter), so the **per-entity instance field stays**.

**2. `ICreate<T>.Create` takes `object` — the item face and the Clr shuttle both die.** An item IS an object; the callers that hold an item (compare coercion, kind delegation, facets) flow through the same signature. The arms that lived behind the opened box become arms of the one switch — `int i => new(i)` sits next to `text t => Parse(t)` in the same place. No `Create(i.Clr<object>())` forwarding, no transient `Clr(9)` allocated per value birth just to be opened and discarded one frame later. The `object` in the signature is not a clr-leak: this method IS the runtime boundary, and the boundary crossing now happens in exactly one declared place instead of behind a wrapper allocation.

```csharp
// ICreate<T> — the ONE boundary signature (pure) + the courier, same pair as before:
static abstract T?      Create(object? raw, context ctx);   // pure core: null = decline, never throws out
                        Create(object? raw, data.@this d);  // courier: kind/strict off d.Type, narrow catch → d.Fail

// the entity door pair on type.@this mirrors it — (object, context) + (object, data), no collision:
public item.@this? Create(object? raw, context ctx) => (_create ??= Bind(ClrType))(raw, ctx);

// the thunk goes back to logic-free — no bridge, nothing to hide:
static Func<object?, context, item.@this?> Create<T>() where T : item.@this, ICreate<T>
    => (raw, ctx) => T.Create(raw, ctx);
```

## Consequences (pin these — you own the final shape)

- **Context rides explicitly everywhere.** Today's `Create(item)` free-rides on the item's context; a raw `int` carries none. Item-shaped call sites pass it: `Create(b, b.Context)` — one extra argument at the coercion sites, nothing structural.
- **`Clr` is a product, never a shuttle.** It survives only at the collection's unowned-POCO fallback. The "never a clr wrapping a scalar" comment becomes unconditionally true — no transient-mechanism footnote needed anymore.
- **The index is a `FrozenDictionary<System.Type, type.@this>`**, built from the types' `OwnedClrTypes` declarations, frozen after registration (re-frozen when a plugin registers). Rung order is the optimization: `is item` first (cheapest), exact-type frozen hit second (covers every scalar birth), assignable container rungs after, `Clr` last.
- **Per-birth cost budget** (the hottest path in the runtime): one type-check, one frozen-dictionary hit, one cached-delegate invocation. Zero reflective work after first bind, zero transient allocations — the only allocs left on `new Data("a", 9)` are the Data and the `number` itself.

## Supersedes

- `stage2-raw-clr-lift-answer.md`: the **navigated lift** and **`OwnerOf` dies fully** stand; the bridge-inside-the-thunk (`raw as item ?? new Clr(raw, d.Context)`), the `data`-carrying entity door, and the static perimeter are superseded by the above. (Banner added there.)
- The plan's model #4 "`Create` receives a materialized item" — the *materialized* half stands (await sits in front of the door, sync inside); the *item-typed* half becomes `object`.

## Acceptance

- `new Data("a", 9)` → `number(9)` with **zero transient allocations** beyond the Data and the number (no `Clr` on the path — assert via the debugger or an allocation test if cheap).
- `CompareRedesign/Stage4_PerTypeCompareTests` go green (unchanged goal).
- Grep-zero: `static.*Create(object` on `type/this.cs` (the static perimeter is gone); `new Clr(` appears only at the collection's fallback rung and genuine host construction.
- A plugin type registering `OwnedClrTypes` lifts through the frozen index after re-freeze (the extensibility pin).
