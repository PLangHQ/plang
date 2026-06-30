# plang build — crash-chain findings (for architect)

**Branch:** `fix-plang-build` (off `context-never-null`).
**Goal:** get `plang build` to run end-to-end. It crashes; each fix surfaces the next.
**Baseline:** on the pre-session tip (`8b6d271fe`) `plang build` already crashed at crash #1 —
these are pre-existing WIP breakage on the goal-load / goal-execution path, NOT regressions
from the context-never-null session. The unit suites don't drive `plang build`, so this path
stayed broken while suites were green.

**The lens.** The Stage-3 getter flip (`Data.Context` = `_context`, no `(_item as IContext)?.Context!`
fallback) is what *exposes* these: the fallback was silently papering over genuinely
context-less / uninitialized values on the build path. Each crash below is a real value that
was never properly born/initialized — the flip just makes it loud. **Fix roots (born/set the
value), never `!= null` guards or static caches.**

---

## Crash #1 — `step.Disabled` NRE at goal load  ✅ FIXED (root)

`LoadFromFile` deserializes the goal tree (STJ → `steps.@this.Context` left null), then
`foreach (goal.Steps)` to wire back-refs; the Steps enumerator reads `.Context` for
per-execution `Disabled` state → null → NRE (`steps/this.cs:54` → `step/this.cs:27`).
**Fix (root, not guard):** born `goal.Steps.Context = context` (root + sub-goals) at the
`.pr`-load seam, beside `goal.App`. Committed.

*Design note:* the goal tree is deserialized context-less and back-refs (`App`, `Step.Goal`,
`Context`) are stamped after — construct-then-stamp at tree level. Cleaner: the goal reader
(`goal/serializer/Reader.cs`, holds `ctx.Context`) borns the tree, or the goal carries a
`Context` whose `Steps`/`Goals` getters cascade it (they already cascade `Goal`/`Parent`).
Pick the single owner of "wire the deserialized goal tree."

---

## Crash #2 — `text.Value` self-resolution → StackOverflow

`text.Value` for a whole `%x%` match: `Variable.Get(x)` → `resolved.Value()`. If `x`'s value
is itself the template `%x%`, it recurses to SO (~6680 frames). **I added an AsyncLocal
resolving-set guard and reverted it — that's a band-aid for the loop, not the cause.**

