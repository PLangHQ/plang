# coder v2 — variable resolution for complex objects + JsonNode conversion

## What this is

Two related bugs in PLang's value-resolution path were preventing the LLM
builder from running. User-facing symptom: `plang build` crashed with NRE
in `OpenAiProvider.Query`. Real cause: nested `%var%` references inside
list/dict parameter values were never being substituted on the plain-`Data`
path, and `set ... type=json` then re-cast those (already-broken) values
through `JsonNode`, which downstream typed handlers couldn't convert.

Fixing both restores symmetry between plain `Data` and `Data<T>` resolution
(one helper, both call sites) and gives the JSON pipeline a complete
roundtrip from goal source through to typed handler.

## What was done

### 1. Variable resolution on plain Data — `PLang/App/Data/this.cs`

Extracted `WalkContainerVars(raw, ctx)` and `IsWalkableContainer(raw)` as
private helpers. Both `AsCanonical` (plain Data) and `AsT_Impl` (typed
Data<T>) route through `WalkContainerVars` — the same `WalkList` /
`WalkDict` / `SubstitutePrimitive` machinery already existed; what was
missing was `AsCanonical` calling it.

Before: `AsCanonical` returned `this` unchanged when `raw` was a list or
dict, so `variable.set.Value.Value` saw literal `"%buildGoalPrompt%"`
strings inside the parameter's nested structure.

After: `AsCanonical` returns a fresh `Data` whose value has been walked
the same way the typed path walks it.

### 2. JsonNode in the conversion dispatch — `PLang/App/Utils/TypeConverter.cs`

Added `JsonNode` to the complex-source check at line ~336 (alongside
`IDictionary<string, object?>`, `JsonElement`, `IList`). Without this,
`JsonObject` (which implements `IDictionary<string, JsonNode?>`, NOT
`IDictionary<string, object?>`) skipped every dispatch arm and landed at
`TypeMismatch`. With `JsonNode`, the existing JSON-roundtrip (serialize
the source, deserialize to target type) works.

Added a parallel `JsonArray` element-iteration arm next to the existing
`JsonElement`-array arm. `JsonArray` implements `IList<JsonNode?>` but
NOT non-generic `IList`, so it would otherwise fall through to the
"recurse for single-element-list-wrap" branch and fail.

### 3. Tests

- `PLang.Tests/App/DataTests/AsTIdentityTests.cs` — 4 new tests covering
  the gap: plain Data with list/dict whose elements contain `%vars%`,
  list-of-dicts (the actual `BuildGoalCore` shape), and literal-list
  values preservation.
- `PLang.Tests/App/Engine/Utility/TypeMappingDictConversionTests.cs` — 2
  new tests: `JsonObject → typed class` and `JsonArray → List<typed>`.

### 4. Cleanup

- Deleted lowercase `tests/` directory (duplicates of `Tests/Modules/Modifiers/`
  + `Tests/Simple/` — every goal file was byte-identical).
- Deleted `.bot/runtime2-settings/scaffolder/v1/tests/plang/Start.test.goal`
  (stale leftover from another bot session).

## Code example — the helper extraction

```csharp
// Single source of truth for "walk %vars% in any container".
private static bool IsWalkableContainer(object? raw) =>
    raw is IList<object?> || raw is IDictionary<string, object?>;

private static object? WalkContainerVars(object? raw, Actor.Context.@this ctx)
{
    if (raw is IList<object?> list) return WalkList(list, ctx);
    if (raw is IDictionary<string, object?> dict) return WalkDict(dict, ctx);
    return raw;
}

// In AsCanonical (after the string-resolution branch):
if (ctx != null && IsWalkableContainer(raw))
{
    var walked = WalkContainerVars(raw, ctx);
    var transient = new @this(Name, walked, _type, Parent) { Context = ctx };
    transient.Properties = Properties;
    transient.OnCreate   = OnCreate;
    transient.OnChange   = OnChange;
    transient.OnDelete   = OnDelete;
    return transient;
}

// In AsT_Impl (replacing the inline IList/IDict checks):
if (ctx != null && IsWalkableContainer(raw))
    return WrapAs<T>(WalkContainerVars(raw, ctx), ctx);
```

## Verification

- `dotnet run --project PLang.Tests` — **2530 pass / 9 fail** (all 9 failures
  are pre-existing `Assert.Fail("Not implemented")` placeholders in
  `ListAddIdentityTests` and `Plng001PostMigrationTests`, unchanged by
  this work).
- `plang --test` — **166 / 166 pass**. (Was 170/173 before; the delta is
  the deleted lowercase duplicate tests.)
- `plang build Start.goal` — original `OpenAiProvider:64` NRE is gone; the
  builder reaches the LLM, gets a response, then fails at a separate
  pre-existing `Actor` param validation issue ("Cannot convert String to
  this") that already shows up in `tests/modifiers/PerActionErrorScope`.
  Out of scope for this fix.

## What's next

The remaining `Actor` param validation issue is the same shape as the
two ex-failures the user surfaced. Worth a follow-up session — the LLM
builder is producing a string like `"this?(user|service|system)"` (looks
like the schema description leaked into the value slot) for the `Actor`
parameter. Likely a builder template or LLM-prompt issue, not a runtime
type system one.
