# plang build — crash-chain findings (for architect)

**Branch:** `fix-plang-build` (off `context-never-null`).
**Why this branch:** `plang build` does not complete — it crashes, and after each fix the
*next* crash surfaces. Rather than bloat `context-never-null`, we walk the chain here.
Decision (Ingi): there will likely be several of these before a full build runs.

**Baseline fact:** on the pre-session tip (`8b6d271fe`) `plang build` already crashed at
crash #1 below — so these are pre-existing WIP-branch breakage, NOT regressions from the
context-never-null / canonicalization session. The session's work (Stage 3/4/6, signing
canonicalization) is sound and committed on `context-never-null`; it's just that `plang build`
never got far enough to exercise it.

---

## Crash #1 — `step.Disabled` NRE at goal load  ✅ FIXED (commit on this branch)

**Trace**
```
NRE at app.goal.steps.step.this.Disabled(context)        step/this.cs:27  (context.Get<bool>(...))
  app.goal.steps.this.GetEnumerator()+MoveNext()         steps/this.cs:54 (step.Disabled(Context))
  app.goal.GoalCall.LoadFromFile(...)                    GoalCall.cs:321  (foreach goal.Steps)
  GoalCall.GetGoalAsync → App.RunGoalAsync → builder.RunAsync → App.Start
```

**Root cause.** `LoadFromFile` deserializes the goal tree (STJ) — which leaves
`steps.@this.Context` (`= null!`) unset — then `foreach (goal.Steps)` to wire back-refs.
The Steps **enumerator** reads `.Context` to check per-execution `Disabled` state. At load
there is no execution context yet → `Context` null → NRE. `step.Disabled` is *per-execution*
state keyed by the running context; it has no business being consulted at load.

**Fix (root, not a `!= null` guard).** Born the step collections' context at the `.pr`-load
seam, alongside `goal.App`: `goal.Steps.Context = context` (root + each sub-goal). The tree
is born with the read context — never null.

**Design note for architect.** This is the deeper smell the branch is about: the goal tree is
deserialized **context-less** and back-refs (`App`, `Step.Goal`, now `Context`) are stamped
*after* in `LoadFromFile`. That's construct-then-stamp at the tree level. A cleaner shape:
the **goal reader** (`goal/serializer/Reader.cs`, which holds `ctx.Context`) borns the tree
with context, OR the goal carries a `Context` whose `Steps`/`Goals` getters cascade it (the
getters already cascade `Goal`/`Parent` — `goal/this.cs:51`, `:58`). Worth deciding the single
owner of "wire the deserialized goal tree."

---

## Crash #2 — `text.Value` infinite recursion (StackOverflow)  ← CURRENT

**Trace (≈6680 repeating frames)**
```
app.type.text.this.Value(data)        text/this.cs:72
  → context.Variable.Get(varName)     text/this.cs:79   (full %var% match)
  → resolved.Value()                  text/this.cs:87
  → app.data.this.Value()
  → app.type.text.this.Value(data)    (loops)
...
  app.callstack.call.this.ExecuteAsync → action.RunAsync → step.RunAsync → Steps.RunAsync
```

**Root cause.** `text.Value` for a stamped template that is a *whole* `%x%` reference does:
```
var resolved = await context.Variable.Get(varName);   // the value of %x%
return await resolved.Value();                        // materialize it
```
If `%x%`'s stored value is itself the stamped template `%x%` (a self-reference, or a cycle
`%x%`→`%y%`→`%x%`), `resolved.Value()` re-enters `text.Value` with the same template forever →
StackOverflow. There is **no cycle/self-reference guard** in the resolve path.

**Open questions for architect (need a decision):**
1. **Is the self-reference legitimate input, or a setup bug?** During `plang build` some
   variable resolves to a template naming itself. Need to find *which* variable and whether
   the builder/`%!...%` infra is creating a self-referential stamp (e.g. a reserved var seeded
   with its own `%name%`), vs. a genuine author cycle.
2. **Fix location.** Options:
   - Cycle/self-ref guard in `text.Value` / `Variable.Resolve` (a resolving-set or depth cap;
     a `%x%` whose resolution reaches `%x%` again → typed error, not stack overflow).
   - Fix at the source so a variable is never stamped with a template referencing its own name.
   - `Variable.Get(varName)` returning the *same* binding it is resolving for → short-circuit.

   Leaning toward: a guard is needed regardless (a stack overflow on cyclic input is a
   DoS/robustness hole, like the wire `MaxReadDepth` cap), *and* root-cause the specific
   self-ref the builder produces.

---

## Pattern / expectation

Both crashes are **context/resolution plumbing exposed only once a full goal actually loads
and runs during build** — the unit suites don't drive `plang build`'s goal-execution path, so
they stayed green while this path was broken. Expect more of this shape (context-null at a
not-yet-wired seam; resolution cycles; deferred-source materialization) as the chain unwinds.
Each will be fixed root-first (set/​born the value, not `!= null`) on this branch until
`plang build` completes end-to-end.
