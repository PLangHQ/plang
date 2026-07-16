# Modifier ruling — `modifier` is a type; catalog splits; `GroupModifiers` → `Nest` (interim) → gone (2b)

Answer to `coder/to-architect.md` (the modifier reshape, Ingi-seeded), settled 2026-07-16. Your three questions answered, then the traced implementation.

> **You own this.** Code reviewed with Ingi in chat for shape; bodies/factoring yours. Traced against `d96fdf89b`.

## The rulings

1. **`modifier : action` — a subtype.** A modifier shares the whole mechanism (handler, params, `Run()`, dispatch, `.pr` reading); a shared-base split would fork every dispatch/reader surface for nothing. The "never stands alone" role is enforced STRUCTURALLY — a modifier is born inside a target's `Modifiers` slot — not by the type lattice. Folder per the `path/file` precedent: `action/modifier/this.cs`. Note: `action.@this` is `sealed` today — it unseals; that line is deliberate.
2. **Both 2a and 2b, sequenced.** 2a now (the interim below); 2b — LLM emits nested, the parse builds `modifier` instances into the slot, the seam dies — is the endpoint but is builder-emit surgery (LLM-contract change, compile-quality gate, cache invalidation), so it's a LOGGED FOLLOW-ON, not Stage-4 scope. Bonus fact for 2b's file: the two LLM schemas already disagree — `QueryAndVerify` has no `modifiers` field, `FixValidation` already carries `modifiers?: list<…>` (`BuildStep/Start.goal:46` vs `:63`) — 2b unifies an existing inconsistency, it doesn't invent a contract.
3. **Level 1 (catalog split) is in-scope now** — it IS the module-discovery reshape; your interim `IsModifier`/`ModifierOrder` element facts are REPLACED by the type, as you intended.

Model sentence for the plan: *a flag discriminating behavior is a type wanting to exist — `IsModifier` was the flag, `modifier` is the type, and its wrapping role is enforced by where it is born, not by a check.*

## Two constraints the trace surfaced (they shape the code)

