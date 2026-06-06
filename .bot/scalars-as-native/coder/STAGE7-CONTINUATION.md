# Stage 7 / lock-phase — continuation (read this first after a context clear)

**Goal of the resumed session:** finish the branch → **100% green BOTH suites**
(C# `dotnet run --project PLang.Tests` AND PLang `cd Tests && plang --test`), no red
stubs, all `Tests/ScalarsAsNative/Stage*/` goals built. This is the coder done-bar
(see `claude-md-proposals.md` coder-v4). You write AND build the `.test.goal` files.

Read in order: this file → `.bot/scalars-as-native/architect/stage-7-lock-and-cleanup.md`
→ `.bot/scalars-as-native/architect/plan.md` (the law + decisions) → the coder v1
report (`.bot/scalars-as-native/coder/v1/report.md`).

---

## What is DONE and committed (do not redo)

- **`item.@this` apex** (`PLang/app/type/item/this.cs`): abstract, storage-free,
  carries `IsTruthy()` (sync) + `IBooleanResolvable.AsBooleanAsync` (default → IsTruthy)
  + virtual `Narrow()` (default no-op = return self). Does NOT implement
  `IOrderableValue`/`IEquatableValue`.
- **All 8 wrappers built out, each `: item.@this`** with compare/equality/truthiness/
  parts/ops/bare-`ToString`:
  - `number` (pre-existing, rewired `: item`), `text` (ops + ordinal-ci compare),
    `datetime` (DateTime ctor + parts), **`date`** (new, DateOnly), **`time`** (new,
    TimeOnly), `duration` (parts, zero-falsy), **`bool`** (new, equality-only,
    truthiness primitive), **`null`** (new, singleton, equality-only).
  - `date`/`time`/`bool` have `this.Convert.cs` hooks (yield raw CLR) + `this.Owns.cs`.
- **~40 C# wrapper unit tests pass** (`PLang.Tests/App/ScalarsAsNative/`:
  ItemApexTests, NumberRegressionTests, TextWrapperTests, DateTimeWrapperTests,
  DateWrapperTests, TimeWrapperTests, DurationWrapperTests, BoolWrapperTests,
  NullWrapperTests[4/6]).
- **Builder fixed** (was breaking ALL builds, now works — suite 271→274):
  - `OpenAi.cs`: LLM cache stored a `JsonElement` that serialized to
    `{"valuekind":"Object"}`. Fixed: cache `RawResponse` string + re-parse on restore.
  - `Fluid.cs`: Fluid couldn't read native `dict`/`list`/`JsonNode` (no IDictionary/
    IEnumerable) → compile prompt rendered blank → compiler guessed blind (assert →
    condition.if/error.throw). Fixed: a `ValueConverter` wraps natives in lazy
    read-through views (`NativeDictView`/`NativeListView`), zero copy, O(1) dict access.
- **Tool:** `python3 tools/pr-summary.py <path|folder> [--params]` — terse .pr
  step→action mapping (flags dropped modifiers + empty actions). Use after every build.
  Traces: `python3 Documentation/v0.2/inspect-trace.py`. **Never hand-edit a `.pr`.**

---

## What REMAINS = the lock phase. Two failing groups, ONE root: born-native + narrow + constraint.

### C# — 23 stubs (`Assert.Fail("Not implemented")`), grouped by the feature each needs

| File | Needs |
|---|---|
| `ConstructionBornNativeTests` (6) | **born-native flip**: `UnwrapJsonElement` String→`text`, Number→`number`, True/False→`bool`, Null→`Data.Null()` singleton; no raw scalar escapes; **delete `UnwrapNewtonsoftToken`** (`data/this.cs`, dead v1 shim) |
| `ItemConstraintTests` (5) | **`where T : item` on `data.@this<T>`** + everything `: item` (`path`/`image`/`code`/`Variable`/`Ask`/`snapshot`); negative-compile for `Data<int>` and `Data<data.@this>` (double-wrap kill) |
| `CoercionMediatorTests` (6) | rewrite `Operator.NormalizeTypes` to inspect **wrapper** types; `"5"==5`, widening, date-vs-datetime, bool/null routing |
| `ScalarComparerCollapseTests` (4) | collapse `data/ScalarComparer.cs` Name()/per-type arms → coercion + thin IComparable fallback; `Compare.Order(text)` routes via `IOrderableValue` |
| `NullWrapperTests` (2) | `Data.Null()` stamps the `null.@this` singleton; `IsInitialized` value-vs-absence; nulls sort last via `Compare` |
| `ToBoolean_RawScalarFallbacks…` (1, in ScalarComparerCollapse) | raw fallbacks in `Data.ToBoolean()` unreachable for wrapped values |

### PLang — every `Tests/ScalarsAsNative/Stage{1..7}/*.test.goal`

- Stage-1 goals built & 3/4 pass; **`DictIsItemKeepsNoOrder` fails** — proven cause:
  `set %people% = [{…}]` lands elements as **un-narrowed `item` (kind object)**, never
  narrowed to `dict`, so `sort` (→ `Compare.Order`) never hits dict's no-order throw.
  Needs **lazy-narrow on touch/compare** (architect's `item.Narrow()` wired into the
  value path so an un-narrowed json-object item becomes a `dict` when examined).
