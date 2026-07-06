# The key realization — an action param IS a setting (the whole branch in one line)

**Settled with Ingi, this branch.** This is the load-bearing insight; everything else follows.

## The collapse

There is no separate "config", no "policy", no "option", no per-module resolver. **A settable
value is one concept — a setting — resolved across scopes, tightest wins:**

```
step / action    - multiply %a% %b%, precision=decimal      ← innermost scope (the action param)
goal             set %!math.precision% = decimal
app              --math={precision:decimal}  /  ... on app
default          the [Default] on the param
```

**An action parameter is simply the innermost (per-action) scope of a setting.** "Lifetime" was
a red herring — per-app / per-goal / per-action are just *scopes* of the same thing. The step
value isn't an "override" of a setting; it's the setting at its tightest scope.

## Therefore: the generator seam is THE mechanism (not per-module code)

Every settable action param resolves by ONE cascade the generator bakes:

```
step value  →  %!module.action.param%  (action key)  →  %!module.param%  (module key)  →  [Default]
```

- `http`'s `Timeout`, `llm`'s `Model`, `math`'s `Overflow`/`Precision` are **all the same thing** —
  action properties resolved by this cascade. Math is not special.
- The **module key** (`math.overflow`) is the shared fallback so one `%!math.overflow%` covers
  `add`/`subtract`/`multiply`/… without setting each action key.
- **No hand-written resolver per module.** `MathPolicy.Resolve` / `Default.Policy` were me
  hand-rolling what the seam does for everyone — that's the anti-pattern this kills.
- **No carrier/bundle types.** `NumberPolicy` (overflow+precision struct) dies — the arithmetic
  ops take the two resolved values directly; each math action reads its own two properties.

## Consequences / worklist

1. **Build the generator seam** — insert the cascade layer into the three typed param branches
   (`Emission/Property/Data/this.cs`: `IsNullable` / `DefaultValue` / `else`; skip `IsPlainData`).
   Keys baked from `module + action + param`. This is the core deliverable now.
2. **`NumberPolicy` struct dies** (`app.type.number`) — arithmetic ops take `overflow` + `precision`
   directly; math actions read their own resolved `Overflow`/`Precision` params. Bounded refactor
   of the arithmetic signatures.
3. **`MathPolicy` / `Default.Policy` die** — replaced by the seam + the two action props.
4. **`P` prefix on `POverflow`/`PPrecision` is noise** — use `OverflowMode`/`PrecisionMode`
   (or `number.Overflow`/`number.Precision`).

## Divergence from the architect's plan — FLAG BACK

The architect's plan (§number caveat) said number policy is **module-level only, read directly,
NOT a per-action `[Default]`** — reasoning "no single owning action; add/subtract share the knob."
**Ingi's model is the opposite and cleaner:** *every* action owns the property (`Overflow`/
`Precision` on each), and the module key (`math.overflow`) is the shared fallback via the cascade.
So math is NOT a special module-level-only case — it's the ordinary action-param cascade with a
module-key fallback. Loop this back to the architect; it simplifies the plan (one mechanism, zero
special cases).
