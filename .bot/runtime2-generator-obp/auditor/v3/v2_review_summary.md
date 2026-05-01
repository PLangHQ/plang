# v2 review summary

What I asked for in v2:

- **Major #1**: Restore the deleted `RawScalarValidations` safety net so that
  missing-or-null `Data<Variable>` slots produce a `MissingRequiredParameter`
  ServiceError instead of an NRE caught downstream as generic `StepError`.
  Suggested fix: detect `T : IRawNameResolvable` in Discovery, plumb through
  to `ActionClassInfo`, emit the guard in `Emission/Property/Data/this.cs`
  (~10 lines + 1 flag, mirror `IsSensitive` plumbing).
- **Minor #2**: review-quality finding on security/v2's count being off.
  Information only; no code change requested.
- **Minor #3**: tester gap — no regression test for the deleted contract.
  Asked for a parametrized test over the 22 handlers.

What coder/v8 did:

- Discovery now sets `isRawNameResolvable` on `DataProperty` by inspecting
  `T.AllInterfaces` for `App.Variables.IRawNameResolvable`. The flag is
  threaded through `EquatableArray<PropertyBase>` so incremental cache
  semantics still hold.
- Emission path differs from my suggestion: instead of adding the guard
  inside `Emission/Property/Data/this.cs`'s property getter, the validation
  is emitted in `Emission/Action/this.cs` as a pre-`Run()` check, mirroring
  the existing `[IsNotNull]` block. Functionally equivalent, structurally
  cleaner — both checks fire eagerly before `Run()` is called, so the NRE
  path is unreachable.
- Filter `!p.IsNullable` correctly excludes `foreach.ItemName` /
  `KeyName` (intentionally permissive nullable slots).
- New regression test: 20-row parametrized
  `MissingVariableNameTests.cs` covers all non-nullable
  `Data<Variable>` slots — variable.{get, set, exists, remove} +
  list.{add, any, contains, count, first, flatten, get, group, indexof,
  join, last, remove, reverse, set, sort, unique}. (My v2 finding text
  said "22 handlers" but that included the 2 nullable foreach slots —
  20 non-nullable is the correct count.)
- `IncrementalCacheTests.cs` updated for the new `DataProperty` ctor arg.
- Tests: 2570/2570 C# + 166/166 plang green (was 2550/2550 + 166/166;
  +20 from the new test).

What carries over:

- Minor #2 (security count) is a review-quality reflection — no code
  change required; coder v8 doesn't address it. That's correct.
- Minor #3 closed by the new `MissingVariableNameTests.cs`.

The fix matches my recommendation in shape (generator-side, eager check
before Run()) and improves on the placement (Action emitter rather than
Property getter — better separation, reuses the [IsNotNull] block's
infrastructure).