- Stages 2–7 goals are `- throw "not implemented"` stubs — author them to the
  test-plan (`.bot/scalars-as-native/test-designer/test-plan.md`) AND build them
  green. They test born-native (`%s.type%` is text, `%dt%` is datetime, `if %b%`,
  `%x% == null`, `"5"==5`, etc.).

---

## The hard-won gotchas (these cost hours — don't rediscover them)

1. **`Variable` is a `record`; `item.@this` is an abstract `class`. A record CANNOT
   inherit a plain class.** This is THE blocker for `where T : item`. Either convert
   `Variable` (`PLang/app/variable/this.cs`) from `record` to `class` (keep value
   equality + `IRawNameResolvable`), or make `item` something a record can inherit.
   Decide this BEFORE turning on the constraint.
2. **`Ask`/`snapshot`/`path`/`image`/`code` already implement `IBooleanResolvable`.**
   Making them `: item` → their `AsBooleanAsync` becomes `override` (item declares it
   virtual). Mechanical but touches each file.
3. **`JsonNode` is the recurring villain.** `set … type=json` (`variable/set.cs:~127`)
   stores `System.Text.Json.Nodes.JsonNode`, which is IEnumerable-but-wrong-shaped.
   It blanked Fluid (fixed) and mis-decomposed in `list.FromRaw` (a JsonObject is
   IEnumerable→became a list-of-kvps). Consider the ROOT fix: make `type=json`
   produce native `dict`/`list` (via `UnwrapJsonElement`) instead of `JsonNode`. If
   you don't, audit every `is IEnumerable` / `IDictionary` consumer for the JsonNode trap.
4. **`Data.Ok(JsonElement)` unwraps** to native via the ctor's `UnwrapJsonElement` —
   that's why live LLM calls worked but the cache (raw JsonElement) didn't.
5. **The constraint cascade is the real cost**: ~25 generic `Data<T>`/`Data<U>` infra
   methods (`Merge`/`Clone`/`Ok`/`Fail`) each need `where T : item` threaded. Turn the
   constraint on LAST; the compiler then enumerates every `Data<rawCLR>` slot to fix.
6. **The ~197-site body sweep**: grep `is string` / `(string)value` / `value is int|
   long|double|decimal|bool` / `is System.DateTimeOffset|TimeSpan|DateOnly|TimeOnly` /
   `.Value is <scalar>`. Behavioral→method on wrapper; perimeter→single `.Value`;
   coercion→mediator. Do it per-type WITH the construction flip for that type.

---

## Suggested sequence (architect stage-7 + my findings)

1. **born-native construction** first (`UnwrapJsonElement` → wrappers; `Data.Null()` →
   null singleton; delete `UnwrapNewtonsoftToken`). Then the body sweep per type.
   Wire `item.Narrow()` so un-narrowed json items narrow to dict/list on touch
   (fixes DictIsItem + JsonReadIsItemUntilTouch semantics).
2. **everything `: item`** (path/image/code/Variable[record→class]/Ask/snapshot).
3. **Collapse ScalarComparer + rewrite `Operator.NormalizeTypes`** to inspect wrappers.
4. **Serializers**: each wrapper renders bare on `application/json` (add `serializer/
   <format>.cs` `Write`; they already expose the bare form via `ToString()`).
5. **Turn on `where T : item`** + thread through the generic layer; fix until it compiles.
6. **Fill in all C# stubs** (they're authored as Assert.Fail with intent comments) and
   **author + build every Stage*/*.test.goal**. Run both suites to 100%.

---

## Build / test mechanics (env-specific, verified this session)

- Rebuild binary first (stale-binary trap): `dotnet build PlangConsole` (0 errors).
- C# suite: `dotnet run --project PLang.Tests` (TUnit; only failures print; check `failed: N`).
- PLang build: from `Tests/`: `../PlangConsole/bin/Debug/net10.0/plang build
  '--build={"files":["ScalarsAsNative/Stage2/Foo.test.goal"],"cache":false}'`.
  `cache:false` forces fresh LLM (avoids stale cache). Builder IS available + works now.
- PLang run: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.
- Verify a build: `python3 tools/pr-summary.py Tests/ScalarsAsNative/Stage2/.build/foo.test.pr --params`.
- LLM cache lives in `Tests/.db/system.sqlite` (`llmcache` table); `DELETE FROM llmcache` to clear.
- Builder phrasing that maps reliably: `assert %x% equals N`, `assert %x% is true`,
  `if %x% is <type>`, `set %x% = …`, `math.add A=%a% B=%b%, write to %s%`. Keep goals
  short; split nested navigation out of asserts if a step won't map.
- A `cache:false` rebuild OVERWRITES the committed `.pr` — `cp` it aside / `git checkout`
  after diagnostics so a bad build never lands. Don't commit non-deterministic `.pr`
  until the goal builds correctly and the test passes; then it's fine to commit the green `.pr`.

## Done-bar checklist
- [ ] `where T : item` compiles; no `Data<rawCLR>`; `Data<object>`→`Data<item>`; double-wrap won't compile.
- [ ] `ScalarComparer` Name()/per-type arms gone; `UnwrapNewtonsoftToken` gone.
- [ ] All 23 C# stubs implemented & green; full C# suite green.
- [ ] Every `Tests/ScalarsAsNative/Stage*/*.test.goal` built & green; full PLang suite green.
- [ ] `dict` still throws on sort (regression guard); `"5"==5` + widening still coerce.
