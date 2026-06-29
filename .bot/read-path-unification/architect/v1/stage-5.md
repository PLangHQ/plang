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

## Shipped (partial) + precise remaining

**Landed (green with WireLocal still present — prerequisites for the deletion):**
- ✅ **Data-reader leniency** — an untyped string value rides as `text` (a `%ref%`/literal; text is the
  fallback) instead of the strict `no declared type` throw. Cleared that error class.
- ✅ **Variable born-eager** — the data reader borns a `type:variable` value EAGER (resolved to the
  Variable via the variable reader), not a deferred source. WHY: the name-slot read path
  `Data.Value<variable>()` (data/this.cs:607) takes the reference off `Peek()` **without opening the
  door**, so the Variable must already be there; a deferred source `Peek()`s as a source →
  `Value<variable>` opens the door → materializes a typed-null → `variable.Create` declines
  `CreateVariableDeclined`. Mirrors the old Judge resolve. (Verified the branch fires + resolves:
  born type=variable, name=greeting/content/… via a diag throw.)

**Still red when WireLocal is deleted — the remaining work (3 distinct causes, traced):**
1. **A SECOND variable load path bypasses the data reader.** The `%Name%` set-targets in StartGoal/
   RunGoalAsync (name, newVarName) reach `variable.set` EXECUTION and fail `CreateVariableDeclined` —
   they never hit the data-reader born-eager (no diag throw fired for them, unlike greeting/content).
   Another path builds a `type:variable` Data without resolving it. **Find it; apply the same resolve.**
2. **`path` `%ref%` → `CreateItemDeclined`** — a `%path%` value resolves to a text, then
   `path.Create(text)` declines (path.Create doesn't convert text→path the way Build's Convert does).
3. **NRE in Defaults** — `Object reference not set` on the defaults-resolution path; untraced.

Plus the **Wire** suite's `MalformedWireBytes_TruncatedJson_RaisesTypedChannelError` (1) — a truncated
read raises a different error shape without WireLocal's tolerant default.

Ingi: "maybe remove some of those tests" — once the load paths are unified, re-check whether any of the
15 assert the old context-less/WireLocal behavior and should be deleted rather than fixed.
