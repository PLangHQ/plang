# Registry internals — Event.@this storage and lookup

## What lives where

```
PLang/App/Event/this.cs                  -- @this — the registry (renamed from Events/this.cs)
PLang/App/Event/On.cs                    -- the On enum
PLang/App/Event/Phase.cs                 -- Before | After
PLang/App/Actor/Context/Event/this.cs    -- per-actor registry (renamed from Events/this.cs)
```

The same `Event.@this` class type is used at both App and Context
levels (same as today's `Events.@this` is used for both). The class
itself is identical; the instances live at different ownership levels
with different lifetimes.

`Channel/Event/` folder is **deleted**. Channels don't own bindings.

## Public API on Event.@this

```csharp
public sealed class @this
{
    // Registration
    public string Register(Binding binding);
    public bool   Unregister(string id);
    public void   Clear();
    public List<Binding> Save();
    public void   Restore(List<Binding> snapshot);

    // Fire surface — uniform contract
    public Task Before(On on, Data source);
    public Task After(On on, Data source);

    // Fast-path check (used by Context.event when consulting App)
    public bool HasAnyBindingsFor(On on);

    // Inspection (mostly for tests / diagnostics)
    public int Count { get; }
    public IReadOnlyList<Binding> GetBindings(On on);

    // OBP: notify consumers (e.g. cached lookups) when bindings change
    public System.Action? OnChanged { get; set; }
}
```

`Binding` is a private nested record inside `Event.@this` — no public
`Binding.@this` type (per Ingi 2026-05-12 — drop the `.Binding.`
namespace level).

## Internal storage

```csharp
// One bucket per (On, name). name="" for category-wide bindings (App, Read, etc.).
private readonly Dictionary<(On on, string name), List<Binding>> _byKey;

// Class-level fast path: which On values have any bindings at all.
private ImmutableHashSet<On> _activeOns;   // lock-free reads

private readonly object _writeLock;        // taken only on register/unregister
```

`_byKey` is the primary index. Lookup at fire time:

```csharp
public async Task Before(On on, Data source)
{
    if (!_activeOns.Contains(on)) return;    // fast path — no bindings anywhere for this On

    var name = source.Name ?? "";
    
    // O(1) bucket lookup
    if (_byKey.TryGetValue((on, name), out var bucket))
        await FireMatching(bucket, source, Phase.Before);

    // Also check the wildcard bucket (bindings with name=null/empty pattern)
    if (name != "" && _byKey.TryGetValue((on, ""), out var anyName))
        await FireMatching(anyName, source, Phase.Before);
}
```

`FireMatching` walks the (typically small) per-bucket list, applies
path-pattern filter and phase filter, runs each binding's handler in
priority order.

## Two-tier walk inside Context.event

`Context.event.Before` consults App.event for `scope:app` bindings:

```csharp
public async Task Before(On on, Data source)
{
    await BeforeLocal(on, source);                          // bindings on this Context
    if (App is not null) await App.event.Before(on, source); // bindings on App
}
```

Order matters: actor-scoped bindings fire first, then app-scoped. Or
the reverse — open question for Ingi. Today's order is the equivalent
of "register order" within a single tier; cross-tier ordering is new.
Priority field can disambiguate within tier, but cross-tier needs a
rule. **Lean: actor-scoped first.** Most-specific scope runs first.

## Fast path cost

Steady state with zero bindings for `On.Variable`:

```
ImmutableHashSet<On>.Contains(On.Variable)   →  hash lookup, branch-not-taken, return
```

Two operations, JIT-friendly, no allocation. ~5ns on modern hardware.

When `On.Variable` has at least one binding registered, the lookup
crosses to the Dictionary. `_byKey.TryGetValue((On.Variable, "step"))`
is a single hashed-pair lookup — O(1) average, ~20ns.

The binding list per `(On, name)` key is typically 0-2 entries.
Iteration is a tight loop with pattern matching on path.

## Why ImmutableHashSet for `_activeOns`

The Variable category fires from `Data.Value` — potentially every
property access in the runtime. Read frequency is hot; write frequency
(register/unregister) is cold. Lock-free reads are mandatory.

Two implementations work:
- **`ImmutableHashSet<On>`** — reads are lock-free by construction;
  writes create a new instance via `Add`/`Remove` and a single
  reference swap. Cost on write: O(n) per modification. n is bounded
  (10 enum values), so it's fine.
- **`int` bitmask** — `On` has 8 values, fits in a byte. `_activeOns
  & (1 << (int)on)` is single-instruction. Even faster, atomic
  read/write via `Interlocked` if needed.

**Lean bitmask.** Cheaper still and the API can keep its `Contains`-
shaped public surface (`HasAnyBindingsFor(on) => (mask & (1 << (int)on)) != 0`).

## OnChanged

Existing `Events.@this` has `OnChanged` for cache invalidation. New
shape keeps it. The variable resolver's path-resolution cache (if any)
invalidates on changes. Same shape as today.

## Save / Restore

Used by test mocks today (`mock/action.cs:73`) to snapshot and restore
the registry. Keep the API; serialize the bucket dictionary and the
mask.

## Index maintenance

On `Register(binding)`:
1. Compute `(on, name)` key — `name` from binding's pattern. If the
   pattern has wildcards (`Api/*`), key uses `""` (the wildcard
   bucket); otherwise key uses the literal `name` for fast lookup.
2. `lock (_writeLock)` → append to bucket → update `_activeOns` mask
   → assign new ImmutableHashSet (or set bitmask bit) → fire OnChanged.

On `Unregister(id)`:
1. Find binding across buckets (one linear scan acceptable — unregister
   is cold).
2. Remove. If bucket becomes empty, check if any other bucket has the
   same `On` value; if not, clear the bit in `_activeOns`.

## Wildcard bucket trade-off

Wildcard bindings (pattern with `*` / `?` / regex) go into the `""`
bucket and are checked on every fire for that `(On, *)`. If a user
registers 1000 wildcard-step bindings, every step fire walks all 1000.

Acceptable for v1. Pattern compilation happens once at register time
(compiled glob → regex or matching function), so per-fire cost is one
function call per wildcard binding. Optimization opportunity if it
becomes a real hot path: precompute "wildcard bindings that could
match name X" with a trie or similar. Not in scope for this branch.

## What happens when a handler throws

Today's behavior: exception bubbles up through `lifecycle.Before.Run`
and aborts the step. Same shape in the new design — `Event.@this.Before`
awaits each handler; an exception escapes; caller decides.

Convention preserved: Before-handlers can abort the action by
returning `Data.Fail(...)`; the caller (engine fire site) checks the
returned Data and aborts if `!Ok`. After-handlers' returns are
inspected for logging but typically don't abort.
