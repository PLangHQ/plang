# Stage 5 — fixtures + the 15

**Design authority:** `plan.md` "Phase 5". Firmed up — done together with Stage 4 (coupled push).

## Entry
- Stage 4 work underway (4+5 is one push — WireLocal removal regresses the 15).

## The crux (plan.md:8) — Build vs Judge parity
The 15 regress because the `Data` ctor **forks on context-presence**: `_context != null` → `type.Build`
(eager), else → `type.Judge` (deferred). Context-never-null kills the `Judge` arm — **but `Judge` is the
only one that handles `path` / `%ref%`**. So forcing `Build` breaks path/ref reads → the 15 fail.

**Fix order:** bring `type.Build` to parity with `type.Judge` (path + `%ref%`/variable-target handling)
FIRST. Once `Build` does everything `Judge` does, the `else Judge` arm is dead and safe to delete, and
WireLocal can go — the 15 pass because `Build` is correct, not because a branch was silenced.

## Exit
- `type.Build` handles `path`/`%ref%` (parity with `Judge`); `type.Judge` + the ctor/`Declare`
  `else Judge` arms deleted.
- Fixtures swept to born-with-context (so the always-`Build` path has a context).
- The 15 (`RunGoalAsync_*`, `ResolveValue_*`, `FilePaths_*`, `Defaults_*`) pass on a correct read.
  Full suite green.

## Shipped + deltas from plan
_(coder fills: Build parity changes, which fixtures changed, that the 15 pass on a correct read.)_
