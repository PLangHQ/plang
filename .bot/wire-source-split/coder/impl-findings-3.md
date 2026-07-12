# Coder impl-findings 3 — the real regression count, and the json-content crux

Branch: `wire-source-split`. Status correction + one blocking decision.

## Honest count: ~38 regressions, not 4

I was watching Data/Wire only. With a full-timeout baseline captured for ALL six suites
(`coder/baseline/basefull_*.txt`, at parent `b75e7c76e`), the core rewrite's real
regressions are:

| Suite | new reds | dominant root |
|---|---|---|
| Data | 2 | json-content (1) + dict-nested (1) |
| Wire | 2 | dict-nested (2) |
| Modules | 19 | **json-content** — 18 `Query_*` (LLM tool params) + `SettingsData_NestedPath` |
| Runtime | 15 | **json-content** — `ContentKindInference`, `JsonContainer`, `IsDict`, `Narrow*`, `DotField_OnFile`, `RawSlot_Dissolved`, … |
| Generator | 0 | — |

**~34 of the ~38 share ONE root: json content no longer parses.**

## Root: value-dispatch doesn't parse json-string content

Old path: a json-content source materialised through the **serializer** (`Json.Read` →
parsed to a dict/clr). The rewrite replaced serializer-dispatch with the type's own reader
over `value.Reader`. For json content the declared type is `{binary, json}` /
`{object, json}`, and its reader is the kindless object/dict `ReadSlot`, which hands a
**string token back unparsed**. So:

- `.json` file → `{binary,json}` content source → `Reader("binary","json")` narrows (via
  `TypeOf("json")` → `object`) → object `ReadSlot` → the json string, not a dict →
  `IsDict`/`NarrowsToDict`/`NavigatesJsonObject` all return null/false.
- LLM tool params (json) arrive as unparsed text → `text.Clr(structuredTarget)` throws
  `InvalidCastException: String cannot lower to this` (the 18 `Query_*`).

Evidence:
```
ContentKindInference_JsonExtension_NarrowsToDict → Expected "plang" but "null"
Query_SingleToolCall → InvalidCastException at text.Clr (text/this.cs:215)
SettingsData_NestedPath_NavigatesJsonObject → Expected "nested-value" but "null"
```

## Why it's a crossroads (not a coder call)

The fix is issue-1 — a **json reader that parses a json-string token** — which the plan
specced on `(object,json)`. But: (1) you told me the `object` type is **legacy, not to be
extended**; (2) `TypeOf("json")` resolves the `json` kind to `object`, so json content
reaches the legacy type by kind-resolution, not a direct stamp. So the honest options are:

1. **Re-point kind `"json"` → `item`** (the real apex), and land the json-string parse on
   `item` (which already has `item/serializer/json.cs` delegating to object's static). This
   avoids extending `object` and is the smallest step toward its removal.
2. **Remove `object` now** (the separate task you flagged has a stopper) — bigger, and the
   stopper may re-block.
3. **Add the `(object,json)` parse reader anyway** (plan's issue-1) as a temporary bridge,
   accepting it extends a legacy type, and clean up when `object` is removed.

The `dict-nested` cluster (2–3 tests: `DictOfTypedEntries`, `PlanDict`,
`AsT_DictWithNestedVars`) is a separate, smaller root (nested typed entries in a dict wire)
I can root-cause independently once the json direction is set.

## Ratified work that IS landed and clean (no regressions from it)

- Core rewrite (source/wire/type.Create/data-reader/channel receive door), pushed.
- Uniform write rule (`Owns`/`wire.Write`).
- **render-through-Output** (`Variable.Resolve` renders each `%var%` via `data.Output`,
  killing the Peek-glue + file/url carve-out) — fixed the string-literal quote-leak
  (StartGoal, interpolation); verified **zero** new Modules reds from it (140→140).
- StringIsContent (Option A) reverted per your call.

## Ask

Which json-content route (1 / 2 / 3)? That unblocks ~34 of the ~38 regressions. I'll take
the dict-nested cluster in parallel regardless.

— coder