- **The flat list from the LLM is built as plain `action` hosts** (the json read constructs by declared element type — it cannot know roles). So the grouping seam is STILL a catalog join; the type-check happens on the CATALOG element. The type's big wins are the catalog/slot/templates; the seam improves modestly until 2b deletes it.
- **`Order` is not stored in the `.pr`** — position in the slot carries it at runtime (today's behavior, kept). `Order` is a catalog fact stamped at mint from `[Modifier(Order = N)]`.

## The code

### 1. The type — `action/modifier/this.cs` (NEW); `action.@this` unseals

```csharp
namespace app.goal.steps.step.actions.action.modifier;

/// <summary>A modifier — an action whose ROLE is to wrap the preceding action
/// (cache.wrap, error.handle, timeout.after). Same mechanism as any action
/// (handler, params, Run, dispatch); the type IS the role. It exists only inside
/// a target's Modifiers slot — never standalone; enforced by where it is born,
/// not by a check.</summary>
public class @this : global::app.goal.steps.step.actions.action.@this
{
    /// <summary>Nesting order (lower = outermost wrapper) — from [Modifier(Order = N)] at
    /// catalog mint. Not stored in the .pr: position in the Modifiers slot carries it at runtime.</summary>
    public int Order { get; init; }
}
```

### 2. The `Modifiers` slot becomes typed

`modifiers.@this` (the existing collection with the `RunAsync` wrap logic — behavior untouched) changes element type `action.@this` → `modifier.@this`. Payoff: `.pr`-loaded modifiers are BORN `modifier` instances — the reflection kind constructs by declared `PropertyType`, the Stage-1 rule doing the work.

### 3. The module element — one mint walk, two homes, and the selection door

```csharp
// module/this.cs — replaces the single _actions mint
private List<action.@this>? _actions;
private List<action.modifier.@this>? _modifiers;

private void Mint()
{
    _actions = new(); _modifiers = new();
    foreach (var name in _list.GetActions(Name))
    {
        var attr = Handler(name)?.GetCustomAttribute<global::app.module.ModifierAttribute>();
        if (attr != null)
            _modifiers.Add(new action.modifier.@this { Module = Name, ActionName = name, Order = attr.Order, Context = _list.App.System.Context });
        else
            _actions.Add(new action.@this { Module = Name, ActionName = name, Context = _list.App.System.Context });
    }
}

public global::app.type.item.list.@this Actions   /* native list over _actions — mint-once, as today */
public global::app.type.item.list.@this Modifiers /* native list over _modifiers */

/// <summary>Select one catalog element by action name — action OR modifier; the type answers the role.</summary>
public global::app.goal.steps.step.actions.action.@this? this[string actionName]
{
    get
    {
        if (_actions == null) Mint();
        return _actions!.FirstOrDefault(a => Match(a)) ?? _modifiers!.FirstOrDefault(m => Match(m));
    }
}
```

Discovery/registry UNTOUCHED — the index doesn't care about role; the element routes at mint (smaller than your note feared: "touches discovery" turned out false).

### 4. `GroupModifiers` → **`Nest`** (Ingi named it; single verb, the collection reshaping itself)

```csharp
/// <summary>Nests each modifier onto the preceding action's Modifiers slot (flat LLM
/// order → the .pr shape), ordered by the catalog modifier's Order. A leading modifier
/// with no preceding action is dropped with a warning. Mutates in place.</summary>
public void Nest(global::app.module.list.@this modules)
{
    if (_items.Count == 0) return;
    var flat = _items.ToList();
    _items.Clear();
    action.@this? current = null;

    foreach (var a in flat)
    {
        // the catalog element answers the ROLE — a modifier is a TYPE there, not a flag
        if (modules.Contains(a.Module)
            && modules[a.Module][a.ActionName] is action.modifier.@this catalog)
        {
            if (current == null) { /* DroppedLeadingModifier warning — unchanged */ continue; }
            // the flat item was read as a plain action host; it becomes what it IS —
            // a modifier in its target's slot, order from the catalog
            current.Modifiers.Add(new action.modifier.@this
                { Module = a.Module, ActionName = a.ActionName, Parameters = a.Parameters, Order = catalog.Order });
        }
        else { current = a; _items.Add(a); }
    }

    foreach (var a in _items)
        a.Modifiers.Sort();   // by Order — the collection sorts its own; no registry callback
}
```

`GroupModifiersRecursive` renames/dies with it. Under 2b, `Nest` disappears entirely.

## Demolition (this piece)

- `this.Schema.cs:27-34` — the interim `IsModifier`/`ModifierOrder` (replaced by the type + `Order`).
- `modules.IsModifier(module, action)` + `GetModifierOrder(module, action)` — lose their last caller HERE; deleted now, not waiting for 4e.
- `GroupModifiers`/`GroupModifiersRecursive` — renamed to `Nest`/its inner (interim); slated to die at 2b.
- At 4d: the summary templates' `where: "IsModifier", true/false` gymnastics — the `# Modifiers` section renders `module.Modifiers` structurally.

## Pins

- `.pr` byte-identical on a modifier-bearing Sanity goal (`Order` not stored; position carries it; `[Store]` faces unchanged).
- `Modifiers.RunAsync` wrap behavior untouched (`modifier : action` — dispatch identical).
- The leading-modifier warning preserved verbatim.
- A `code.load` module's modifier nests correctly (element-cache invalidation → fresh catalog join).
- Catalog parity: the rendered `# Modifiers` section byte-identical (the goldens).

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `modifier : action` | the flag became a type; role enforced structurally; one dispatch mechanism | ok |
| typed `Modifiers` slot | .pr modifiers born as what they are (declared-type construction) | ok |
| two homes on the element | structural distinction, no boolean discriminator anywhere | ok |
| `Nest` | single verb, the collection's own reshaping; the catalog join is explicit | ok |
| `Order` on `modifier` | the qualifier died with the homelessness ("ModifierOrder" was Order in exile) | ok |
| registry per-action modifier queries deleted early | behavior on elements; selection on collections | ok |