**Root question:** *why is a variable's value an unresolved template naming itself?* This is
the same family as #3 (below): a variable read on the build path comes back as its own
unresolved `%ref%` instead of a concrete value. Likely the GoalCall call-by-value seam
(`this.cs:586-596`) already documents this exact hazard — *"call Foo x=%x% → x resolves %x% →
x and loops; so resolve the ref NOW before Set"* — but the equivalent doesn't hold on some
other write path (action params? `set`/`set default`? the Executor's `Set("path", …)`?), so a
self-referential binding gets stored and then loops on read. **Find the write that stores a
bare self-ref; fix it there.** (A depth cap may still be warranted as defense-in-depth, but
it is NOT the fix.)

---

## Crash #3 — `builder.goals` / `EmitBuildEvent` `path=%path%` → "path cannot be empty"

**Trace**
```
ArgumentException: path cannot be empty   path/file/this.Validate.cs:33  (ValidatePath)
  path.this.Resolve("")                   path/this.cs:128
  path.this.Value(data)                   path/this.cs:106-110
  data.Value<T>() / data.Value()
  .pr value: %path% [path]   final: %path%
```

**Mechanism (read carefully — the surfaced error masks the real one):**
`path.Value` renders its template via `_location.Value(data)` (text resolution).
`text.Value` resolves `%path%` → `Variable.Get("path")` → **`!resolved.IsInitialized`** →
`data.Fail(VariableNotFound)` + returns `Absent`. Back in `path.Value`:
`rendered = Absent.Clr<string>() ?? ""` = `""` → `Resolve("")` → `ValidatePath("")` **throws
ArgumentException**. So the ArgumentException is a *secondary* crash that hides the real story:
**the variable `path` reads back uninitialized.**

**Why that's surprising / the root question.** `path` is set on `User.Context` by the Executor
(builder mode: `Set("path", startupDirectory)`), Build runs on that same `User.Context`
(`builder/this.cs:110-111`), and `Build.goal` step `set default %path% = "/"` runs first. Yet
`Get("path")` reports `!IsInitialized`. So one of:
  1. `variable.Set(name, stringValue)` borns a Data that reads back `IsInitialized == false`
     (context-less birth via the getter flip? `IsInitialized` not set on that path?).
  2. `set default` doesn't actually initialize the binding (registers a default, never sets).
  3. the goal runs in a variable scope where the Executor's `User.Context` binding isn't
     visible (overlay/Calls-scope — see the call-by-value comment at `this.cs:586`).

Need to confirm which by inspecting `variable.list.Set` / `IsInitialized` and `set default`
semantics. (`--debug` itself currently crashes loading the debug goal — `GoalCall.LoadFromFile`
→ `path.ReadBytes` — so step-trace debugging is unavailable; that's a separate breakage.)

**Secondary (robustness, still not the root):** `path.Value` should NOT render a failed text
resolution to `""` and call `Resolve("")` (which hard-throws `ArgumentException`). When
`_location.Value(data)` fails / returns `Absent`, propagate that failure as a typed Data error
instead of throwing. The hard `throw new ArgumentException` in `ValidatePath` is the wrong
shape for an empty/unresolved path — but fixing it only changes the symptom from a throw to a
typed error; `path` being uninitialized is the real bug.

---

## Unifying picture

Every crash is **variable / context state on the build's goal-execution path** that the
getter-flip enforcement now surfaces: a collection context left null at load (#1), a variable
that stores/returns its own unresolved `%ref%` (#2), a variable that reads back uninitialized
(#3). The center of gravity is **`variable.list` (Set / Get / IsInitialized)** and the
**deserialized-goal-tree wiring**. Recommend the architect decide the canonical "a variable
binding is born initialized + with context, and a value-slot ref is resolved-at-write (never
stored as a bare self-ref)" rule, then the chain collapses rather than being patched
crash-by-crash.

**Not done (deliberately, awaiting architect):** #2 and #3 roots. #1 is fixed at root.

---

## Builder `.pr` audit — type naming leaks CLR (separate from runtime, feeds it)

Reviewed all 11 `os/system/builder/**/*.pr`.

**Structure: valid.** Every `type` is the entity form `{"name": …}` — no bare-string types,
all parse as JSON. They pass the `data` reader's "type must be an object" gate.

**Names: CLR-type leakage (builder bug).** Inline dict/list literals are stamped with raw .NET
reflection names instead of PLang names:
```
set %trace% = { id:…, goal:… }   →   "type": { "name": "dictionary`2" }   // want: "dict"
```
- `dictionary`2` (CLR `Dictionary`2`) and `list`1` (`List`1`) — 13+ occurrences across
  `BuildGoal/*.pr`, `BuildStep/*.pr`. The builder emits `Type.Name` (reflection) for an inferred
  inline object/array literal instead of the PLang type name.
- Inconsistent: list appears as `list`, `list<action>`, AND `list`1`; dict as `dict<text,text>`
  AND `dictionary`2`. Also `int`/`double` where PLang's scalar is `number`.

**Runtime impact.** `{name:"dictionary`2"}` → `TypeMapping` has no such PLang type → `UnknownType`
/ null `ClrType` → the value won't resolve/navigate as a `dict`. Same family as the runtime
resolution failures above. These `.pr` were built by a builder with the type-naming bug and need
a rebuild once the builder runs — chicken-and-egg with the crash chain.

**Where to fix:** the builder's literal-type inference (form + variable intent) must map an inline
`{…}`/`[…]` to the PLang type (`dict`/`list`), never `Type.Name`. Single source: the PLang type
name should come from the type registry, not CLR reflection.
