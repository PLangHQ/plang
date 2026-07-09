# Stage 1 — navigation walks a cross-type path in ONE kind (design smell, for architect)

**From:** coder. **Context:** Stage 1 drop is in progress — `goal`/`step`/`action`/`actions`
now drop `item.@this` and flow as `clr<T>` (host, navigated/written by the `*` reflection
kind). Ingi flagged this while I was making `%goal.Steps[0].Actions%` navigate/write work:
*"I don't like this, something is not right."* Surfacing before committing.

## The code

`app/type/kind/behavior/this.cs` — the walk (`Navigate`) and the descend primitive (`Step`):

```csharp
public virtual async ValueTask<data.@this> Navigate(
    object obj, path.@this path, data.@this parent, context.@this ctx)
{
    object? node = obj;
    foreach (var seg in path.Segments)
    {
        string key = seg is path.Segment.Index i ? await i.ResolveKey(ctx.Variable)
                                                  : ((path.Segment.Member)seg).Name;
        var (found, next) = Step(node!, key, ctx);   // <-- ALWAYS `this` kind's Step
        if (!found) return ctx.NotFound(seg.Raw);
        node = next;                                  // node changes type every hop
    }
    return Data(parent.Name, node, parent, ctx);      // kind re-derives ONLY here (last hop)
}

protected virtual (bool found, object? node) Step(object obj, string key, context.@this ctx)
    => throw new NotSupportedException($"kind '{Kind}' is not navigable");
```

`app/type/kind/behavior/reflection.cs` — the `*` kind's `Step`, with the branches I had to add:

```csharp
protected override (bool, object?) Step(object obj, string key, context.@this ctx)
{
    if (obj is IList list && int.TryParse(key, out var idx))          // <-- ADDED
        return idx >= 0 && idx < list.Count ? (true, list[idx]) : (false, null);
    if (obj is IDictionary dict)                                       // <-- ADDED
        return dict.Contains(key) ? (true, dict[key]) : (false, null);

    PropertyInfo? prop = null;                                        // the original body
    for (var t = obj.GetType(); t != null && prop == null; t = t.BaseType)
        prop = t.GetProperty(key, BindingFlags.Public | BindingFlags.Instance
            | BindingFlags.IgnoreCase | BindingFlags.DeclaredOnly);
    return prop == null ? (false, null) : (true, prop.GetValue(obj));
}
```

## Why it's wrong (three things converge)

1. **One kind walks the whole path.** `Navigate` loops `Step` in a SINGLE kind (`this`) for
   every segment, but `node` changes type each hop: `goal` (POCO) → `GoalSteps` (`IList<Step>`)
   → `step` (POCO) → `StepActions` (`IList<action>`). The comment concedes it — *"a container
   node stays raw for the next hop; the last hop builds the child Data (its kind derives
   again)."* Kinds only re-derive at the **final** hop, never mid-path. So the `*` kind is
   forced to handle every node type the path crosses.

2. **`*`'s job was "reflect public properties" — now it walks collections.** `%goal.Steps[0]%`
   made the `*` kind's `Step` get `"Steps"` (a property → an `IList`) then `"0"` (an index).
   Pure `GetProperty("0")` returns null → navigation dies → the write never lands (the pin
   test's `Actions.Count` was 0). So I bolted `IList`/`IDictionary` indexing onto `*`. A raw
   `List<Step>` inside a POCO has **no kind of its own** (it's not a plang `list.@this`; it
   falls to the catch-all `*`), so this isn't duplicating the `list`/`dict` *kinds* — but it
   is stretching `*` from "reflect properties" to "navigate an arbitrary CLR graph including
   its collection nodes."

3. **`Step` the NAME collides with the domain `Step`.** The navigation "descend one level"
   primitive is literally called `Step`, right next to a goal's `Step` — reading
   `Step(node, "Steps")` is genuinely confusing. Ingi read it as the goal step.

## Options (need the architect's steer)

- **A. Re-derive the kind per hop** — `Navigate` dispatches each segment to *node's* kind's
  descend, not `this`. Removes collection-walking from `*` — *if* a raw CLR `IList`/`IDictionary`
  can be given a kind (today they resolve to `*`, so either the `list`/`dict` kind learns to
  navigate a raw `IList`, or `*` still owns them). This is the structural fix; it changes the
  core walk.
- **B. Keep `*` as the CLR-graph navigator** (my current change) — accept that the reflection
  kind owns walking any CLR object graph, collections included. Simplest, but `*` grows.
- **C. Rename the primitive** (`Descend`/`Enter`/`At`) regardless of A/B, to kill the `Step`
  collision.

## Where this bites

Only surfaced now because the drop made `goal`/`step`/`action` flow as `clr<T>` navigated by
`*`. Before, they were `item.@this` with their own `Navigate`, so the path never crossed into
`*`. Pin test `ClrJsonActionsWriteTests` (the `%goal.Steps[i].Actions%` write) is the repro.

**Ask:** A (per-hop kind re-derivation), B (`*` owns CLR-graph nav), or a different shape —
and do we rename `Step`? I've paused the drop's commit on this.
