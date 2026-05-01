# coder v2 — variable resolution for complex objects + JsonNode conversion

## Why this exists

User asked me to run plang tests and "rebuild some" if needed. `plang --test`
showed 170/173 with 2 failures and 1 stale entry. Investigation revealed:

1. Two failing tests sat in **lowercase** `tests/modifiers/` — stale duplicates
   of the canonical `Tests/Modules/Modifiers/` versions. Their `.pr` files
   predate the modifier-shape change.
2. One stale `.bot/runtime2-settings/scaffolder/v1/tests/plang/Start.test.goal`
   from an old bot session.
3. Trying to rebuild via `plang build` crashed with NRE inside the LLM
   call — a builder bug independent of the failing tests.

User wanted (a) all tests under `Tests/`, (b) the stale bot test deleted,
(c) the builder NRE investigated, (d) no hand-editing `.pr` files.

## What I found (root cause)

`BuildGoalCore` step 6:
```
- set %messages% = [{"Role":"system", "Content":"%buildGoalPrompt%"}, ...], type=json
```

The `.pr` loader (`UnwrapJsonElement` / `UnwrapJsonArray`) parses the Value
parameter into `List<object?>` of `Dictionary<string, object?>`. When the
`variable.set` handler reads `Value.Value`, the **nested `%var%` references
inside the list/dict were never resolved**. `AsCanonical` (used by plain
`Data.@this` properties like `variable.set.Value`) only walks string-shaped
raw values — for lists/dicts it returns `this` unchanged. The typed
`As<T>()` path DOES walk via `WalkList`/`WalkDict`, so resolution was
asymmetric.

Even after fixing resolution, the second-order issue remained: `set ...
type=json` re-casts the resolved structure through `JsonNode`, and
`TypeConverter.TryConvertTo` couldn't convert `JsonObject`
(`IDictionary<string, JsonNode?>`, NOT `IDictionary<string, object?>`) to
strongly-typed targets like `LlmMessage`. JsonObject slipped past every
dispatch arm and landed at "TypeMismatch".

## Fix shape

1. **Single source of truth for nested-var walking** — extract
   `WalkContainerVars(raw, ctx)` on `Data.@this`. Both `AsCanonical` and
   `AsT_Impl` route through it. Plain `Data` and `Data<T>` now resolve
   nested vars by the same rule.
2. **`JsonNode → typed target`** — add `JsonNode` to the complex-source
   dispatch in `TypeConverter.TryConvertTo` (alongside `IDictionary<string,
   object?>`, `JsonElement`, `IList`). Added a parallel `JsonArray`
   per-element iteration arm (mirrors the existing `JsonElement`-array
   case) since `JsonArray` doesn't implement non-generic `IList`.

## Files

- `PLang/App/Data/this.cs` — add `IsWalkableContainer` + `WalkContainerVars`,
  call from both `AsCanonical` and `AsT_Impl`.
- `PLang/App/Utils/TypeConverter.cs` — add `JsonNode` to dispatch; add
  `JsonArray` element-iteration arm.
- `PLang.Tests/App/DataTests/AsTIdentityTests.cs` — 4 new tests (list with
  nested var, dict with nested var, list-of-dicts with nested vars,
  literal-list values preserved).
- `PLang.Tests/App/Engine/Utility/TypeMappingDictConversionTests.cs` — 2
  new tests (JsonObject → class, JsonArray → list-of-class).

## Cleanup (separate from the bug fix)

- Delete lowercase `tests/` directory (duplicates of `Tests/`).
- Delete `.bot/runtime2-settings/scaffolder/v1/tests/plang/Start.test.goal`.

## Verification

- `dotnet run --project PLang.Tests` — 2530/2539 pass (9 pre-existing
  `Assert.Fail("Not implemented")` placeholders unchanged).
- `plang --test` — 166/166 pass (was 170/173; the 7-test delta is the
  deleted lowercase duplicates).
- Build of `Start.goal` now progresses past the original NRE; fails later
  at a pre-existing `Actor` param validation issue ("Cannot convert String
  to this") that is unrelated to this work.
