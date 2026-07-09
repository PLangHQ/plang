# Coder review — clean-rewrite plan (`.pr` graph as hosts, sync Create), a2de7e6a1

Traced the new load-bearing claims. The rewrite is a real improvement: sync `Create`
(kills the async-prep ripple I flagged in v2), and `.pr`-graph-as-clr-hosts leverages
existing machinery instead of a bespoke navigate-pull builder. Blocker fix verified
coherent: `clr.Clr` → `ClrConvert` terminal-throws today (`item/this.cs:345`, *"the
type must own this Clr projection"*); routing through `Kind.Clr` makes the json kind
own it. Earlier v2 items #1/#3 (async) are resolved by the sync decision; #4 (identity
runtime-hot) is now folded into Stage 3.

**Checked and cleared — not issues:**
- `[Store]` coverage is complete on the host classes (goal 16/16, step 11 + 5
  `[JsonIgnore]`, action 5 + 19 `[JsonIgnore]`). Tagged's tag-aware gate + `[JsonIgnore]`
  exclusion (`Tagged.cs:64,92`) already model the `.pr` set. No annotation pass needed.
- The design reversal (navigate-pull record builder → clr hosts) is sound; the
  item ⟺ ICreate / no-ICreate ⟺ host line (model #1) is a clean divider and reuses
  Output's `Tagged.PropertiesFor` loop. (Note: `from-source-spec` and my
  `project_navigation_driven_record_builder` memory are now stale — will update.)

## Issues / comments

### 1. Widen the Stage-1 DoD from "param round-trip" to "full goal-graph round-trip"
The plan's DoD is a *param* sign-identical round-trip. That covers the Data-leaf seam
but **not** the risk that actually bites: the reflection `Read` is a full
re-implementation of what `JsonSerializer.Deserialize<goal>` did, and its failure mode
is **silent field drift** — a nested host, a collection element type, a default, a null,
a `[JsonPropertyName]` casing that STJ carried and reflection-Read drops. This is the
`born_native_fromwire_completeness` shape (FromWire once silently dropped `modifiers`).
DoD should be: **read the same `.pr` via STJ and via the new reflection path, assert
structural equality across the whole goal→steps→actions→params/modifiers graph** — not
one param. Cheapest insurance against the one class of bug this stage can introduce.

### 2. `*`-kind `Read`/`Set`/`Kind.Clr` is a format-agnostic deserializer, not a "thin mirror"
The plan frames Read/Set as "missing mirrors" of Output/Navigate. Output *writes*
(property → wire); `Read` must **reconstruct a typed host graph from json**: instantiate
`action`, populate its 5 `[Store]` props by declared type, build `List<Data>`
`Parameters` through the data reader, recurse nested hosts (`Modifiers`, `actions`),
honor defaults/nulls/element types. That's materially more than the `PropertiesFor`
loop. Not an objection — a **scope flag**: Read is the largest new surface in Stage 1,
and "mirror of Output" undersells it. Budget it as "re-derive STJ deserialize, by
reflection, format-agnostic."

### 3. Bridge-item audit is cross-stage — decide it *in* Stage 1, it moves the Stage-2 worklist
`snapshot`/`GoalCall`/`catalog/view`/`app` classification (value vs host) is listed as a
Stage-1 audit, but `GoalCall` also has a `Convert` hook the Stage-2 worklist relocates
to `GoalCall.Create`. If Stage 1 makes it a host, that Stage-2 line is moot; if it stays
an item, it isn't. So the audit isn't optional Stage-1 cleanup — its outcome edits
Stage 2. Resolve the four before closing Stage 1.

### 4. Sync `Create` + async `kind.behavior.Convert` — verify no relocated hook awaits
Sync `Create` is the right call. One guard for Stage 2: a hook being relocated
`Convert → Create` must not currently `await` (I/O). number/text/dict parse in memory —
fine. `image`'s strict check is explicitly deferred to the load seam — good. But confirm
each of the 14 relocations is genuinely in-memory before pinning `Create` sync; if one
awaits, it can't become a sync `Create` and needs the async `kind.behavior.Convert` door
instead. Cheap to check at relocation time; flag it if any hook surprises.

### 5. `goal.getTypes` List-lower — confirm it's the *same* root, not a sibling patch
Stage 2 defers the `Data<list<dict>>` native-`List`→terminal-LOWER blocker to "resolves
when construction routes through Create." Confirm it's the **same** `clr.Clr` terminal
throw (`item/this.cs:345`) reached from a different path, fixed by the **same** `Kind.Clr`
delegation — not a second, separate lowering fix. If it's the same root, say so; if it's
a distinct native-`List` path, it needs its own line, not a hand-wave.

---

Nothing here blocks Stage 0/1. #1 (widen the DoD) is the one I'd bake in before writing
Stage 1 code — it's the guardrail for the only silent-failure class this rewrite adds.
