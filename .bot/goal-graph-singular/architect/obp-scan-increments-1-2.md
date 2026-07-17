# OBP scan — graph→items increments 1-2 (Ingi asked, 2026-07-17)

Scanned the production diff `be6bbf294^..HEAD` (the four item faces: `goal/step/action/this.Item.cs` + `modifier` Type). Everything is honestly marked TRANSITIONAL — the scan is about what must NOT survive the transition as-is, not about the scaffolding being wrong to exist.

## Clean (the pattern working — don't touch)

- Each item **names its own type** (`Type => new("goal", typeof(@this))`, …) — kills NamespaceTail free-riding for the graph (a standing todo, closed for free).
- `IsLeaf => false` correct; `modifier` gets its own `"modifier"` Type (role is the type).
- `Set`'s `value.Clr(prop.PropertyType)` is the SANCTIONED crossing — writing into a host's C# property slot; the value lowers ITSELF, once, at the edge (the crossing test). Not a leak.
- `return this` (mutate-in-place) is within the `Set` contract ("possibly replaced value; caller rebinds if the instance differs").

## Finding 1 — triplicated `Set`; one missing shared home (the meta-test fires)

`goal.Set`, `step.Set`, `action.Set` are **byte-identical bodies** (Data-unwrap → `GetProperty(IgnoreCase)` → `Clr`-lower if mismatched → `SetValue` → `return this`). Three copies of one behavior = one missing type/method — *"one line of choreography needing edits in three files."*

The clean home is already half-built: the base `item.Get` DEFAULT reflects child-READ through the clr carrier (`new clr.@this(this, parent.Context).Get(...)`, `item/this.cs:141`), and the clr carrier already has a `Set` (`clr/this.cs:110`). Child-WRITE is the exact symmetric operation → it should route the same way, so **no graph item overrides `Set`**. The real missing piece (why coder overrode instead) is that base `Set(key, isIndex, value)` has **no `context` param** where `Get(parent, key)` gets `parent.Context` — the clr carrier needs a context to construct. That param gap is the one thing to fix; fixing it deletes all three overrides.

**Ruling:** when increment 3 resolves the transition, the reflective child-write lands in ONE home (base default routing to the clr carrier, symmetric with `Get`; solve the context-param gap), never three copies. If the items get native child-write instead (no reflection), that's also one home — the requirement is single-home, not the mechanism.

## Finding 2 — triplicated delegating `Output` (dies in increment 3, don't re-home)

`goal/step/action.Output` are the same one-liner (`new reflection(ctx).Output(this,…)`). Same three-copy shape, but this one is EXPLICITLY replaced by the explicit token `Write` in increment 3. **Do not extract/share it transitionally** — just let increment 3 delete all three. Flagging only so it's not mistaken for a keeper.

## Finding 3 — `modifier.Order` shadows `item.Order` (coder already flagged; reinforced)

`modifier.Order` (nesting-depth NOUN) collides with the base `item.Order(@this)` comparison VERB → CS0108. A noun named for a verb is the smell twice over. Rename to a non-verb noun (`Depth`/`Nesting` — coder's `Depth` lean is fine) when `modifiers.Sort`/`RunAsync` re-home onto `action`. Don't ship `Order` on modifier.

## Net

Shape is sound; the two triplications are transitional scaffolding whose ONLY requirement is **collapse to one home (Finding 1) or delete outright (Finding 2) at increment 3** — do not let three copies ship. `modifier.Order` renames. Nothing here blocks increment 3; these are its exit criteria.
