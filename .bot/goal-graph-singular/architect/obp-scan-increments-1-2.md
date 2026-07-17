# OBP scan — graph→items increments 1-2 (Ingi asked, 2026-07-17)

Scanned the production diff `be6bbf294^..HEAD` (the four item faces: `goal/step/action/this.Item.cs` + `modifier` Type). Everything is honestly marked TRANSITIONAL — the scan is about what must NOT survive the transition as-is, not about the scaffolding being wrong to exist.

## Clean (the pattern working — don't touch)

- Each item **names its own type** (`Type => new("goal", typeof(@this))`, …) — kills NamespaceTail free-riding for the graph (a standing todo, closed for free).
- `IsLeaf => false` correct; `modifier` gets its own `"modifier"` Type (role is the type).
- `Set`'s `value.Clr(prop.PropertyType)` is the SANCTIONED crossing — writing into a host's C# property slot; the value lowers ITSELF, once, at the edge (the crossing test). Not a leak.
- `return this` (mutate-in-place) is within the `Set` contract ("possibly replaced value; caller rebinds if the instance differs").

## Finding 1 — triplicated `Set` → SETTLED: base `Set` reflects by default (Ingi, 2026-07-17)

`goal.Set`, `step.Set`, `action.Set` are **byte-identical** (Data-unwrap → `GetProperty(IgnoreCase)` → `value.Clr(propType)` if mismatched → `SetValue` → `return this`). Three copies of one behavior = one missing home.

**Correction to my first scan: `Set` does NOT need a context.** The body is context-free (`await dv.Value()` uses the Data's OWN context; `Clr`/`SetValue` take none). I wrongly said "needs context" — that was only true for a clr-carrier route I proposed for symmetry; that route is unnecessary. Drop it.

The real asymmetry is in the base:

```
Get(parent, key)     default REFLECTS (new clr.@this(this, parent.Context).Get(...))  — child-read works for every item
Set(key, …, value)   default THROWS   (NotSupportedException)                          — every host must override → the 3 copies
```

Child-read reflects by default and just works (leaves have no navigable child; containers override; hosts reflect). Child-write is the mirror and should behave the same. **Fix: move coder's exact `Set` body onto the base `item.Set` default (replacing the throw).** Then:
- goal/step/action drop all three overrides — 3 → 0, no new plumbing, no context.
- Leaves (`text`/`number`): `GetProperty` finds no writable member → throws "no writable property" — same outcome as today's throw, from the shared path.
- Containers (`dict`/`list`): keep their key/index `Set` overrides — untouched.

One check before committing: nothing must depend on `Set` THROWING by default (a test asserting the exact message, a caller catching `NotSupportedException` to mean "leaf"). The base default still throws when `GetProperty` returns null, so behavior holds; only the message string changes — verify no assertion pins it.

## Finding 2 — triplicated delegating `Output` (dies in increment 3, don't re-home)

`goal/step/action.Output` are the same one-liner (`new reflection(ctx).Output(this,…)`). Same three-copy shape, but this one is EXPLICITLY replaced by the explicit token `Write` in increment 3. **Do not extract/share it transitionally** — just let increment 3 delete all three. Flagging only so it's not mistaken for a keeper.

## Finding 3 — `modifier.Order` shadows `item.Order` (coder already flagged; reinforced)

`modifier.Order` (nesting-depth NOUN) collides with the base `item.Order(@this)` comparison VERB → CS0108. A noun named for a verb is the smell twice over. **SETTLED (Ingi): rename to `Position`** — a linear wrap-precedence (lower = outermost; no tree, so `Depth` was the wrong axis; `Rank` is taken by the compare precedence). Rename the property + its 3 sites (step Clone, action mint, `Sort` comparator) when the fold re-homes onto `action`.

## Net

Shape is sound. Settled resolutions: **Finding 1 — base `Set` default reflects (3 overrides drop, no context); Finding 2 — the delegating `Output` triplet is deleted outright at increment 3 (not re-homed); Finding 3 — `modifier.Order` → `Position`.** Nothing blocks increment 3; these are its exit criteria.
