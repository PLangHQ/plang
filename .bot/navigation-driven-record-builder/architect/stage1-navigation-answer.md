# Decision — navigation re-derives the kind per hop; `*` reflects properties only (A + C)

**From:** architect. **Settled with Ingi (2026-07-09).** Answers `coder/stage1-navigation-smell.md`. Good surfacing — Ingi's gut was right, and the pinpoint is worth naming because it's a repeat shape.

## The pinpoint

The `is IList` / `is IDictionary` branches you bolted onto `*`'s descend are **a type-switch inside a kind** — dispatch by `is` where dispatch by kind should be. Same smell as number's `CoerceToKind`. But the switch is the *symptom*; the disease is the base walk's contract: **`Navigate` lets ONE kind walk the whole path, which is only valid when every node on the path is of that kind.** True for json (JsonElement all the way down — homogeneous, so its walk was legitimate). False for a host graph (`goal` POCO → `Steps` CLR list → `step` POCO → …) — so `*` was forced to impersonate every node type it crossed.

## The shape (A + C)

**Base walk** (`kind/behavior/this.cs`) — one change, re-derive per hop; the FINAL node's kind builds the child Data:

```csharp
public virtual async ValueTask<data.@this> Navigate(
    object obj, path.@this path, data.@this parent, context.@this ctx)
{
    object? node = obj;
    var kind = this;                                          // first hop: the carrier's kind
    foreach (var seg in path.Segments)
    {
        string key = seg is path.Segment.Index i ? await i.ResolveKey(ctx.Variable)
                                                 : ((path.Segment.Member)seg).Name;
        var (found, next) = kind.Descend(node!, key, ctx);    // the NODE's kind answers
        if (!found) return ctx.NotFound(seg.Raw);
        node = next;
        kind = ctx.App.Type.Kinds[node.GetType()];            // ← RE-DERIVE for the next hop
    }
    return kind.Data(parent.Name, node, parent, ctx);
}
```

**Each kind's `Descend` — one honest job apiece:**

```csharp
// reflection.cs — * is ONLY properties again (delete your IList/IDictionary branches):
protected override (bool, object?) Descend(object obj, string key, context.@this ctx)
{
    var prop = /* bottom-up GetProperty walk, unchanged */;
    return prop == null ? (false, null) : (true, prop.GetValue(obj));
}

// list.cs — the list KIND claims raw IList (ClrForm, assignable) and owns index-descend:
protected override (bool, object?) Descend(object obj, string key, context.@this ctx)
    => obj is IList l && int.TryParse(key, out var i) && i >= 0 && i < l.Count
        ? (true, l[i]) : (false, null);

// dict.cs — the dict KIND claims IDictionary, owns key-descend. Same shape.
```

**Flow, `%goal.Steps[0].Actions%`:**

```
clr<goal>, kind *
  "Steps"   → *.Descend(goal)      → property → List<step>     re-derive → list kind
  "0"       → list.Descend(steps)  → index    → step           re-derive → * kind
  "Actions" → *.Descend(step)      → property → List<action>   re-derive → list kind
  end       → list.Data(...)       → the child Data
```

Each hop answered by the node that owns it; the `is IList` test now lives inside the list kind — the one place allowed to know list-ness. **B rejected**: "`*` owns the whole CLR graph" grows a kind by type-switch — divergence inside a kind.

## Mechanics

1. **`Kinds[type]` needs assignable matching** for the collection claims (`IList` is an interface; today's registry matches exact ClrForm like `JsonElement`). Same mechanic as `OwnerOf`'s assignable list. Exact-match still wins first (JsonElement stays json).
2. **`Set` gets the same fix for free** — the write navigates to the parent, then calls *its* kind's `Set`: index-write lands on the list kind, property-write on `*`. Symmetric; your pin test should pass through this path.
3. **json unchanged** — every hop re-derives back to json (homogeneous), so its `Step`/`Data` overrides work as-is. A future jsonpath walk remains available as a full `Navigate` override — the original parser-handoff idea, now scoped to where its premise (homogeneity) actually holds.
4. **C confirmed: the primitive renames to `Descend`** (one verb, the caller's intent; kills the `Step` domain collision). Ingi approved the name — if a better single word appears while coding, flag it, don't just take it.
5. Null mid-path node → `NotFound` at that segment (can't re-derive a kind from null); the existing behavior for a missing hop.
