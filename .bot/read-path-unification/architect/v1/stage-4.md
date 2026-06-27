# Stage 4 — finish context-never-null for reads

**Design authority:** `plan.md` "Phase 4". Stub — **firm up when Stage 3 is green + pushed.**

## Entry
- Stage 3 green + pushed.

## Exit
- `WireLocal` + both `[JsonConverter(typeof(WireLocal))]` deleted; the `_context==null` fail-closed branch + tripwire gone; `Wire._context` structurally non-null.
- The `signature` reader verifies with the actor in scope. Build + both suites green.

## Dies / Stays
- See `plan.md` Phase 4 — populate + re-verify line numbers when this stage starts.

## Shipped + deltas from plan
_(coder fills.)_
