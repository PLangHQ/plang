# `ParameterSchema` ruling — deleted; the handler is registry state, selected by identity

Answer to `coder/to-architect.md` (the ParameterSchema obpv, Ingi-flagged), settled 2026-07-16.

> **You own this.** Shapes reviewed in chat; bodies and factoring yours.

## The model sentence (goes in the plan)

*An action NAMES its handler (`Module` + `ActionName`); the collection OWNS it; reflection happens at the catalog leaf through the owner's door — a raw `System.Type` never rides on a domain object.*

The handler type already has one true home: the module collection's index (`ActionEntry(Type, Instance)`), keyed by exactly the identity the action already carries on the `.pr`. So `ParameterSchema` isn't renamed — it is **deleted**, and nothing replaces it on the action. Both smells die at once (the stored CLR handle and the compound noun standing in for "the handler class").

## The three consumers

1. **The class-zoom leaf** (`Properties`/`Return`/attribute facts in `this.Schema.cs`): the catalog action has `Context` (stamped at mint) — resolve the handler through the owner: `Context.App.module[Module]` (cached element) → an **internal `Handler(actionName)` door on the module element** handing the `System.Type` TRANSIENTLY for the one reflection pass. Touched at the sanctioned leaf, answered by its owner, stored nowhere. The lazy caches (`_rows`, `_return`) stay on the action instance.
2. **4c.3 — modifier grouping: built actions do NOT self-identify.** Modifier facts are CATALOG facts, joined by identity at the grouping site. Both `GroupModifiers` callers are build-time (your hot-path trace), catalog in hand: the walker asks `app.module[a.Module].Actions[a.ActionName].IsModifier` / `.ModifierOrder` per action. No `ParameterSchema` spread onto the `.pr` path, no Context stamping on built actions — and the obpv you were actually hunting (`GroupModifiers(modules)` leaning on registry methods `IsModifier(module, action)`/`GetModifierOrder(module, action)`) dies into element facts. Whether the walker reaches the catalog via a parameter or its own context is your call — the model is only: identity-join against catalog elements.
3. **`Describe()`** (the other setter, alive until 4e): served the same internal way off the catalog mint until it dies.

## Why not the other candidates (on the record)

- **"The action IS the handler"** fails where you suspected: a built action is the `.pr` record, not the handler instance — the identity can't hold across zooms.
- **"Store the resolved plang entities instead of the Type"** half-works but smuggles the question back for every next fact (`IsModifier`, `ModifierOrder`, `Defaults`, prose) — each would need pre-computing at mint. The door keeps facts lazy with one owner.

## Demolition

- `action.ParameterSchema` (`action/this.cs:14`) — the property, its two setters (catalog mint `module/this.cs:36`, `Describe` at `list/this.cs:433`), and every read in `this.Schema.cs` (re-pointed through the element door).
- The registry per-action queries this makes redundant go with 4e as already planned (`IsModifier(module, action)`, `GetModifierOrder`, `GetDefaults` — element facts now).

## Pins

- Class-zoom `Properties`/`Return` unchanged in output (parity goldens already prove them).
- `GroupModifiers` output byte-identical on a goal with modifiers (build a Sanity goal with `on error`/`cache` clauses; diff the `.pr`).
- A `.pr`-zoom action touching `Properties` still fails with the documented contract error (no Context) — unchanged.
- `code.load` module: its actions group modifiers correctly through the catalog join (the registry mutation invalidates elements; the join picks up fresh ones).

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `ParameterSchema` deleted | no stored CLR handle on a domain object; no compound-noun stand-in | ok |
| internal `Handler(name)` on the element | the owner answers; transient hand-over at the sanctioned reflection leaf | ok |
| identity-join for modifier facts | catalog facts joined by the identity the action already carries; no self-ID, no spread | ok |
| registry per-action queries die into element facts | behavior on the element, selection on the collection | ok |
