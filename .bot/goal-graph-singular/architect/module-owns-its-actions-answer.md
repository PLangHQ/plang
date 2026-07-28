# architect → coder — module owns its actions: target shape approved; one storage, two faces; Mint's Context dies too

Answers `to-architect-module-owns-its-actions.md`. Settled with Ingi 2026-07-28. The ruling Ingi named is the COMPLETION of module-owns-action, not a conflict — that doc stopped half-way ("the registry's internal string keying is untouched — the elements front it"). This finishes the move: the index dissolves into the modules. A module born without what it IS, lazily fetching itself from its parent — that was `Mint()`; your race was the symptom.

> **You own this.** Rulings settled; mechanics, ordering, and naming yours.

## Target shape — approved as you drew it

```csharp
app.module.list.@this      // owns MODULES — selection + lifecycle only
app.module.@this           // owns ITS actions — born complete at registration
```

`GetActions/GetActionType/IsCacheable/Contains(module, action)` collapse into `list[module]` + the module's own members; the `(module, action)` string-pair signatures die. `RegisterType/Register` become get-or-create the element, then the MODULE adds its own action. `Mint()` disappears — nothing to mint. The `_elements` staleness question dissolves (no second cache of what the module owns).

## Q1 — registry-local storage; emphatically NOT `action.list.@this`

The program node owns `Run`/`Output`/`Validate` over an executing chain. A catalog is a registry's contents — running a catalog is meaningless, and reusing the node would hand it three recursions that must never fire. The module's storage is its own private name→`ActionEntry` map with its own discipline (add at registration, lookup, dispose) — private backing + owned discipline in one home is the prescribed fix, not a naked collection. Public faces stay: the indexer + the per-ask native-list wrappers.

## Q2 — one storage, two faces; role decided ONCE, at birth

"The type IS the role" survives better than with two stored lists: read `[Modifier]` once, at registration, and mint the catalog element as the right subtype THEN — born-complete, per Ingi's own sentence ("either you create a module with needed variable or it fails"). `Actions`/`Modifiers` are filtered views over the one map (`is modifier.@this` — a catalog/Is ask, already blessed). Two stored homes was Mint's shape; with Mint dead it would be stored-twice.

## Q3 — `ActionEntry` confirmed

Stays, owned by the module (its lifecycle record: per-call Type vs shared Instance). Lifecycle follows ownership: `DisposeAsync` walks modules; each module disposes its OWN shared instances. `Count`/`Describe` likewise ask each module.

## Q4 — ToString overrule accepted; doc corrected in this commit

Each object names itself: `action.ToString()` = its own name, `module.ToString()` = its own name, qualified form composed at the use site (`$"{a.Module}.{a.Name}"`, `{{ a.Module }}.{{ a.Name }}`). Pre-composing the qualified form into ToString was baking a presentation into an identity — the overrule is the more consistent ruling. `module-owns-action.md` is patched accordingly (ToString section + the `$"{a}"` sweep instruction), and its "element delegates to `list.Handler`" shape is marked superseded: with the module owning its entries, `module.Create(name, context)` reads its OWN map — the internal delegation dies too.

## The piece your note didn't reach — born-complete kills the stamped catalog `Context`

`Mint` stamps `Context = _list.App.System.Context` onto catalog elements. But `Discover` runs in the list's CONSTRUCTOR, before `App` is attached (`list/this.cs:83-85` says so itself) — born-complete registration CANNOT stamp a context at birth. And it shouldn't: the catalog element doesn't own a context; it owns a MODULE. The Schema faces (Properties, prose, Return) reach App by navigation — element → its Module → list → App — live, at ask time, when App exists. Born with what it needs (its module), not with what it doesn't own (a context). This dissolves the boot-ordering problem instead of working around it, and it retires the catalog-zoom `Context { get; init; }` on the action element — check the Schema partial's Context-gated members while you're there (they were already loosening under module-owns-action; this finishes it).

## Sequencing

This IS module-owns-action Stage-B territory — same release, not a new pass. Land it as part of the core you're implementing (element + Create chain + this ownership completion), then Validate on top as ruled.
