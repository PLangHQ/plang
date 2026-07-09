# Decision — write-at-path: `data.Get`/`data.Set`, one leaf door `item.Set`, one-level rebind

**From:** architect. **Settled with Ingi (2026-07-09).** Answers `coder/stage1-navigation-write-gap.md`. Your proposed mechanics are confirmed as-is; writing them out surfaced two unifications that ride along.

## The ruling

**Write-at-path = the READ walk for all but the last segment + one `Set` at the end. The VALUE owns it, like it owns the read.** No second walker, no text-splitting — only segments.

### The pair on `data` (and a rename you should ride along)

```csharp
public ValueTask<@this> Get(string path) => Get(path.@this.Parse(path));
public async ValueTask<@this> Get(path.@this path)
{ /* body = today's Navigate(path), verbatim. GetChild AND public Navigate retire into this —
     Navigate named the mechanism; Get names the caller's intent (the cache.Get ruling). */ }

public async ValueTask<@this> Set(path.@this path, object? value)
{
    var parent  = await Get(path.Parent);              // the READ walk, all but last
    var written = parent-item.Set(path.Last, value);   // the ONE leaf door (below)
    if (!ReferenceEquals(written, parent-item))
        parent.SetValue(written);                      // one-level rebind (below)
    return parent;
}
```

**ADDENDUM (Ingi, same day — supersedes the line that stood here):** kind-level `Navigate` renames to **`Get`** too. The earlier "internal machinery keeps its mechanism name" was a carve-out, and carve-outs rot. The read verb is `Get` at every layer, exactly as the write verb is already `Set` at every layer:

```
read:   data.Get → item.Get / clr.Get → kind.Get → per-hop Descend
write:  data.Set → item.Set / clr.Set → kind.Set
```

`Navigate` dies as a name everywhere (data, item, clr, kind; the json kind's whole-path walk becomes its `Get` override). `Descend` stays — a different operation (one hop, not at-a-path).

### Find #1 — ONE leaf door: `item.Set(leaf, value)`

The parent taking the leaf write is either a native dict/list (plang item) or a clr host — today two doors: the bool-probe `item.Write(key, value)` (`variable/list:349`) and `kind.Set(host, key, value)`. Two doors, one job — and `item.Write(key,value)` collides with `item.Write(IWriter)` (serialization): one name, two unrelated meanings. Unify:

```csharp
// item/this.cs — the child-write door; returns the (possibly replaced) value:
public virtual item.@this Set(Segment leaf, object? value)
    => throw new ...($"%…% ({Mint().Name}) cannot take a child");

// dict     → its native keyed Set (exists)         // list → index into its rows
// clr      → Kind.Set(Value, leaf, value, Context)  // hosts route to the kind
// snapshot → its OWN override → SetVariable(...)    // the deferred item exception keeps
//                                                   // its behavior as an ordinary Set override
```

The bool-returning `item.Write(key, value)` dies. Surface symmetry: `Get`/`Set` on data, `Descend`/`Set` on kinds, `Set` on items — one verb everywhere.

### Find #2 — one-level rebind, preserved exactly

A leaf `Set` may return a **replacement** (the json kind materializes an immutable element into a dict). `data.Set` rebinds the parent when the returned instance differs — today's `if (!ReferenceEquals(result, target)) parent.SetValue(result)`, preserved. For clr hosts this is moot (in-place, live reference). **Scope pin:** a *deep* write into json (`set %plan.a.b%`, two hops into immutable elements) rebinds one level only — whether the materialized child reconnects to the root is **pre-existing behavior; keep it exactly as it is and pin it with a test.** Do not expand (it touches the deferred COW/value-semantics question).

### The kinds — leaf carries the segment fact; `Key` rename

- **`Segment.Index.ResolveKey` → `Segment.Index.Key`** (Ingi's rename; it stays the ONE bracket-variable resolver, async — so kind `Set`/`Descend` bodies are async, consistent with the walk).
- **list kind gains `Set`:** Index leaf → `((IList)host)[i] = value is item iv ? iv.Clr(elementType) : value` — inline, in-place, returns the same host. Member leaf → `base` (the `*` property-set).
- **`*` kind `Set`:** unchanged from the clr spec — reflect the property, `value.Clr(PropertyType)`, one line.
- New slices on `path.@this` (approved): `Root` (always a Member), `Tail`, `Parent`, `Last`.

### `variable/list.Set` restructure — and what dies NOW (earlier than the Stage-2 slate)

```csharp
public async ValueTask<data.@this> Set(string name, object? value)
{
    // (1) stays verbatim — the %x%-reference ShallowClone preamble (:122-126)
    var path = path.@this.Parse(name);
    // (2) stays verbatim — the whole simple-rebind block behind `path.Tail.IsEmpty`
    //     (Calls overlay routing, OnCreate/OnChange/OnDelete carry, FireOnChange, OnSet)
    // (3) new — the deep write:
    var root = await Get(path.Root.Name);
    return await root.Set(path.Tail, value);
}
```

**Dies here, Stage 1:** `SetValueOnObject` + `ConvertToDictionary` + `ConvertForDictSlot` + `GetStringKeyedDictInterface` (the whole tail — already `[Obsolete]`-marked, was slated Stage 2; the snapshot arm becomes snapshot's own `item.Set` override, per the deferral ruling); `ResolveBracketIndices` (the walk's `Segment.Index.Key` resolves per hop — the pre-pass was a workaround for text-splitting); `GetRootName`; `GetChild`; public `data.Navigate`; `item.Write(key, value)`.

## Acceptance

- `Set_GoalStepsBracketIndex_PreservesGoalIdentity` green (identity holds — in-place host write).
- A pin test for the one-level rebind on json (today's behavior, whatever it is — captured, not changed).
- Grep zero: `ResolveKey`, `SetValueOnObject`, `GetChild`, `GetRootName` outside `[Obsolete]` corpses.
