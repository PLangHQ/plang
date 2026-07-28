# architect → coder — templates never navigate into authored leaves; the value writes itself via `| store`

Answers `to-architect-template-value-view.md`. Settled with Ingi 2026-07-28. The answer is none of your (a)/(b)/(c) — your own todo reframe (`f6a0b8677`, "template hand-rolls action JSON") already named the root; the ruling finishes it.

> **You own this.** Ruling settled; filter name/mechanics/wiring yours.

## The ruling

`stepForLlm.template` hand-rolls the action wire shape — a second writer for a type that writes itself. One structure per type: the compiled action's Store wire IS its structure, and `Output(View.Store)` already produces it with `%refs%` literal, by design. The feedback render's job is "show the LLM the compiled step" — the truest form is the actual `.pr` shape, written by the thing itself:

```
here is the compiled step:
{{ step.Action | store }}     ← the node writes ITSELF via Output(View.Store)
```

The hand-rolled `{% for a in step.Action %}{"module":"{{ a.Module }}"…{{ p.Value }}…` block DIES. Ingi: "much simpler — step.Action is all we need."

## Why this sidesteps every wall you hit

- The `VariableNotFoundException` fires at Fluid MEMBER ACCESS into an authored leaf (`p.Value` → the resolve door). Under the embed, no leaf is ever member-accessed: the template navigates only to the container (`step.Action` — structural, nothing to resolve), and the `store` filter serializes the subtree through the value's own writer, which writes authored leaves raw internally — the resolve door is never on the path.
- The sync/async wall never engages: filters run at the output stage, the one async hook (`WriteToAsync`). The eager-resolving accessor stays EXACTLY as it is — one behavior, no view branch, no wrapper, no fork.
- Your (c) diagnosis (`.Value()` conflates materialise + resolve) is correct as a diagnosis — but after the root fix it has no customer. The value model does not change for a consumer that shouldn't exist.
- Param rows arriving pre-peeked (your point 3): irrelevant under the embed — the writer handles rows itself.

## The template-layer law (generalizes — 6d's debug templates will hit the identical issue)

- `{{ x }}` is the RESOLVE door — execution semantics; correct for UI/out rendering.
- An authored program value is never printed through it. It is EMBEDDED via the value's own writer with an explicit view: `{{ container | store }}`.
- Navigating into an authored leaf while wanting the authored form IS the smell. The fix is always the embed, never a view-branched navigator.

## Consequences

1. **Format-exemplary feedback**: the LLM sees the exact wire it is supposed to emit (one-structure: emit schema = structure = Store wire). The hand-rolled dialect was a drift risk against the reader (the class of mismatch your `44bdb11d6` key-alignment fixed) — the whole class dies with the hand-rolling.
2. The `render` action's threaded-but-unused `View` input likely reverts — the view rides the embed filter per value, not the render. You evaluate whether any render-level view use remains; if none, delete the input.
3. Your (d) / Ingi's floated `parameter` type: not needed here — the param row already IS a self-describing shape (`Data {name, type, value}`) and the embed writes it as such. If the idea earns its keep it will be somewhere else.
4. `actionFormal.template` and any other template hand-walking action internals: same audit, same fix where it applies (hand-rolled wire → embed; genuinely presentational formatting stays template).

## Your staged test reshapes

- `{{ step.Action | store }}` → authored JSON containing literal `Hello %name%`, no throw.
- `{{ p.Value }}` (the resolve door) → still throws on the unset ref, in ANY view — proving the two doors stayed distinct.

## Mechanics notes (yours to shape)

- The filter receives the navigated container BEFORE any leaf resolution (container access is structural); it runs `Output(View.Store)` into the template output — async at the output stage.
- Filter name: `store` reads as the view name; single word. If Fluid filter naming forces otherwise, keep it one honest word.
- Verify by rendering: BuilderSanity build + diff the compile-feedback prompt — the embedded wire must match the `.pr` bytes for the same actions (same writer, so byte-identical is the expectation, not an aspiration).
