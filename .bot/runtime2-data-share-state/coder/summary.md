# coder — runtime2-data-share-state

## v1 — Data identity preservation foundation

Landed phases 1, 2, 3, 4 (and the spot-check tests for 5a) of architect/v1's
6-phase plan. `Data` events become `List<Action<...>>` so cross-type wraps
(`As<T>`) can share state by reference; identity-preserving wrap rules
(same-type / variance / cross-type) replace the always-allocate `ConvertAndWrap`;
`AsCanonical` for plain-Data slots returns the LIVE variable Data;
`Variables.Set` is dumb storage with `Variables.Remove` firing OnDelete;
`variable.set` is the sole binding-mint site with type-inference + clone
semantics from a Data source.

Phases 5b (variable handlers Pattern B), 5c (list handlers Pattern A), 6
(generator + `[VariableName]` deletion) are deferred — they need rebuilt
`.pr` files because the parameter naming convention changes (e.g. `ListName`
→ `List`). The LLM build pipeline currently can't run on this branch, so
that work belongs to a follow-up branch with build access.

C# tests: 2524/2533 (9 remaining are forward-looking stubs for the deferred
phases). plang --test: 166/166 green (started at 145; brought back 21 tests).

See [v1/summary.md](v1/summary.md) for full details.

## v2 — variable resolution for complex objects + JsonNode conversion

User asked to run `plang --test`; result was 170/173 with 2 failures from
duplicate stale `tests/` (lowercase) files plus 1 stale bot test, AND a
broken builder (NRE in OpenAiProvider on every build). Investigation traced
the builder NRE to two underlying value-resolution bugs:

1. `AsCanonical` on plain `Data` only walked strings — list/dict parameter
   values with nested `%vars%` were never resolved (typed `As<T>()` walked
   them; the two paths had drifted). Extracted `WalkContainerVars`/
   `IsWalkableContainer` helpers; both paths now route through one rule.
2. `set ... type=json` mints `Data<JsonNode>`, but `TypeConverter` had no
   `JsonNode` arm in its complex-source dispatch — `JsonObject` slipped
   past every check (it implements `IDictionary<string, JsonNode?>`, not
   `IDictionary<string, object?>`). Added `JsonNode` to the dispatch and
   a parallel `JsonArray` element-iteration arm.

Cleanup: deleted lowercase `tests/` (duplicate of `Tests/`) and the stale
scaffolder bot test. C# tests: 2530/2539 (9 still pre-existing "Not
implemented" stubs); `plang --test`: 166/166. Builder now reaches the LLM
and gets past the original NRE; remaining `Actor`-param validation issue
is a separate pre-existing builder/template problem.

See [v2/summary.md](v2/summary.md) for full details.
