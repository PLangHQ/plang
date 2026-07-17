# Modifier wrap — ownership + naming fix (settled with Ingi 2026-07-17)

Follow-on to `obp-scan-increments-1-2.md`. The modifier-collection deletion re-homed the wrap fold onto `action.RunModifiers`; scanning it surfaced a deeper ownership smell Ingi confirmed. Traced against HEAD.

> **You own this.** The shape is settled; bodies/factoring yours.

## The finding — a modifier responsibility sits on `action`

`WrapAround` is defined on `action.@this` (base, ~`action/this.cs:251`) but **only ever called on modifiers** (`Modifiers[i].WrapAround(...)`, ~`:194`). It resolves the handler, verifies `IModifier`, and delegates to `handler.Wrap(next, context)`. A plain action never wraps anything — only a modifier does. So a modifier-only method is misplaced on the action base. (Ingi: "isn't it the job of modifier to wrap itself around the action — the owner owns its responsibility?")

## The fix (three moves)

1. **`WrapAround` → `modifier.Wrap`.** Move the method off `action`, onto `modifier`, rename to `Wrap`. `modifier.Wrap(inner, context)` — the modifier wraps an inner execution in itself (resolve its handler, `IModifier` check, delegate to `IModifier.Wrap`). The modifier now owns wrapping. Kills the `WrapAround` verb+noun. Safe: the only caller passes a `modifier`, so nothing calls it on a plain action.

2. **`RunModifiers` vanishes — inline the fold into `RunAsync`.** It has ONE caller (`RunAsync`); by the extract-only-when-shared rule a single-caller helper inlines. The fold loop (right-to-left over `Modifiers`, calling `Modifiers[i].Wrap(...)`, then the AfterAction coverage fire) goes back into `RunAsync`. No `RunModifiers`, no separate `action.Wrap` (which would also collide with `modifier.Wrap` under `modifier : action`). The action composes its modifiers inline — its own job, it owns the `Modifiers` list and the dispatch.

3. **`IModifier.Wrap`** (the handler's actual wrapping) is already a clean single verb — **unchanged**. `modifier.Wrap` resolves its handler and delegates to `IModifier.Wrap`, the same pattern as `action.Dispatch` → `handler.Run`. Two layers, both honestly "wrap."

## After

- `modifier.Wrap(inner, context)` — the per-modifier wrap (owns its responsibility).
- `RunAsync` — folds `Modifiers` around `DispatchAsync` inline (the fold + AfterAction fire).
- `WrapAround`, `RunModifiers` — gone.

## Verify

- The wrap ORDER preserved: right-to-left, lowest `Position` outermost (the slot is pre-sorted by `Nest`).
- AfterAction fires once per modifier, carrying the chain result — unchanged behavior.
- `on error` / `cache` / `timeout` modifier goals still wrap correctly (a modifier-bearing Sanity goal, run green).
- Empty `Modifiers` → the bare inner run, no fold.

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `modifier.Wrap` | the modifier owns wrapping itself; no modifier-logic on the action base | ok |
| fold inlined in `RunAsync` | single-caller helper inlined; action composes its own modifiers | ok |
| names | `Wrap` (one verb) replaces `WrapAround`/`RunModifiers` (verb+noun ×2) | ok |
