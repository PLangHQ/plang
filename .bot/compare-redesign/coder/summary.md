# Coder — compare-redesign

## Version: v5 — Stage 2 async-door cutover (sprint mode). **FULL SOLUTION COMPILES (0/0/0).**

`PLang` + `PlangConsole` + `PLang.Tests` all build clean. Production: 2130→0 errors **and 0 CS8974**
(no silent method-group `.Value` left in production). Tests: 1736→0 errors. The all-or-nothing Stage 2
cutover **compiles end to end**.

### ⚠️ KNOWN GAP — 130 silent `.Value` sites in TESTS (CS8974), causing runtime failures
The `.Value` property→method change has a trap: `.Value` (now a method group) passed to a non-Assert
position — `ReferenceEquals(d.Value, x)`, `Convert.ToInt64(result.Value)`, `object? v = d.Value;`,
ternaries — converts **method-group → object** silently. It is **not** a compile error and **not** an
Assert.That/TUnit-analyzer error, so neither my compile-error worklist nor the TUnit pass caught them.
They assert on/use the *method*, not the value → **runtime test failures** (~10/95 in DataTests; more
across the suite). **They do NOT break compilation.**
- **Worklist:** `dotnet build PLang.Tests 2>&1 | grep "warning CS8974"` — 130 sites, each a Data-receiver
  `.Value` to wrap as `await X.Value()` (async ctx) or `X.Materialize()` (sync).
- **Production is clean** of these (verified 0 CS8974 in PLang). Only tests have them.
- My regex passes misfire on the sync/async split here (the same line can be method-group in either
  context). Do this pass **per-file with the enclosing-method async check**, not one global regex;
  build `--no-incremental` between (incremental counts are unreliable — they bit me repeatedly).
- Smoke test (DataTests) after the door: **85/95 pass** — the door is behavior-preserving for in-memory
  values; the 10 failures are these CS8974 sites (e.g. `ReferenceEquals(ov.Value, null.Instance)`).


Ingi's call: **full sprint, build red mid-flight; go through everything, don't stop, commit/push
between.** This session drove the entire Stage 2 door cutover.

### MILESTONE (committed, pushed)
- **`PLang` + `PlangConsole` compile clean — 2130 → 0 `error CS`.** The all-or-nothing part of the
  sprint (the async `Value()` door + every Data-receiver `.Value` migration across ~120 production
  files) is structurally complete.

### The door (PLang/app/data/this.cs)
- `public virtual ValueTask<object?> Value()` — the single public async read door. No public sync
  `.Value` property.
- `internal virtual object? Materialize()` — the sync in-memory core (factory + parse-rung). Sync
  surfaces that can't `await` read through it. `DynamicData` overrides it. `ParseRaw()` = inner parse.
- `public virtual void SetValue(object?)` — the write side (was the setter).
- `Data<T>.Value()` typed async; `Peek()` = current rung, no parse (distinct from Materialize: see below).
- **OBP (Ingi caught this):** the sync read is the **verb** `Materialize()` (internal plumbing), NOT
  a `CurrentValue`/`Materialized` noun-twin of `Value` (smell #4 / verb+noun).
- **Three levels:** `Peek()` = current rung, no parse; `Materialize()` = parse (sync, no I/O);
  `await Value()` = async I/O read (Stage 3) + parse. They coincide once materialised.

### Source generator (PLang.Generators/Emission/{Property/Data,Property/Code,Action})
Emits `Peek()` for param-slot diagnostics + presence guards, `Materialize()` for `[Code]` injection.
Collapsed all 956 generated errors.

### The recipe used across ~120 files
1. Async method: `await X.Value()` (typed door returns T; clean for typed params).
2. Sync surface (serializer, predicate, lambda-in-sync-delegate, build-time, `INavigator`): `X.Materialize()`.
3. Guard-reorder where a param is guarded: `var v = await X.Value(); if (!X.Success) return X;`.
4. Write `X.Value = v` → `X.SetValue(v)`.
5. `data.Value is/as T` → `Materialize() is/as T` (sync) or `await Value() is/as T` (async).
6. `Data<bool>` truthiness: `(await X.Value())?.Value == true` / `(X.Materialize() as @bool.@this)?.Value`.
7. Typed `.Value.Member` in sync: `(X.Materialize() as ConcreteType)!.Member`.
Watch: `await` cannot go inside a sync lambda (`items.Find(i => …)`) — hoist the value to a local first.

### NOT done — the rest of the sprint
- **`PLang.Tests` — 1736 `error CS` across 169 test files.** Same `.Value` migration in test code
  (most `[Test]` are `async Task`, so `await X.Value()`). Mechanical, follows the recipe. The
  compiler error list is the worklist: `dotnet build PLang.Tests 2>&1 | grep "error CS"`.
- **Stages 3–6** (reference types `file`/`directory`/`url`, narrow-on-examination, per-type `Compare`
  → `Comparison` enum, the `data.Compare` async entry, consumers + demolition of the old mediator).
  The door is async-*shaped* but `Value()` still sync-completes everything — real async I/O reads,
  narrowing, and the typed compare are Stages 3–6. The ~140 CompareRedesign test stubs stay red
  until those land.
- Navigation chain is still **sync** (reads via `Materialize()`); making `GetChild`→`Variable.Get`
  →`Resolve` a `ValueTask` chain is the deferred nav-async sub-step.

### PLang.Tests migration — the approach (learned this session; do it in ONE pass)
1736 sites across 169 files, almost all `.Value` on a Data in an `async [Test]` method. A
**receiver-aware regex, applied ONCE**, clears ~1700 of them. The regex that works (validated to
1736 → ~26 before I hit corruption from *re-running* it):

```python
valpat = re.compile(r'([A-Za-z_][\w.]*(?:\([^()]*\)[\w.]*)*)(!?)(\??)\.Value(!?)(?!\w|\()')
# async method  -> (await <recv><!?>.Value())<!?>
# sync method   -> (<recv><!?>.Materialize())<!?>
```
Receiver must allow dotted chains AND single-level method calls (`a.At(0)!`, `d.Get("k")!`) AND a
trailing `!` (`typed!.Value`). Detect async by the nearest enclosing `public/private…(` decl line
containing `async`.

**Pitfalls that bit me (why I reverted rather than ship corruption):**
- **Run the pass exactly once.** Re-running over already-wrapped lines, or layering "fix-up" regexes
  (dangling-dot, `@this`, `global::`), compounds into double-`await`s and split tokens. One pass, then
  hand-fix the residual.
- **Sync lambdas inside async methods** (`items.Find(i => … .Value)`, `if (payload.Value is string)`
  in a predicate) — the method is async but the lambda isn't → `await` there is CS4034. ~30 sites;
  the residual after the one pass. Fix by hand to `Materialize()`.
- **Nested casts** (`((Dict)(await d.Value())!).Get("port")!.Value`) — a handful; hand-wrap.
- **`global::`/`@this` prefixes** the regex won't span — hand-fix the ~2.
So: ONE receiver-aware pass, then ~30 sync-lambda + ~5 nested-cast lines by hand. The clean state is
preserved — tests are at their original 1736, not a half-migrated mess.

### Next
1. Migrate `PLang.Tests` (1736 sites) via the one-pass receiver-aware regex above + ~35 hand-fixes.
2. Stages 3–6 (per architect stage files) → land green at the 2→6 boundary; CompareRedesign stubs pass.
3. Run both suites.
