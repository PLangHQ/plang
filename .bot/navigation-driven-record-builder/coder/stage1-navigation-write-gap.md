# For architect — the navigation-WRITE counterpart to `.Count`: setting into a clr-carried host

**From:** coder. **2026-07-09.** Follows `stage1-read-path-answer.md` (your read-path A + `.Count`
segment-dispatch both landed and green). This is the write sibling — the last item-drop regression
in Data, and it shows up in some Modules builder tests too.

## Symptom

`set %goal.Steps[0]% = newStep` (a variable holding a goal; exactly what `builder.merge` does)
no longer lands. `Set_GoalStepsBracketIndex_PreservesGoalIdentity`: after the set, `%goal.Steps[0].Text%`
reads null instead of the new step's text.

## Root cause — write-split isn't segment-aware, and the list kind has no `Set`

The read side now dispatches on segment kind (Index vs Member) — you designed that. The WRITE side
(`variable.list.Set`) predates the kind model and still splits the path on the **last dot**:

```
name = "goal.Steps[0]"
rootName = "goal";  remaining = "Steps[0]";  lastDot(remaining) = -1
→ parent = the goal;  leaf propertyName = "Steps[0]"      // WRONG — the bracket wasn't a boundary
→ target.Write("Steps[0]", …) fails, then Kind.Set(goal, "Steps[0]", …)
→ reflection.Set: GetProperty("Steps[0]") == null → "no writable property 'Steps[0]'"
```

At baseline the goal was an `item` whose own `Write` decomposed `"Steps[0]"`. Now the goal rides as
`clr<goal>`, and two things are missing for the write to reach the list element:

1. **The write-split isn't segment-aware.** It should segment `goal.Steps[0]` the same way read
   navigation does — `[goal] . [Steps] . [0]` — take all-but-last as the parent (`goal.Steps`, a
   list) and the last segment as the leaf (index `0`).
2. **The list kind owns no `Set`.** Only `reflection` and `json` override `Set` today; `list` inherits
   the base throw. An index write into an `IList` host has no home.

## Proposed shape (for your confirm — write is your design call, like read was)

Symmetric to the read fix:

- **Write-split goes through the path grammar**, not `lastDot`: parent = navigate segments[..^1];
  leaf = segments[^1], carrying its `isIndex` fact — same `Segment.Index` vs `Segment.Member` the
  read side now uses.
- **`Set` takes the same `isIndex` fact** the way `Descend` now does. The **list kind** gains a
  `Set`: `isIndex` → `IList[i] = value` (positional write); a member leaf on a sequence → the `*`
  kind reflects the settable property (mirror of the `.Count` read path, though a settable member
  on a list host is rare).
- `clr` already routes `Kind.Set(clrTarget.Value, …)` (the write-if-setter face in your clr spec),
  so once the list kind owns index-`Set` and the split feeds it the right parent+leaf, the write
  lands on the actual `List<step>` element and identity holds.

Open question for you: should the segment-aware split live in `variable.list.Set` (the one write
entry), or does a value own "write at this path" as a single verb the way `GetChild` owns "read at
this path"? The read side kept per-hop `Descend` under a `Navigate` walker; the write side currently
has no walker — it hand-splits. Unifying them (a `Navigate`-for-write) may be the honest shape, but
that's a bigger move than this one regression needs.

## Scope note

184 real failures total (clean build; stale binaries were reporting 239 — the goal-read fixes
recovered ~55 Modules/Runtime tests once rebuilt). Pre-item-drop baseline was 129, so ~55 item-drop
tail remains, spread across Modules (57), Runtime (49), Types (23), Wire (19), Data (36→ this is the
last Data one). The `GetActions returns null` cluster in Modules looks like a separate action-discovery
root (the ClrJsonActionsArray path) — I'll scope that separately once you're happy with the write shape.
