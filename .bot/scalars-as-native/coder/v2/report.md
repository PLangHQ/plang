# Coder v2 — `where T : item` FLIP LANDED (both suites)

## Result

The constraint flip is **merged onto `scalars-as-native` and pushed** (HEAD `6fec7e875`).

- **Production (PlangConsole) compiles with `where T : item` ON** — 0 errors.
- **C# suite: 4165/4165 green.**
- **PLang runtime: 265/272** — up from the flip-merge baseline of **206/272** (+59, zero new
  regressions; the 7 remaining are all a subset of the baseline failures).

Commits:
- `c2bf7b426` — flip ON + the As<T> test redesign + production gaps the flip left.
- `822d3ba44` — variable.set reconstructs the `as <type>` entity from its wire dict.
- `6fec7e875` — TypeFromWire unwraps a born-native `@bool` wrapper for `strict`.

## What the As<T> redesign required (the named task — DONE)

Test rules applied: wrapper slots assert via `GetValue<raw>()`; raw-projection tests drop
`.Value`; `Data<object>`/`Data<Guid>` are now structurally unrepresentable (guards became
compiler-enforced); variance ref-share moved to a scalar (native collections are walked).

It surfaced **real production gaps** the flip left behind — fixed here:
- `variable.set` mint built `Data<rawCLR>` → maps raw→item wrapper, wraps the value; resolves
  the `as <type>` entity from a string name OR the `{name,kind?,strict?}` wire dict (dict via
  `TypeFromWire`, name via `FromName` to keep ClrType = Int32 not the wrapper); `strict` wrapper
  unwrapped.
- `dict`/`list` `ToRaw()` now fully unwrap scalar wrappers (a `dict` projects to a genuinely-raw
  `Dictionary`).
- Context-free conversion path (`convert.@this.OfStatic`) so the Text serializer parses
  `"True"`/datetime/binary without an App; skip it for `IError` values; chain the conversion
  failure onto an error value's chain at the `TypeMismatch` leaf (original error stays primary).
- `datetime.Convert` adopted the `returnWrapper` pattern + its missing `OwnedClr` kind.
- `Data.Type.ClrType` unwraps scalar leaves to the raw CLR mate (`Int32`, not `number.@this`).
- Schema/catalog surfaces `choice<T>` types under the inner type's name (`operator`, …) — in
  `GetTypeName`/`GetTypeNameStatic` and the `BuildTypeEntries` walk.

## Remaining 7 PLang failures — ALL separate-subsystem / design work (NOT As<T>, NOT regressions)

1. **`TypedReturns` Stage0/Stage4 (5)** — build-time type-inference. The downstream variable
   annotates as `"object"` not `"json"`/`"csv"`. This is the **object→item fold** + build-time
   `→ returns` stamping subsystem the handoff deferred. Each test builds a sub-probe via
   `builder.goals` internally, so a `.pr` rebuild does not help. Needs the object→item fold.
2. **`LazyDeserialize/NavigationOnTypeUnknown_AsksForAsType` (1)** — a **navigation-semantics
   design decision**: `%x.port%` on a text JSON-string — auto-narrow text→dict, or error+ask-as-type?
   Needs Ingi's call (ties to the per-path-lazy-narrow todo).
3. **`ScalarsAsNative/Stage1/DictIsItemKeepsNoOrder` (1)** — the **PLang builder drops the
   `on error set %caught% = true` handler** when compiling `sort %people%, on error …` (the rebuilt
   `.pr` has empty `errorHandlers`; the runtime sort correctly throws "cannot order dict"). A
   builder/compile limitation (the v0.1 "cannot combine two modules in one step" family), not runtime.

`PhotoPathExistsMissingFile` is flaky on a `Tests/Types/tmp-photo-missing.png` artifact left by the
copy/delete test — passes on a clean tree.

## Mechanics confirmed this session
- The LLM builder works headless: `plang '--build={"files":["…goal"]}'` (7.5s/goal). Rebuilding a
  `.pr` also touches `Tests/.build/app.pr` and can mark sibling `.pr` `[Stale]` — revert if a
  speculative rebuild doesn't land green.
- Stash-and-rebuild to baseline: `git stash push -- PLang/` gives the flip-merge production state
  (PlangConsole compiles; PLang.Tests does not, but the runtime suite runs off PlangConsole).
