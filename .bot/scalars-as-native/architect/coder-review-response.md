# Architect response — coder review

All four points landed; thanks — #1 caught a real contradiction. Dispositions below; plan + stages updated to match.

## #1 — `item` and `IOrderableValue`: accepted, with a correction to *both* our framings

You're right that `item` must not implement `IOrderableValue` — `dict : item` is equality-only (`type-system.md`), and inheriting an order it can't honor regresses the audited contract. **Fixed:** ordering (and value-equality) stay opt-in interfaces; each type implements only what it honors, exactly as `dict`/`list` do; `Compare`'s interface dispatch routes them. `item` carries **only** truthiness + the lazy narrow.

One correction the other way, from discussion with Ingi: `item` is **not** a thin marker. It's the apex **and** the un-narrowed/lazy type — `read file.json` → `Data<item(kind=json)>`, narrows to `dict`/`list` on touch. The PLang `object` type **folds into `item`** (the un-migrated name; `(object,json)` → `(item,json)`). So `item` is thick on the *universal* contract (truthiness, narrow) but withholds ordering — that's the synthesis. Stage 1 rewritten on this basis; the Stage-1 green bar is now `dict : item` **and** `Compare.Order(dict)` still throws.

## #2 — blast radius: accepted; Ingi overruled the bare-`Data` part

The cascade naming is now in Stage 7 and the plan — the ~25 generic `Data<T>`/`Data<U>` infra methods threading `where T : item` are called out as the real cost, not the leaf swaps. `path`/`image`/`code` → `: item`.

On `Ask`/`snapshot`: **Ingi's call is everything `: item`, not bare `Data`** — keep the typed slots, make the types `: item`. It's clean precisely because `item` forces no contract (#1), so `Ask : item` writes no stub. `snapshot` is `: item` until it's deleted. `Variable` stays `: item` + `IRawNameResolvable` (its typed slot is load-bearing for name-binding, unlike `Ask`/`snapshot`).

## #3 — value-equality aliasing: accepted, with your narrowing

Added as a Stage-2 regression + coverage row: `HashSet`/list-element equality of a wrapped value vs. its raw form, not dict keys (you correctly noted dict keys are string-indexed via `_index`, so the hazard is element/value equality). The construction flip stays bounded to within each type's stage.

## #4 — accepted

- dict/list-as-`item` **pinned to Stage 1** (not floating) — and it's the proof that a non-orderable value sits under `item` cleanly.
- sync `ToBoolean()` path kept reachable for scalars; async only for I/O truthiness (`path`) — noted in the `item` contract.
- double-wrap kill promoted to a first-class **Stage-7 acceptance criterion** (`Data<data.@this>` must not compile).

Re-ground anchors against the rebased `runtime2` base when you start — the plan's file:line cites predate the merge.
