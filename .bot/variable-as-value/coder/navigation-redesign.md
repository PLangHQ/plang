# Navigation redesign — the item owns its navigation (branch `variable-as-value`)

**Date:** 2026-06-23. Agreed with Ingi. This is the structural fix for the recurring
"endless reference-back loop" class (CultureInfo cycle, `%plan.usage%` self-ref).

## The disease

`data.GetChildValue(key)` calls `await Value()` to navigate ANY key. For a dict that's
`dict.Value()` = **render EVERY entry** (resolve each sibling `%ref%`). So `%plan.steps%`
also resolves `%plan.usage% = {model:%plan.Model%, …}` → re-renders `%plan%` → ∞.
Two wrongs: (a) it resolves siblings we didn't ask for; (b) navigation logic + a
reflection/Properties/whitelist fallback live in `data/this.cs` instead of on the type.

## The model

- `x.name` = **value-plane**: `data → _type → item.Navigate("name")` → `dict["name"]`.
  The ITEM owns it. Truly lazy — only the asked key is touched; `dict.Get`/`Slot` born
  one entry and don't even resolve its `%ref%` (the consumer does, on `.Value()`).
- `x!Name`, `x!Success`, `x!Type`, `x!data.branchIndex` = **Data-envelope plane**:
  working with the Data object itself — the `!` infrastructure plane (already exists,
  `GetInfrastructureValue`). `.` becomes PURELY value-nav.

## The changes

1. **Data delegates per-key nav straight to the type.** In `data.Navigate(path)`'s
   segment switch, call `await _type.Navigate(this, key)` (use `_type`, we have it — not
   `Peek()`). Inline it; remove the separate `GetChildValue` method. One line per segment.
2. **No fallbacks in Data.** Drop the reflection / `Properties` / `Name`-`Success`-`Error`-
   `Type` whitelist from the value-plane. If a spot a fallback used to cover is reached,
   **throw a typed error** (trackable) — so we find each case and move it onto the right
   type, transition-style (like the `type`-must-be-object and `DictRenderCycle` throws).
3. **`item.@this.Navigate(parent, key)` base virtual = the reflection** (the moved
   `ReflectChild`): a domain item (`step`, `goal`) navigates `.Index`/`.Steps`/… by
   reflecting its own CLR property. Returns NotFound when truly absent.
   - `dict.Navigate` = key lookup (`Get`) — already.
   - `list.Navigate` = index — already.
   - `file`/`url`/`source`/`variable`.Navigate (override) = **materialize self** (open own
     door, `.Value()` — read `config.json` → dict) THEN navigate the result. The `.Value()`
     that genuinely materializes a reference lives HERE, on the type that needs it — never
     in `data/this.cs`.
   - `text`/leaf.Navigate = NotFound → the existing "navigate `.x` on text — add
     `as <type>`" error (the one Data-side message that stays, or moves to text).
4. **Data-envelope fields → `!` plane** (`%x!Name%`, `%x!Success%`, `%x!Type%`). `.` no
   longer reads Data fields. (`%goal.Name%` still works — that's the goal *item's* `Name`
   property via base-reflection, not `Data.Name`.)

## Order (each step build + repro-test before the next)

- [ ] Step A: base `item.@this.Navigate` does reflection (move `ReflectChild` logic);
      `data.Navigate(path)` segment switch calls `_type.Navigate(this, key)`; drop
      `GetChildValue` + its fallbacks; throw on the would-be-fallback path. Build, run
      the `Tests/Scratch/Repro.goal` builder repro (`timeout` — it can loop), see what throws.
- [ ] Step B: add materializing `Navigate` to `file`/`url`/`source`/`variable` (the cases
      that throw in A because they needed `.Value()` first).
- [ ] Step C: move Data-envelope `.Name/.Success/.Type` reads to `!`; fix any caller.
- [ ] Step D: full `./dev.sh full` — this changes navigation for ALL dict/list/domain
      items, so the whole suite is the regression gate. Then commit.

## Context / where we are

The builder now runs `Build → BuildGoal → Plan → BuildStep → Compile`. The blocker this
fixes: `set %goal.Steps[step.Index].Formal% = %compileResult.formal%` and the `%plan%`
render cycle. Backstop in place: `dict.Value` throws `DictRenderCycle` past depth 64
(keep it — a real loop should still fail loud, not overflow).

Related todos already logged in `Documentation/v0.2/todos.md`: goal.call eager `.Value()`
injection (isolated-scope fix); `Variables.Set(string, object?)` → `Data`; goal reader
text-then-STJ.
