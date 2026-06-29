# Stage 5 — delete the dead from-raw machinery (subtractive)

**Goal:** remove what no longer has a live caller. The from-raw eager route and its scaffolding die; the convert-a-built-value op (case 2b) **survives**.

**Kind:** subtractive. Every deletion below must be preceded by a grep proving zero live callers (the dead-caller proof). No rebase gate — delete freely against `3ddcdb17f` (merge order settled).

**Depends on:** Stages 3–4 (ctor + `set` + `validateResponse` no longer call `Build`/`Judge` for construction).

---

## What dies

| Target | File | Proof before cutting |
|---|---|---|
| **`Judge(item.@this)`** | `type/this.cs:538` | grep `\.Judge(` → only the (now-rewritten) ctor/`Declare`, which no longer call it. Zero live callers. |
| **`Deserialize(object?)`** | `type/this.cs:516` | already zero callers today (labelled "Replaces Judge" but unused). Confirm grep `\.Deserialize(` is empty, sweep. |
| **`Build`'s from-raw scaffolding** | `type/this.cs:232` | the throwaway-`text` lift + the Variable (`IRawNameResolvable`) and `%ref%`-template special-cases (now `source`'s job, Stage 2). The **core** — apply the family hook to a built value — survives as case 2b's `Convert(item)`. See "gut, not delete" below. |
| **from-raw route into `Convert(object?, ctx)`** | `type/this.cs:177` | the raw-CLR `TryConvert` tail + the `null` arm the ctor now owns. The **method survives** as case 2b's engine (built-leaf → family hook). Thin it; do not delete. |
| **`source` context-less string fallback** | `source.cs:120-129` (the `if (_value is string s)` branch) | construction now always carries context (born-with-context), so the fallback's last reason to exist is gone. Confirm no context-less `source` births remain. |

---

## "Gut, not delete" — `Build` and the 2-arg `Convert`

The plan is explicit: **the behavior survives, the from-raw route into it dies.**

- **`Build(object?)`** — if, after Stage 3/4, `Build` has zero callers, delete the method outright; its surviving core already lives in case 2b's `Convert(item)` (introduced Stage 2). If a caller remains that genuinely needs "make a value of this type from a built item", it should call `Convert(item)` — migrate it, then delete `Build`. Do **not** keep `Build` as a synonym.
- **`Convert(object?, ctx)`** — keep it (or its `Convert(item.@this)` overload from Stage 2) as case 2b's engine. Remove only the parts no longer reached: the raw-CLR `TryConvert(value, target)` tail (that was the from-raw eager route — construction no longer enters here) and the `null` arm (the ctor's case 1 owns typed-null). Verify `catalog/Conversion.cs:231`'s marshalling use of `Conversions.Of` is unaffected — that is a *different* job (PLang value → C# parameter) and **stays** (see Stays-list).

---

## Stays-list — looks dead, isn't (do NOT delete)

- `Create(object?, context)` (`type:372`) — general CLR→plang lift; read path + every list/dict slot.
- the per-type static `Convert(raw, kind, ctx)` **hooks** (number/text/date/…) — the destination leaves; the reader and case 2b both reach them.
- the **case-2b op** (`Convert(item)` / thinned `Convert`) — re-types `text "5"` → `number 5` for Declare/validateResponse/`set`-type-differs. The §7 correction; behavior stays.
- `convert.OwnerOf` + ownership table + `OwnedClr` (`convert/this.cs:84+`) — CLR-interop seam.
- `convert.OfStatic` + `_staticCache` + `Discover` (`convert/this.cs:43,133`) — `Create`'s raw-scalar lift + context-free deserialize.
- `convert.Conversions.Of` (instance) + `_cache` — survives: `catalog/Conversion.cs:231` marshals a PLang value into a specific C# type at the action boundary. Different job; off the construction path now, but not dead. **Do not fold marshalling onto the reader table here** — separate cleanup.
- the born-typed holder ctor (`data/this.cs:222`) — the from-raw arm delegates into it.
- `source` + the reader registry — the surviving door.

---

## Exit criteria

- [ ] `Judge`, `Deserialize` deleted; `Build` deleted or migrated-then-deleted (no synonym left).
- [ ] `Convert(object?, ctx)` thinned to case-2b's engine (from-raw `TryConvert` tail + `null` arm removed); `Convert(item)` is the construction caller's entry.
- [ ] `source.cs:120-129` context-less string fallback removed; no context-less `source` births remain.
- [ ] Grep proofs recorded for each deletion (zero live callers before cut).
- [ ] `catalog/Conversion.cs:231` marshalling still compiles + its tests pass (the `Conversions.Of` survivor).
- [ ] Global exit gates green.

## What must NOT happen

- Do not delete the per-type `Convert` hooks, `Create`, `Conversions.Of`, or the case-2b op.
- Do not keep `Build` as an alias/synonym for `Convert(item)` — migrate callers, then delete.
- Do not bundle the marshalling-onto-reader-table cleanup into this stage.
- Do not cut anything without its zero-caller grep proof in the commit.
