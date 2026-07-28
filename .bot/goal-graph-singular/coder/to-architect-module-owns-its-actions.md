# To architect — the registry owns each module's actions; that's the obpv under `Mint()`

**Branch:** goal-graph-singular
**From:** coder
**Status:** Ingi-named violation, found mid-Stage-B. Not yet implemented — scoping it here.

## What Ingi named

> "`_list.GetActions(Name)` is obpv — actions should not live in module, they live in actions."
> "I think having a `.Mint()` is hack, it's to fix a bug that is from other place, either you create a module with needed variable or it fails."

Both land on the same defect, and it is one level under the code I was editing.

## The shape today

`app.module.list.@this` holds a **naked nested collection**:

```csharp
ConcurrentDictionary<string, ConcurrentDictionary<string, ActionEntry>> _modules;
```

The registry — not the module — owns each module's action map. Everything downstream follows
from that:

- **The module asks its parent for its own children.** `module.@this.Mint()` calls
  `_list.GetActions(Name)`, `_list.IsCacheable(Name, actionName)`, `_list.GetActionType(...)`.
  A child fetching its own contents from the collection that holds it.
- **Six verb+noun accessors on the registry doing the module's job**, each taking the module
  NAME as a parameter: `GetActions(module)`, `GetActionType(module, action)`,
  `IsCacheable(module, action)`, `Contains(module, action)`, plus `Names`, `Count`.
- **`Mint()` exists only to bridge that gap** — a lazy fetch-and-materialize because the module
  is born without what it IS. That laziness is what produced the half-built-catalog race I hit
  (`_actions` was both the guard and the buffer; published empty, then filled).

## Why my two attempts were both the wrong layer

1. **Publish-last inside `Mint()`** (landed in `05f55661a`) — fixed the race, kept the hack.
2. **Build the catalog in the module's constructor** (written, tested, then REVERTED) — born
   complete, no half state, and it forced the registry to actually invalidate `_elements` on
   mutation (the existing comment claimed it did; nothing did). But it still *fetches from the
   parent* — it only moves the fetch earlier. It also shifted startup timing enough that two
   path-scheme tests began failing in the parallel suite (they pass 3/3 in isolation), i.e. it
   perturbed a pre-existing shared-state race in scheme registration without fixing anything real.

Reverted; the tree is clean at Stage B.

## Target shape

```csharp
app.module.list.@this      // owns MODULES — selection + lifecycle only
    Dictionary<string, module.@this> _modules;
    module.@this this[string name];

app.module.@this           // owns ITS actions — they live here
    action.list Action;                  // its own collection, not fetched
    action.@this? this[string actionName];
    Type? Handler(string actionName);
```

Consequences:

- `GetActions/GetActionType/IsCacheable/Contains(module, action)` **collapse** into
  `list[module]` + the module's own members. The name-keyed pairs die.
- `RegisterType/Register(module, action, …)` become: get-or-create the module element, then the
  **module** adds its own action. Registration stops reaching two levels deep.
- **`Mint()` does not get fixed — it disappears.** There is nothing to mint: the module already
  has its actions. The `_elements` staleness question dissolves with it (no second cache of what
  the module already owns).
- `DisposeAsync` / `Count` / `Describe` walk modules and ask each for its own actions.

## Scope + call I need

~6 registry methods plus their call sites (`Discover`, `Describe`, `GetCodeGenerated`, dispose,
`Count`, and the build/test/type consumers that pass `(module, action)` string pairs). It is
mechanical once the shape is fixed, but it is a real restructure of the registry, so I want it
ruled rather than assumed — particularly:

1. Does `module.@this` hold `action.list.@this` (the existing program node) for its catalog, or a
   registry-local collection? The catalog elements are `action.@this` instances but they are
   *catalog-zoom*, not `.pr` program nodes — reusing `action.list` may conflate two things.
2. Modifiers: today the module keeps `_actions` and `_modifiers` as two homes ("the type IS the
   role"). Does that survive as two collections on the module, or one collection the module
   filters by type?

`ActionEntry` (type + optional shared instance) is the registry's own lifecycle record — I assume
it stays, owned by the module now. Confirm if not.

## Also for you: a ruling of Ingi's that contradicts `module-owns-action.md`

The doc specifies `action.ToString() => $"{_module.Name}.{Name}"` and "all `$"{a.Module}.{a.Name}"`
sites become `$"{a}"`". Ingi overruled that:

> "tostring should not give us {module}.{actionName} … each object owns itself, module.name,
> action.name, etc., so when we need both, you use both objects"

So `action.ToString()` is the action's own name, `module.ToString()` is the module's own name, and
the qualified form is composed at the use site (`$"{a.Module}.{a.Name}"`,
`{{ a.Module }}.{{ a.Name }}`). Implemented that way in Stage A/B. The doc should be corrected.
