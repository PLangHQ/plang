# tester v2 — plan

Reviewing coder/v2 (commit `24cba238`) on top of codeanalyzer/v3 PASS at `ae827527`.

## Scope

Coder v2 was scoped to two **new** bug fixes (not v1 tester findings):
1. Nested `%var%` walk asymmetry between `AsCanonical` (plain Data) and `AsT_Impl` (Data<T>) — extracted `WalkContainerVars` helper, both call sites route through it.
2. `JsonNode` missing from `TypeConverter.TryConvertTo` complex-source dispatch + `JsonArray` element-iteration arm missing.

Production diff: `PLang/App/Data/this.cs` (+48/-15), `PLang/App/Utils/TypeConverter.cs` (+22/-7).
Tests: 4 new in `AsTIdentityTests.cs` (Rules 4c-4f), 2 new in `TypeMappingDictConversionTests.cs`.

## What I'm checking

1. **Run C# + plang test suites** — confirm coder's claim (2530/2539 C#, 166/166 plang).
2. **Coverage on new branches** — AsCanonical container branch, IsWalkableContainer/WalkContainerVars helpers, JsonNode dispatch arm, JsonArray arm.
3. **Deletion test on the 6 new tests** — confirm each new production line is actually pinned.
4. **Verify codeanalyzer/v3's flagged test gap** — the "state-aliasing on container-walk transient" claim. If true, deleting the 4 alias lines (`Properties`, `OnCreate`, `OnChange`, `OnDelete`) on `Data/this.cs:491-494` should NOT fail any test.
5. **Status of v1 tester's 7 findings** — coder v2 was a different scope, but check if any incidentally closed.

## Risk

- The new tests assert on resolved values (not ref-equality of containers or alias lists). If the comments claim "fresh Data with state aliased" but assertions don't pin aliasing, that's a false-green — exactly what codeanalyzer/v3 flagged.
- Rule 4f (LiteralList) claims to mirror "WalkList always allocates" — but a deletion of the walk for literals would still produce identical values in the assertion. Need to check if the walk is actually pinned.
