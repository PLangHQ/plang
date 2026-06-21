# Navigation redesign — the item navigates itself

**Status:** design, not started. Branch `variable-as-value`.
**Why now:** chasing the `%goal.Steps[planStep.index]%` bug surfaced that navigation
is a procedural string-walking pile living *off* the values, not on them. Fixing the
bug minimally is done (see "Interim fix" below); this doc is the real fix.

---

## The smell

Navigating `goal.Steps[planStep.index]` today runs a pipeline that lives on `Data`
and the variable store, not on the values being walked:

```
Variables.Get(name)
  → ResolveVariablesInPath(name)        // regex pre-pass: rewrite [var] → [literal]
  → GetRootName(name) + string slicing  // find the root, peel ".x" / "[i]"
  → Data.GetChild(remaining)
       → ParseNextSegment(path)         // free fn: hand-rolled tokenizer
       → strip "[ ]", quotes, "(...)"   // more string surgery
       → GetChildValue(segment)         // ONLY here does a value get asked
```

`GetChildValue` itself is a **seven-layer fallback** for one hop:

1. `val.Navigate(this, key)`         ← the OBP answer (value owns it)
2. `GetType().GetProperty(key)`      ← Data-subclass reflection (DynamicData)
3. `_context.App.Navigator.Get(val.GetType()).Navigate(...)` ← external registry
4. `ValueNavigators.Navigate(...)`   ← static fallback for "no app context"
5. `Properties[key]`                 ← metadata fallback
6. whitelisted base props (Success/Error/Name/Type)
7. text → error

### Why it's anti-OBP (CLAUDE.md smells)

- **Verb+Noun free functions doing the work:** `ParseNextSegment`,
  `GetChildValue`, `SetValueOnObject`, `ResolveVariablesInPath`. The flashing
  sign. Path parsing is the **path's** behavior; child lookup is the **item's**.
  Neither is a static helper's.
