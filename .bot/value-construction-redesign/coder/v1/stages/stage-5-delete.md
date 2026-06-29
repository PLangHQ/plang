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
| **`Build` leftover scaffolding** | `type/this.cs:232` | `Build` itself is **KEPT** (reimplemented in Stage 3 as the one construction entry: raw → `source`, built → hold/convert). Stage 3 already replaced the throwaway-`text` lift with `source` and moved the Variable/`%ref%` cases to `source`. Stage 5 only sweeps any *leftover* dead lines inside `Build` that Stage 3 left behind. Do **not** delete `Build`. See "thin, don't delete" below. |
| **from-raw route into `Convert(object?, ctx)`** | `type/this.cs:177` | the raw-CLR `TryConvert` tail + the `null` arm the ctor now owns. The **method survives** as case 2b's engine (built-leaf → family hook). Thin it; do not delete. |
| **`source` context-less string fallback** | `source.cs:120-129` (the `if (_value is string s)` branch) | construction now always carries context (born-with-context), so the fallback's last reason to exist is gone. Confirm no context-less `source` births remain. |

---

## "Thin, don't delete" — `Build` and the 2-arg `Convert`

**The behavior survives; only the from-raw eager *route* dies.**

- **`Build(object?)`** — **KEPT** (Ingi: keep the name). It is the one construction entry, reimplemented in Stage 3 (raw → `source`, built → 2a hold / 2b `Convert`, null → typed-absence). The ctor and `Declare` both delegate to it. Stage 5 does **not** delete `Build`; it only removes any dead lines Stage 3's reimplementation orphaned (e.g. an unreachable branch left in place). Confirm `Build` no longer contains the throwaway-`text` lift or reflection.
- **`Convert(object?, ctx)`** — keep it (or its `Convert(item.@this)` overload from Stage 2) as case 2b's engine, called from inside `Build`'s built-but-different branch. Remove only the parts no longer reached: the raw-CLR `TryConvert(value, target)` tail (that was the from-raw eager route — construction no longer enters here) and the `null` arm (the ctor's case 1 owns typed-null). Verify `catalog/Conversion.cs:231`'s marshalling use of `Conversions.Of` is unaffected — that is a *different* job (PLang value → C# parameter) and **stays** (see Stays-list).

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

- [ ] `Judge`, `Deserialize` deleted (zero callers). `Build` **kept** — confirm its throwaway-`text` lift + reflection are gone (Stage 3) and no dead branch lingers.
- [ ] `Convert(object?, ctx)` thinned to case-2b's engine (from-raw `TryConvert` tail + `null` arm removed); `Convert(item)` is `Build`'s built-but-different branch.
- [ ] `source.cs:120-129` context-less string fallback removed; no context-less `source` births remain.
- [ ] Grep proofs recorded for each deletion (zero live callers before cut).
- [ ] `catalog/Conversion.cs:231` marshalling still compiles + its tests pass (the `Conversions.Of` survivor).
- [ ] Global exit gates green.

## What must NOT happen

- Do not delete the per-type `Convert` hooks, `Create`, `Conversions.Of`, the case-2b op, or `Build`.
- Do not bundle the marshalling-onto-reader-table cleanup into this stage.
- Do not cut anything without its zero-caller grep proof in the commit.
