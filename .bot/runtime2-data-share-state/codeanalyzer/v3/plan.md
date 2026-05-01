# codeanalyzer v3 — review of coder/v2

Commit reviewed: `24cba238 coder v2: nested %var% resolution in plain Data + JsonNode conversion`.

User asked for **extra focus on the variable-resolution walk that fixes the
json → LlmMessage path** (`set %messages% = [...], type=json` reaching the
LLM provider with literal `"%buildGoalPrompt%"` instead of resolved
content). Plus my regular five-pass job.

## What changed in coder/v2

Two production files + two test files (cleanup of stale `tests/` dir is
out-of-scope for code review):

1. `PLang/App/Data/this.cs`
   - `WalkContainerVars(raw, ctx)` and `IsWalkableContainer(raw)` extracted
     as private statics.
   - `AsCanonical` calls them after the string-substitution branch — when
     `raw` is `IList<object?>` or `IDictionary<string, object?>`, it now
     walks nested `%var%` and returns a fresh transient Data with state
     aliased.
   - `AsT_Impl` was refactored to reuse the same helper (replacing inline
     `IList<object?>` / `IDictionary<…>` checks).
2. `PLang/App/Utils/TypeConverter.cs`
   - `JsonNode` added to the complex-source dispatch arm (alongside
     `IDictionary<…,object?>`, `JsonElement`, `IList`).
   - Parallel `JsonArray` element-iteration arm added in the list path
     (mirrors the existing `JsonElement` array arm, since `JsonArray`
     implements `IList<JsonNode?>` but not non-generic `IList`).
3. Two new tests files exercise both paths.

## Plan (5-pass review with extra emphasis on the var-walk)

### Pass 1 — OBP

- `IsWalkableContainer` / `WalkContainerVars` are private statics. Verify
  they don't violate the partial-class shape rule (Rule 5 — every operation
  on X belongs to X). They walk `Data`-shaped values, so the placement is
  fine; but verify nothing else outside Data already had a notion of
  "walkable container" that this duplicates.
- `JsonNode` arm in TypeConverter — purely a dispatch addition, no OBP
  surface.

### Pass 2 — Simplification (var-walk focus)

- `AsCanonical`'s partial-interpolation branch and new container-walk
  branch both construct a fresh `@this` and re-alias `Properties`,
  `OnCreate`, `OnChange`, `OnDelete`. That's 6+ lines duplicated. Worth
  extracting a `BuildTransient(value, ctx)` helper? Or are the two paths
  just close enough to leave?
- `IsWalkableContainer` + `WalkContainerVars` is two methods with mirrored
  type checks. Could collapse to one method that returns `(bool walked,
  object? walked_value)`. Minor.
- The `?? new ...` defensive fallbacks in set.cs (carried over) are still
  unreachable per v2 sub-finding. Coder/v2 didn't touch them — out of
  scope, but flag re-noted.

### Pass 3 — Readability

- The two new helpers are well-commented. The inline comment on
  `WalkContainerVars` (line 511-515) explains the
  AsCanonical-vs-AsT_Impl string-handling split clearly.
- `WalkContainerVars` and `IsWalkableContainer` use the same shape check
  — the comment chain that ties them together is good.
- The `JsonArray` arm comment in TypeConverter (`line 126-128`) explains
  the why crisply.

### Pass 4 — Behavioral reasoning (heavy weight here)

The user's extra-focus request lives here. I will:

1. **Trace the var-walk through the json→LlmMessage path** end to end —
   from `.pr` load → `AsCanonical` → `WalkContainerVars` → `set.cs`
   `Value.Value` → `TryConvertTo(list, JsonNode)` → JsonArray storage →
   downstream `As<List<LlmMessage>>` → `JsonArray` arm → element JSON
   roundtrip. Every step needs to do exactly what the next step expects.
2. **Symmetry between AsCanonical and AsT_Impl** — coder claims they now
   "resolve nested vars by the same rule." Verify. Specifically:
   - On a list-of-dicts with nested vars, do they produce equivalent
     resolved structures (modulo the typed/canonical wrapper around them)?
   - Is the cycle-protection / depth-limit enforced for the AsCanonical
     container path? AsT_Impl uses `_resolvingValues` for strings; the
     container walk goes through `SubstitutePrimitive` which has no
     visibility into that set. Is there a regression risk?
3. **Action-destination carve-out** — `AsT_Impl` skips the walk for
   `Action.@this` targets. Does `AsCanonical` also need the carve-out?
   Plain `Data` slots can hold action templates too (e.g. `if/else`
   structures).
4. **JsonObject / JsonArray dispatch edges** —
   - JsonObject (the Mutable JSON view) deserializes back to typed via
     JSON serialize-roundtrip. Verify that an inner `%var%` that's
     a `JsonValue` (not a string) doesn't get resolved.
   - When `targetType` is `JsonNode` itself (the `set ... type=json`
     target), we hit the dispatch arm but the targetType IS JsonNode, so
     do we enter the wrong arm or short-circuit at line 59
     (`IsAssignableFrom(value, JsonNode)`)?
5. **The AsCanonical string branch returning live var Data** — can the
   returned live Data have a list-or-dict value that itself contains
   nested `%vars%` from a stored binding? If so, do we resolve them?
   Currently no — only the parameter Data's container is walked, not the
   live var's. Probably correct (live vars hold resolved values), but
   confirm.

### Pass 5 — Deletion test

For each new line:
- Could `IsWalkableContainer` be inlined? Used in two places — borderline.
- Could the AsCanonical container branch be deleted? No — it's the actual
  bug fix.
- Could the `JsonArray` arm in TypeConverter be deleted? No — covered by
  the `TryConvertTo_JsonArrayToListOfClass_DeserializesEachElement` test.
- Could the cycle-protection skip the walk path? Walk has no built-in
  cycle guard — verify whether the walk is reentrant-safe.

## Files to read

- `PLang/App/Data/this.cs` — already loaded.
- `PLang/App/Utils/TypeConverter.cs` — already loaded.
- `PLang.Tests/App/DataTests/AsTIdentityTests.cs` — already loaded.
- `PLang.Tests/App/Engine/Utility/TypeMappingDictConversionTests.cs` — already loaded.
- `PLang/App/modules/variable/set.cs` — already loaded; trace consumer side.
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — to confirm
  Action template handling.
- Spot-check a handler that takes `Data<List<LlmMessage>>` to verify the
  full path.