- **Behavior outside the type (smell #1/#7):** `Data` opens the value and parses a
  string to decide how to walk it. A courier reading the value to branch on it.
- **Same logical thing stored twice (smell #3):** two navigation mechanisms — the
  `INavigator` registry (`Dictionary`/`List`/`Object`/`Snapshot`) *and* the
  `item.Navigate` / `item.Write` virtuals. The virtuals already exist; the
  registry is the leftover half of an unfinished migration. `item/this.cs:80`
  literally says "until those collapse onto Navigate as well."
- **Read/write divergence:** reads go through `GetChild`; writes go through a
  *different* path (`Set` → `LastIndexOf('.')` split → `target.Write` /
  `SetValueOnObject` reflection). Two walkers, each re-deriving how to index a
  list. The bracket-index-is-a-variable case has to be solved twice.

The net: removing one line of choreography (the bracket pre-pass) needs edits in
three files. Per CLAUDE.md, "those three files are one missing type."

---

## The target shape

Two values, each owning its own behavior. No free-function tokenizer, no
seven-layer fallback, no registry.

### 1. `path` is a value that knows its own segments

A navigation path (`goal.Steps[planStep.index]`, `user.name`, `x!file!path`,
`tags."key.with.dots"`, `data.grep("p").maxLength(100)`) is itself a value.
It parses **once, into itself** — a sequence of typed segments:

- `Member("Steps")`          — dot member / dict key
- `Index(<segment-value>)`   — bracket; the inner is *itself a path value*, so
                               `[planStep.index]` is `Index(Path("planStep.index"))`,
                               not a string to regex. Resolving it is just
                               evaluating that inner path against the store — the
                               same evaluation as everything else. No special case.
- `Infrastructure("file")`   — the `!` plane (Data's own Name/Error/Type/Props)
- `Call("grep", args)`       — method-style segments

`ParseNextSegment` / `ResolveVariablesInPath` / `GetRootName` / the bracket regex
all die. The path owns its tokenization; a quoted segment, a bracket, a `!`-hop
are *segment kinds*, not `if`-ladders in a walker.

### 2. `item.Navigate(segment) → child` and `item.Write(segment, value)`

Each value owns one hop, by segment kind — already the `Navigate`/`Write`
virtuals, finished:

- `dict.Navigate(Member k)`  → its key
- `list.Navigate(Index i)`   → its element; `list.Write(Index i, v)` → set element
- `goal.Navigate(Member k)`  → `Steps` / `Name` / ... (domain member)
- leaf (`text`/`number`)     → "can't be walked by key" error, on the leaf
- the host-object carrier     → reflects its host (the existing `clr` carrier)

The `INavigator` registry, `ValueNavigators`, and `GetChildValue`'s fallback tower
collapse into these virtuals. The `!` infrastructure plane stays a *separate*
concern (it reads `Data`, not the value) — it is the one thing that legitimately
lives on `Data`, because it navigates the binding, not the value.

### 3. Walking is the only orchestration left

```
data.Navigate(path):
    (head, tail) = path.Split()        // path owns this
    child = await value.Navigate(head) // value owns this
    return tail.IsEmpty ? child : child.Navigate(tail)
```

Reads and writes share the walk; only the **last** segment differs (read = return
child; write = `value.Write(lastSegment, v)`). One walker. The list-element write
(`%a.b[i]% = v`) stops being a reflection special case — it's
`(navigate a.b).Write(Index i, v)`, and `list.Write(Index, v)` already exists.

---

## What collapses (delete list)

- `Data.GetChild(string)` string walker            → `Data.Navigate(path)` over segments
- `ParseNextSegment` (free fn)                      → `path.Split()` (path owns it)
- `ResolveVariablesInPath` + bracket regex + guard  → `Index(Path(...))` segment eval
- `GetRootName` string slicing                      → first segment of the path
- `GetChildValue`'s 7-layer fallback                → `item.Navigate` (+ `!` plane on Data)
- `INavigator` / `navigator.list` / `ValueNavigators` / `Dictionary`/`List`/`Object`/`Snapshot` navigators → `item.Navigate` overrides
- `SetValueOnObject` + `SetChildProperty` reflection fork → `item.Write` overrides + shared walk

The `clr` host-carrier keeps reflecting its host — but as *its own* `Navigate`/
`Write` override, not as a registry entry the walker reaches for.

---

## Migration plan (incremental, green at each step)

1. **Path value + `Split()`** — introduce the `path`/segment value, parse the same
   strings `ParseNextSegment` accepts (dot, bracket, quote, `!`, method). Unit-test
   parity against the current tokenizer before anything consumes it.
2. **Read walk on segments** — reroute `Data.GetChild` to walk segments calling
   `item.Navigate`. Keep the old fallbacks *temporarily* behind `item.Navigate`'s
   default so untconverted types still resolve. Full suite green.
3. **Finish `item.Navigate` per type** — move each legacy navigator's body onto the
   value (`dict`, `list`, `clr`/object, `snapshot`). Delete the registry + the
   `GetChildValue` fallback tower as each lands. Green per type.
4. **Write walk on segments** — `Set` uses the same walk; last segment →
   `item.Write`. Delete `SetValueOnObject` / the `LastIndexOf('.')` split /
   `SetChildProperty` reflection. List-element write falls out of `list.Write`.
5. **Kill the pre-pass** — `ResolveVariablesInPath` is gone; `[planStep.index]` is a
   segment whose inner path evaluates through the same walk. Remove the interim fix.
6. **`!` plane** — confirm infrastructure access (`!file!path`, `!data.branchIndex`,
   Name/Error/Type) stays on `Data` as the one binding-level navigator, distinct
   from value navigation.

Risk is in steps 3–4 (every navigator + the write fork). Each type converts
independently, so the suite stays green between conversions.

---

## Interim fix (already landed on this branch — remove at step 5)

`ResolveVariablesInPath` no longer reimplements navigation (`GetRootName` + `Peek`
of the root, which dropped the `.index` tail of `[planStep.index]`). It now
delegates to `Get(varName)` — the one resolver — so the bracket index navigates
its full path. Minimal, *reduces* divergent code, unblocks `plang build`. It is a
patch on the procedural pile, not the fix; this redesign is the fix.

## Tests that pin current behavior (parity targets)

- `PLang.Tests/Data/App/CollectionsAreData/Stage3_ArraysAsDataTests.cs` (navigators)
- `PLang.Tests/Data/App/VariablesTests/VariablesTests.cs` (path get/set, brackets)
- `PLang.Tests/Runtime/.../GoalAccessorTests.cs`, `GoalDataTests.cs`
- builder goals: `BuildStep/Start.goal` reads `%goal.Steps[planStep.index]%` and
  writes `%goal.Steps[step.Index].Actions%` — the real-world read+write+bracket case.
