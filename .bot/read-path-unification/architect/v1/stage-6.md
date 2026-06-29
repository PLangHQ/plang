# Stage 6 (LAST) — retire the value-ctor + delete Build/Judge  *(scope TBD)*

**Design authority:** `plan.md` "Phase 6 (LAST)". Stub — **firm up when Stage 5 is green + pushed, AND Ingi has made the scope call (Q4).**

## Entry
- Stage 5 green + pushed.
- **Ingi's scope decision:** retire the value-ctor fully in this branch, or split to a follow-on? (Needs the no-type `new Data(name, value)` call-site count — uncounted.) Do not start until decided.

## Exit (if in scope)
- Value-ctor `(name, value, type)` + its `Build`/`Judge` fork, `type.Build`, `type.Judge`, `Data.Declare`'s fork retired; every `new Data(name, value[, type])` site → holder ctor or `Data.From`. Build + both suites green.

## Dies / Stays
- See `plan.md` Phase 6 — populate + re-verify line numbers, gated on the scope decision.

## Shipped + deltas from plan
_(coder fills.)_
